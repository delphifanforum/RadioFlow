using RadioFlow.Core.Models;

namespace RadioFlow.Core.Interfaces;

public interface IStationRepository
{
    /// <summary>İstasyonu ekler veya (ExternalId eşleşirse) günceller; kalıcı kimliğiyle döndürür.</summary>
    Task<RadioStation> UpsertAsync(RadioStation station, CancellationToken cancellationToken = default);

    Task UpsertManyAsync(IEnumerable<RadioStation> stations, CancellationToken cancellationToken = default);

    Task<RadioStation?> GetByIdAsync(long id, CancellationToken cancellationToken = default);

    Task<RadioStation?> GetByExternalIdAsync(string externalId, CancellationToken cancellationToken = default);

    /// <summary>Yerel önbellekte Türkçe duyarlı arama.</summary>
    Task<IReadOnlyList<RadioStation>> SearchCachedAsync(StationQuery query, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<RadioStation>> GetCustomStationsAsync(CancellationToken cancellationToken = default);

    Task<bool> DeleteCustomAsync(long id, CancellationToken cancellationToken = default);
}
