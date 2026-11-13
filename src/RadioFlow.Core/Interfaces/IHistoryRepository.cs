using RadioFlow.Core.Models;

namespace RadioFlow.Core.Interfaces;

public interface IHistoryRepository
{
    Task<long> StartAsync(long stationId, DateTimeOffset startedAt, CancellationToken cancellationToken = default);

    Task EndAsync(long historyId, int durationSeconds, CancellationToken cancellationToken = default);

    /// <summary>En son dinlenen benzersiz istasyonlar (yeniden eskiye).</summary>
    Task<IReadOnlyList<RadioStation>> GetRecentStationsAsync(int limit, CancellationToken cancellationToken = default);
}
