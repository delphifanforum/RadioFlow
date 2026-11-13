using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using RadioFlow.Core.Interfaces;
using RadioFlow.Core.Models;

namespace RadioFlow.Infrastructure.RadioBrowser;

/// <summary>
/// Radio Browser API istemcisi (https://api.radio-browser.info). İstekler asenkron ve iptal edilebilir;
/// her istek için zaman aşımı uygulanır, başarısız sunucuda bir sonrakine geçilerek sınırlı sayıda
/// yeniden deneme yapılır.
/// </summary>
public sealed class RadioBrowserClient(
    HttpClient httpClient,
    IRadioBrowserServerProvider serverProvider,
    IStreamUrlValidator validator,
    ILogger<RadioBrowserClient> logger) : IRadioCatalogService
{
    internal const int MaxAttempts = 3;
    internal static readonly TimeSpan RequestTimeout = TimeSpan.FromSeconds(10);

    private const string UnavailableMessage =
        "Radyo kataloğuna şu anda ulaşılamıyor. İnternet bağlantınızı kontrol edip tekrar deneyin. " +
        "Daha önce görüntülenen istasyonlar yerel önbellekten listelenebilir.";

    internal static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        NumberHandling = JsonNumberHandling.AllowReadingFromString,
    };

    public async Task<IReadOnlyList<RadioStation>> SearchAsync(StationQuery query, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        var path = BuildSearchPath(query);
        var dtos = await GetJsonAsync<List<RadioBrowserStationDto>>(path, cancellationToken).ConfigureAwait(false);
        return MapStations(dtos);
    }

    public Task<IReadOnlyList<RadioStation>> GetPopularAsync(string? countryCode, int limit, CancellationToken cancellationToken = default) =>
        SearchAsync(new StationQuery { CountryCode = countryCode, Limit = limit, Order = StationOrder.ClickCount }, cancellationToken);

    public async Task<IReadOnlyList<CatalogCategory>> GetCountriesAsync(CancellationToken cancellationToken = default)
    {
        var dtos = await GetJsonAsync<List<RadioBrowserCountryDto>>(
            "/json/countries?order=stationcount&reverse=true&hidebroken=true",
            cancellationToken).ConfigureAwait(false);

        return dtos
            .Where(d => !string.IsNullOrWhiteSpace(d.Iso3166) && d.Iso3166!.Length == 2 && !string.IsNullOrWhiteSpace(d.Name) && d.StationCount > 0)
            .Select(d => new CatalogCategory(d.Iso3166!.ToUpperInvariant(), d.Name!.Trim(), d.StationCount))
            .ToList();
    }

    public async Task<IReadOnlyList<CatalogCategory>> GetTagsAsync(int limit, CancellationToken cancellationToken = default)
    {
        var dtos = await GetJsonAsync<List<RadioBrowserTagDto>>(
            $"/json/tags?order=stationcount&reverse=true&hidebroken=true&limit={Math.Clamp(limit, 1, 500)}",
            cancellationToken).ConfigureAwait(false);

        return dtos
            .Where(d => !string.IsNullOrWhiteSpace(d.Name) && d.StationCount > 0)
            .Select(d => new CatalogCategory(d.Name!.Trim(), d.Name!.Trim(), d.StationCount))
            .ToList();
    }

    public async Task RegisterClickAsync(string externalId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(externalId) || !Guid.TryParse(externalId, out var uuid))
        {
            return;
        }

        try
        {
            await GetJsonAsync<JsonElement>($"/json/url/{uuid:D}", cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is CatalogUnavailableException or HttpRequestException)
        {
            logger.LogDebug(ex, "Tıklama bildirimi gönderilemedi.");
        }
    }

    internal static string BuildSearchPath(StationQuery query)
    {
        var sb = new StringBuilder("/json/stations/search?hidebroken=true");
        if (!string.IsNullOrWhiteSpace(query.Name))
        {
            sb.Append("&name=").Append(Uri.EscapeDataString(query.Name.Trim()));
        }

        if (!string.IsNullOrWhiteSpace(query.CountryCode))
        {
            sb.Append("&countrycode=").Append(Uri.EscapeDataString(query.CountryCode.Trim().ToUpperInvariant()));
        }

        if (!string.IsNullOrWhiteSpace(query.Tag))
        {
            sb.Append("&tag=").Append(Uri.EscapeDataString(query.Tag.Trim())).Append("&tagExact=true");
        }

        var (order, reverse) = query.Order switch
        {
            StationOrder.Votes => ("votes", true),
            StationOrder.Name => ("name", false),
            _ => ("clickcount", true),
        };
        sb.Append("&order=").Append(order).Append("&reverse=").Append(reverse ? "true" : "false");
        sb.Append("&limit=").Append(Math.Clamp(query.Limit, 1, 500).ToString(CultureInfo.InvariantCulture));
        sb.Append("&offset=").Append(Math.Max(0, query.Offset).ToString(CultureInfo.InvariantCulture));
        return sb.ToString();
    }

    private IReadOnlyList<RadioStation> MapStations(IEnumerable<RadioBrowserStationDto> dtos)
    {
        var result = new List<RadioStation>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var rejected = 0;
        foreach (var dto in dtos)
        {
            var station = RadioBrowserMapper.ToStation(dto, validator);
            if (station is null)
            {
                rejected++;
                continue;
            }

            if (seen.Add(station.ExternalId!))
            {
                result.Add(station);
            }
        }

        if (rejected > 0)
        {
            logger.LogInformation("{Count} istasyon geçersiz veya güvenli olmayan URL nedeniyle listelenmedi.", rejected);
        }

        return result;
    }

    private async Task<T> GetJsonAsync<T>(string pathAndQuery, CancellationToken cancellationToken)
    {
        var servers = await serverProvider.GetServersAsync(cancellationToken).ConfigureAwait(false);
        Exception? lastError = null;

        if (servers.Count == 0)
        {
            throw new CatalogUnavailableException(UnavailableMessage);
        }

        for (var attempt = 0; attempt < MaxAttempts; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var server = servers[attempt % servers.Count];
            var uri = new Uri($"https://{server}{pathAndQuery}");

            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(RequestTimeout);
            try
            {
                using var response = await httpClient.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead, timeout.Token).ConfigureAwait(false);
                if (response.IsSuccessStatusCode)
                {
                    var value = await response.Content.ReadFromJsonAsync<T>(JsonOptions, timeout.Token).ConfigureAwait(false);
                    return value ?? throw new JsonException("Boş yanıt.");
                }

                if ((int)response.StatusCode is >= 400 and < 500 && response.StatusCode != HttpStatusCode.TooManyRequests)
                {
                    logger.LogWarning("Radio Browser isteği reddedildi: {Status} {Uri}", (int)response.StatusCode, uri);
                    throw new CatalogUnavailableException("Radyo kataloğu isteği kabul etmedi. Lütfen daha sonra tekrar deneyin.");
                }

                lastError = new HttpRequestException($"HTTP {(int)response.StatusCode}");
                logger.LogWarning("Radio Browser sunucu hatası: {Status} {Uri}", (int)response.StatusCode, uri);
            }
            catch (OperationCanceledException ex) when (!cancellationToken.IsCancellationRequested)
            {
                lastError = ex;
                logger.LogWarning("Radio Browser isteği zaman aşımına uğradı: {Uri}", uri);
            }
            catch (HttpRequestException ex)
            {
                lastError = ex;
                logger.LogWarning(ex, "Radio Browser HTTP hatası: {Uri}", uri);
            }
            catch (JsonException ex)
            {
                lastError = ex;
                logger.LogWarning(ex, "Radio Browser yanıtı çözümlenemedi: {Uri}", uri);
            }

            serverProvider.ReportFailure(server);
            if (attempt + 1 < MaxAttempts)
            {
                await Task.Delay(TimeSpan.FromMilliseconds(300 * (attempt + 1)), cancellationToken).ConfigureAwait(false);
            }
        }

        throw new CatalogUnavailableException(UnavailableMessage, lastError ?? new HttpRequestException("Sunucu yok."));
    }
}
