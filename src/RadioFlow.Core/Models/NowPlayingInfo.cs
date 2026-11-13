namespace RadioFlow.Core.Models;

/// <summary>
/// ICY metadata ile gelen şarkı bilgisi. Yayın metadata göndermiyorsa nesne oluşturulmaz;
/// uygulama asla sahte şarkı bilgisi üretmez.
/// </summary>
public sealed record NowPlayingInfo(string RawText, string? Artist, string? Title)
{
    /// <summary>"Sanatçı - Şarkı" biçimindeki ICY StreamTitle değerini ayrıştırır.</summary>
    public static NowPlayingInfo? Parse(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return null;
        }

        var text = raw.Trim();
        // Bazı yayınlar boş/yer tutucu başlık gönderir.
        if (text is "-" or "" || text.Equals("Unknown", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var separatorIndex = text.IndexOf(" - ", StringComparison.Ordinal);
        if (separatorIndex > 0 && separatorIndex < text.Length - 3)
        {
            var artist = text[..separatorIndex].Trim();
            var title = text[(separatorIndex + 3)..].Trim();
            return new NowPlayingInfo(text, artist.Length > 0 ? artist : null, title.Length > 0 ? title : null);
        }

        return new NowPlayingInfo(text, null, text);
    }

    public string DisplayText => Artist is not null && Title is not null ? $"{Artist} — {Title}" : RawText;
}
