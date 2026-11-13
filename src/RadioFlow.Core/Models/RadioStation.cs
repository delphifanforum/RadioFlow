namespace RadioFlow.Core.Models;

/// <summary>
/// Bir radyo istasyonu. <see cref="Id"/> yerel veritabanı kimliğidir (0 = henüz kaydedilmemiş);
/// <see cref="ExternalId"/> Radio Browser "stationuuid" değeridir (özel istasyonlarda null).
/// </summary>
public sealed record RadioStation
{
    public long Id { get; init; }
    public string? ExternalId { get; init; }
    public required string Name { get; init; }
    public required string StreamUrl { get; init; }
    public string? Country { get; init; }
    public string? CountryCode { get; init; }
    public string? Language { get; init; }
    public string? Tags { get; init; }
    public string? LogoUrl { get; init; }
    public string? Homepage { get; init; }
    public string? Codec { get; init; }
    public int Bitrate { get; init; }
    public bool IsCustom { get; init; }
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;

    /// <summary>Etiket listesini (virgülle ayrılmış) ayrıştırır.</summary>
    public IReadOnlyList<string> TagList =>
        string.IsNullOrWhiteSpace(Tags)
            ? []
            : Tags.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    /// <summary>Kartlarda gösterilecek birincil tür.</summary>
    public string? PrimaryGenre => TagList.Count > 0 ? TagList[0] : null;

    /// <summary>İki istasyonun aynı kaynağı temsil edip etmediği.</summary>
    public bool IsSameStationAs(RadioStation? other)
    {
        if (other is null)
        {
            return false;
        }

        if (Id != 0 && other.Id != 0)
        {
            return Id == other.Id;
        }

        if (!string.IsNullOrEmpty(ExternalId) && !string.IsNullOrEmpty(other.ExternalId))
        {
            return string.Equals(ExternalId, other.ExternalId, StringComparison.OrdinalIgnoreCase);
        }

        return string.Equals(StreamUrl, other.StreamUrl, StringComparison.OrdinalIgnoreCase);
    }
}
