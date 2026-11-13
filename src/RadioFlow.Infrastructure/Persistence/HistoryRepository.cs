using RadioFlow.Core.Interfaces;
using RadioFlow.Core.Models;
using static RadioFlow.Infrastructure.Persistence.StationMapping;

namespace RadioFlow.Infrastructure.Persistence;

public sealed class HistoryRepository(SqliteConnectionFactory connectionFactory) : IHistoryRepository
{
    public async Task<long> StartAsync(long stationId, DateTimeOffset startedAt, CancellationToken cancellationToken = default)
    {
        await using var connection = await connectionFactory.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO ListeningHistory (StationId, StartedAt, DurationSeconds)
            VALUES ($id, $started, 0)
            RETURNING Id;
            """;
        command.Parameters.AddWithValue("$id", stationId);
        command.Parameters.AddWithValue("$started", FormatDate(startedAt));
        var result = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
        return Convert.ToInt64(result, System.Globalization.CultureInfo.InvariantCulture);
    }

    public async Task EndAsync(long historyId, int durationSeconds, CancellationToken cancellationToken = default)
    {
        await using var connection = await connectionFactory.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        command.CommandText = "UPDATE ListeningHistory SET DurationSeconds = $d WHERE Id = $id;";
        command.Parameters.AddWithValue("$d", Math.Max(0, durationSeconds));
        command.Parameters.AddWithValue("$id", historyId);
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<RadioStation>> GetRecentStationsAsync(int limit, CancellationToken cancellationToken = default)
    {
        await using var connection = await connectionFactory.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        command.CommandText = $"""
            SELECT {Columns}
            FROM RadioStations s
            JOIN (SELECT StationId, MAX(StartedAt) AS LastPlayed
                  FROM ListeningHistory
                  GROUP BY StationId) h ON h.StationId = s.Id
            ORDER BY h.LastPlayed DESC
            LIMIT $limit;
            """;
        command.Parameters.AddWithValue("$limit", Math.Clamp(limit, 1, 500));
        return await ReadAllAsync(command, cancellationToken).ConfigureAwait(false);
    }
}
