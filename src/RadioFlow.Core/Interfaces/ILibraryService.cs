using RadioFlow.Core.Models;

namespace RadioFlow.Core.Interfaces;

/// <summary>
/// Favoriler, geçmiş ve özel istasyonlar için ViewModel'lerin kullandığı üst seviye servis.
/// </summary>
public interface ILibraryService
{
    /// <summary>İstasyon veritabanında yoksa kaydeder ve kalıcı kimliğiyle döndürür.</summary>
    Task<RadioStation> EnsurePersistedAsync(RadioStation station, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<RadioStation>> GetFavoritesAsync(CancellationToken cancellationToken = default);

    Task<bool> IsFavoriteAsync(RadioStation station, CancellationToken cancellationToken = default);

    /// <summary>Favori durumunu tersine çevirir; yeni durumu döndürür.</summary>
    Task<bool> ToggleFavoriteAsync(RadioStation station, CancellationToken cancellationToken = default);

    Task MoveFavoriteAsync(RadioStation station, int offset, CancellationToken cancellationToken = default);

    Task<RadioStation?> GetAdjacentFavoriteAsync(RadioStation? current, int direction, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<RadioStation>> GetRecentAsync(int limit, CancellationToken cancellationToken = default);

    Task<RadioStation> AddCustomStationAsync(string name, string streamUrl, string? logoUrl, string? tags, CancellationToken cancellationToken = default);

    Task<bool> DeleteCustomStationAsync(RadioStation station, CancellationToken cancellationToken = default);

    Task<RadioStation?> GetStationAsync(long id, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<RadioStation>> SearchCachedAsync(StationQuery query, CancellationToken cancellationToken = default);

    Task CacheStationsAsync(IEnumerable<RadioStation> stationsToCache, CancellationToken cancellationToken = default);

    event EventHandler? FavoritesChanged;
}
