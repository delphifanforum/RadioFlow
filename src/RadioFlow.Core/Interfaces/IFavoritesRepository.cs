using RadioFlow.Core.Models;

namespace RadioFlow.Core.Interfaces;

public interface IFavoritesRepository
{
    Task<IReadOnlyList<RadioStation>> GetAllAsync(CancellationToken cancellationToken = default);

    Task<bool> IsFavoriteAsync(long stationId, CancellationToken cancellationToken = default);

    /// <summary>Favoriye ekler (zaten varsa değişiklik yapmaz). Yeni eklendiyse true.</summary>
    Task<bool> AddAsync(long stationId, CancellationToken cancellationToken = default);

    Task<bool> RemoveAsync(long stationId, CancellationToken cancellationToken = default);

    /// <summary>Favorileri verilen istasyon kimliği sırasına göre yeniden sıralar.</summary>
    Task ReorderAsync(IReadOnlyList<long> orderedStationIds, CancellationToken cancellationToken = default);
}
