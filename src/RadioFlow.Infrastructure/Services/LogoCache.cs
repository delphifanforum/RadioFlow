using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Logging;
using RadioFlow.Core.Interfaces;

namespace RadioFlow.Infrastructure.Services;

/// <summary>
/// İstasyon logolarını yalnızca http/https üzerinden, boyut ve tür sınırıyla indirir ve diskte
/// (%LOCALAPPDATA%\RadioFlow\Cache\Logos) saklar. Aynı logo için eşzamanlı indirmeler tekilleştirilir.
/// Görüntü çözümleme UI katmanında küçük boyutlu (DecodePixelWidth) yapılır.
/// </summary>
public sealed class LogoCache(HttpClient httpClient, IAppPaths paths, IStreamUrlValidator validator, ILogger<LogoCache> logger)
    : ILogoCache
{
    internal const int MaxLogoBytes = 512 * 1024;
    private static readonly TimeSpan CacheLifetime = TimeSpan.FromDays(30);
    private static readonly TimeSpan FailureRetryAfter = TimeSpan.FromHours(6);

    // Aynı anda en fazla 6 logo indirilir (yüzlerce kart açıldığında ağı boğmamak için).
    private static readonly SemaphoreSlim DownloadThrottle = new(6, 6);

    private readonly ConcurrentDictionary<string, Lazy<Task<string?>>> _inflight = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, DateTimeOffset> _failures = new(StringComparer.Ordinal);

    public Task<string?> GetLogoPathAsync(string? logoUrl, CancellationToken cancellationToken = default)
    {
        if (!validator.TryValidate(logoUrl, out var uri, out _, allowPrivateHosts: false))
        {
            return Task.FromResult<string?>(null);
        }

        var key = Hash(uri!.AbsoluteUri);
        var path = Path.Combine(paths.LogoCacheDirectory, key + ".img");
        if (File.Exists(path) && DateTime.UtcNow - File.GetLastWriteTimeUtc(path) < CacheLifetime)
        {
            return Task.FromResult<string?>(path);
        }

        if (_failures.TryGetValue(key, out var failedAt) && DateTimeOffset.UtcNow - failedAt < FailureRetryAfter)
        {
            return Task.FromResult<string?>(null);
        }

        var lazy = _inflight.GetOrAdd(key, _ => new Lazy<Task<string?>>(() => DownloadAsync(uri, key, path)));
        return lazy.Value.WaitAsync(cancellationToken);
    }

    private static string Hash(string value) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)))[..32].ToLowerInvariant();

    private async Task<string?> DownloadAsync(Uri uri, string key, string path)
    {
        await DownloadThrottle.WaitAsync().ConfigureAwait(false);
        try
        {
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            using var response = await httpClient.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead, timeout.Token).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                _failures[key] = DateTimeOffset.UtcNow;
                return null;
            }

            var mediaType = response.Content.Headers.ContentType?.MediaType;
            if (mediaType is not null
                && !mediaType.StartsWith("image/", StringComparison.OrdinalIgnoreCase)
                && !mediaType.Equals("application/octet-stream", StringComparison.OrdinalIgnoreCase))
            {
                _failures[key] = DateTimeOffset.UtcNow;
                return null;
            }

            if (mediaType is not null && mediaType.Contains("svg", StringComparison.OrdinalIgnoreCase))
            {
                // WPF SVG çözemez; ayrıca SVG betik içerebilir.
                _failures[key] = DateTimeOffset.UtcNow;
                return null;
            }

            if (response.Content.Headers.ContentLength is > MaxLogoBytes)
            {
                _failures[key] = DateTimeOffset.UtcNow;
                return null;
            }

            await using var source = await response.Content.ReadAsStreamAsync(timeout.Token).ConfigureAwait(false);
            using var buffer = new MemoryStream();
            var chunk = new byte[16 * 1024];
            int read;
            while ((read = await source.ReadAsync(chunk, timeout.Token).ConfigureAwait(false)) > 0)
            {
                if (buffer.Length + read > MaxLogoBytes)
                {
                    _failures[key] = DateTimeOffset.UtcNow;
                    return null;
                }

                buffer.Write(chunk, 0, read);
            }

            if (!LooksLikeRasterImage(buffer.GetBuffer().AsSpan(0, (int)buffer.Length)))
            {
                _failures[key] = DateTimeOffset.UtcNow;
                return null;
            }

            var tempPath = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
            await File.WriteAllBytesAsync(tempPath, buffer.ToArray()).ConfigureAwait(false);
            File.Move(tempPath, path, overwrite: true);
            return path;
        }
        catch (Exception ex) when (ex is HttpRequestException or OperationCanceledException or IOException or UnauthorizedAccessException)
        {
            logger.LogDebug(ex, "Logo indirilemedi: {Url}", uri);
            _failures[key] = DateTimeOffset.UtcNow;
            return null;
        }
        finally
        {
            DownloadThrottle.Release();
            _inflight.TryRemove(key, out _);
        }
    }

    /// <summary>PNG, JPEG, GIF, BMP, ICO ve WebP imzalarını tanır.</summary>
    internal static bool LooksLikeRasterImage(ReadOnlySpan<byte> data)
    {
        if (data.Length < 12)
        {
            return false;
        }

        return data[..8].SequenceEqual(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A }) // PNG
            || (data[0] == 0xFF && data[1] == 0xD8 && data[2] == 0xFF)                              // JPEG
            || (data[0] == (byte)'G' && data[1] == (byte)'I' && data[2] == (byte)'F')                // GIF
            || (data[0] == (byte)'B' && data[1] == (byte)'M')                                       // BMP
            || (data[0] == 0 && data[1] == 0 && data[2] == 1 && data[3] == 0)                       // ICO
            || (data[..4].SequenceEqual("RIFF"u8) && data[8..12].SequenceEqual("WEBP"u8));          // WebP
    }
}
