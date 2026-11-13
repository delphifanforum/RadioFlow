using System.Net;
using Microsoft.Extensions.Logging;

namespace RadioFlow.Infrastructure.RadioBrowser;

public interface IRadioBrowserServerProvider
{
    Task<IReadOnlyList<string>> GetServersAsync(CancellationToken cancellationToken = default);

    void ReportFailure(string server);
}

/// <summary>
/// Radio Browser'ın önerdiği yöntemle sunucu keşfi: all.api.radio-browser.info için DNS sorgusu,
/// ardından ters DNS ile sunucu adları; liste karıştırılır. DNS başarısız olursa bilinen sunuculara düşülür.
/// </summary>
public sealed class RadioBrowserServerProvider(ILogger<RadioBrowserServerProvider> logger) : IRadioBrowserServerProvider
{
    internal const string DiscoveryHost = "all.api.radio-browser.info";
    internal static readonly string[] FallbackServers =
    [
        "de1.api.radio-browser.info",
        "de2.api.radio-browser.info",
        "fi1.api.radio-browser.info",
    ];

    private static readonly TimeSpan CacheDuration = TimeSpan.FromHours(6);
    private readonly SemaphoreSlim _gate = new(1, 1);
    private List<string>? _servers;
    private DateTimeOffset _resolvedAt;

    public async Task<IReadOnlyList<string>> GetServersAsync(CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_servers is { Count: > 0 } && DateTimeOffset.UtcNow - _resolvedAt < CacheDuration)
            {
                return _servers.ToArray();
            }

            var discovered = await DiscoverAsync(cancellationToken).ConfigureAwait(false);
            var list = discovered.Count > 0 ? discovered.ToArray() : FallbackServers.ToArray();
            Random.Shared.Shuffle(list);
            _servers = [.. list];
            _resolvedAt = DateTimeOffset.UtcNow;
            logger.LogInformation("Radio Browser sunucuları: {Servers}", string.Join(", ", _servers));
            return _servers.ToArray();
        }
        finally
        {
            _gate.Release();
        }
    }

    public void ReportFailure(string server)
    {
        // Başarısız sunucuyu listenin sonuna taşı.
        _gate.Wait();
        try
        {
            if (_servers is not null && _servers.Remove(server))
            {
                _servers.Add(server);
            }
        }
        finally
        {
            _gate.Release();
        }
    }

    private async Task<IReadOnlyList<string>> DiscoverAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(TimeSpan.FromSeconds(5));
            var addresses = await Dns.GetHostAddressesAsync(DiscoveryHost, timeout.Token).ConfigureAwait(false);
            var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var address in addresses)
            {
                try
                {
                    var entry = await Dns.GetHostEntryAsync(address.ToString(), timeout.Token).ConfigureAwait(false);
                    if (entry.HostName.EndsWith(".radio-browser.info", StringComparison.OrdinalIgnoreCase))
                    {
                        names.Add(entry.HostName.TrimEnd('.'));
                    }
                }
                catch (Exception ex) when (ex is System.Net.Sockets.SocketException or OperationCanceledException)
                {
                    logger.LogDebug(ex, "Ters DNS başarısız: {Address}", address);
                }
            }

            return [.. names];
        }
        catch (Exception ex) when (ex is System.Net.Sockets.SocketException or OperationCanceledException && !cancellationToken.IsCancellationRequested)
        {
            logger.LogWarning(ex, "Radio Browser DNS keşfi başarısız; yedek sunucular kullanılacak.");
            return [];
        }
    }
}
