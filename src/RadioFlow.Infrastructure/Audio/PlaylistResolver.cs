using System.Text;
using Microsoft.Extensions.Logging;
using RadioFlow.Core.Interfaces;

namespace RadioFlow.Infrastructure.Audio;

/// <summary>
/// .m3u / .pls çalma listelerini indirir (boyut ve derinlik sınırlı), içindeki ilk geçerli
/// http/https girdisini döndürür. file:// gibi girdiler reddedilir; böylece LibVLC'ye
/// güvenilmeyen bir çalma listesi doğrudan verilmez.
/// </summary>
public sealed class PlaylistResolver(
    HttpClient httpClient,
    IStreamUrlValidator validator,
    ILogger<PlaylistResolver> logger) : IPlaylistResolver
{
    internal const int MaxPlaylistBytes = 64 * 1024;
    internal const int MaxDepth = 3;

    private static readonly HashSet<string> StreamMediaTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "audio/mpeg", "audio/mp3", "audio/aac", "audio/aacp", "audio/ogg", "application/ogg",
        "audio/opus", "audio/flac", "audio/x-flac", "audio/webm", "application/octet-stream",
    };

    public Task<Uri> ResolveAsync(Uri url, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(url);
        // Kullanıcının yerel ağdaki özel istasyonu ise çalma listesi girdilerinin de yerel olmasına izin ver.
        var allowPrivate = !validator.TryValidate(url.OriginalString, out _, out _, allowPrivateHosts: false);
        return ResolveCoreAsync(url, allowPrivate, depth: 0, cancellationToken);
    }

    internal static bool LooksLikePlaylist(Uri url)
    {
        var path = url.AbsolutePath;
        return path.EndsWith(".m3u", StringComparison.OrdinalIgnoreCase)
            || path.EndsWith(".pls", StringComparison.OrdinalIgnoreCase);
    }

    internal static IReadOnlyList<string> ParseEntries(string content)
    {
        var entries = new List<string>();
        var isPls = content.Contains("[playlist]", StringComparison.OrdinalIgnoreCase);
        using var reader = new StringReader(content);
        string? line;
        while ((line = reader.ReadLine()) is not null)
        {
            line = line.Trim().TrimStart('﻿');
            if (line.Length == 0)
            {
                continue;
            }

            if (isPls)
            {
                if (line.StartsWith("File", StringComparison.OrdinalIgnoreCase))
                {
                    var eq = line.IndexOf('=', StringComparison.Ordinal);
                    if (eq > 0 && eq < line.Length - 1)
                    {
                        entries.Add(line[(eq + 1)..].Trim());
                    }
                }
            }
            else if (!line.StartsWith('#'))
            {
                entries.Add(line);
            }
        }

        return entries;
    }

    private async Task<Uri> ResolveCoreAsync(Uri url, bool allowPrivate, int depth, CancellationToken cancellationToken)
    {
        if (!LooksLikePlaylist(url))
        {
            return url;
        }

        if (depth >= MaxDepth)
        {
            throw new PlaylistResolutionException("Çalma listesi iç içe çok fazla yönlendirme içeriyor.");
        }

        logger.LogInformation("Çalma listesi çözülüyor: {Url}", url);
        using var response = await httpClient
            .GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
            .ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        var finalUri = response.RequestMessage?.RequestUri ?? url;
        if (!validator.TryValidate(finalUri.OriginalString, out _, out var redirectError, allowPrivate))
        {
            throw new PlaylistResolutionException($"Çalma listesi güvenilmeyen bir adrese yönlendirildi: {redirectError}");
        }

        var mediaType = response.Content.Headers.ContentType?.MediaType;
        if (mediaType is not null && StreamMediaTypes.Contains(mediaType))
        {
            // Uzantısı .m3u/.pls olsa da sunucu doğrudan ses akışı döndürüyor.
            return finalUri;
        }

        var content = await ReadLimitedAsync(response.Content, cancellationToken).ConfigureAwait(false);
        foreach (var entry in ParseEntries(content))
        {
            if (validator.TryValidate(entry, out var entryUri, out _, allowPrivate))
            {
                return await ResolveCoreAsync(entryUri!, allowPrivate, depth + 1, cancellationToken).ConfigureAwait(false);
            }

            logger.LogWarning("Çalma listesindeki güvenli olmayan girdi atlandı: {Entry}", entry);
        }

        throw new PlaylistResolutionException("Çalma listesinde oynatılabilir bir yayın adresi bulunamadı.");
    }

    private static async Task<string> ReadLimitedAsync(HttpContent content, CancellationToken cancellationToken)
    {
        await using var stream = await content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        var buffer = new byte[MaxPlaylistBytes];
        var total = 0;
        int read;
        while (total < buffer.Length
               && (read = await stream.ReadAsync(buffer.AsMemory(total, buffer.Length - total), cancellationToken).ConfigureAwait(false)) > 0)
        {
            total += read;
        }

        return Encoding.UTF8.GetString(buffer, 0, total);
    }
}

public sealed class PlaylistResolutionException : Exception
{
    public PlaylistResolutionException()
    {
    }

    public PlaylistResolutionException(string message)
        : base(message)
    {
    }

    public PlaylistResolutionException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
