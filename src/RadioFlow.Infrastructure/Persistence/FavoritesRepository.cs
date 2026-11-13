using Microsoft.Data.Sqlite;
using RadioFlow.Core.Interfaces;
using RadioFlow.Core.Models;
using static RadioFlow.Infrastructure.Persistence.StationMapping;

namespace RadioFlow.Infrastructure.Persistence;

public sealed class FavoritesRepository(SqliteConnectionFactory connectionFactory) : IFavoritesRepository
{
    public async Task<IReadOnlyList<RadioStation>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = await connectionFactory.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        command.CommandText = $"""
            SELECT {Columns}
            FROM Favorites f
            JOIN RadioStations s ON s.Id = f.StationId
            ORDER BY f.SortOrder, f.Id;
            """;
        return await ReadAllAsync(command, cancellationToken).ConfigureAwait(false);
    }

    public async Task<bool> IsFavoriteAsync(long stationId, CancellationToken cancellationToken = default)
    {
        await using var connection = await connectionFactory.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT EXISTS(SELECT 1 FROM Favorites WHERE StationId = $id);";
        command.Parameters.AddWithValue("$id", stationId);
        var result = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
        return Convert.ToInt64(result, System.Globalization.CultureInfo.InvariantCulture) != 0;
    }

    public async Task<bool> AddAsync(long stationId, CancellationToken cancellationToken = default)
    {
        await using var connection = await connectionFactory.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT OR IGNORE INTO Favorites (StationId, SortOrder, CreatedAt)
            VALUES ($id, COALESCE((SELECT MAX(SortOrder) + 1 FROM Favorites), 0), $now);
            """;
        command.Parameters.AddWithValue("$id", stationId);
        command.Parameters.AddWithValue("$now", FormatDate(DateTimeOffset.UtcNow));
        return await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false) > 0;
    }

    public async Task<bool> RemoveAsync(long stationId, CancellationToken cancellationToken = default)
    {
        await using var connection = await connectionFactory.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM Favorites WHERE StationId = $id;";
        command.Parameters.AddWithValue("$id", stationId);
        return await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false) > 0;
    }

    public async Task ReorderAsync(IReadOnlyList<long> orderedStationIds, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(orderedStationIds);
        await using var connection = await connectionFactory.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var transaction = (SqliteTransaction)await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "UPDATE Favorites SET SortOrder = $order WHERE StationId = $id;";
        var orderParam = command.Parameters.Add("$order", SqliteType.Integer);
        var idParam = command.Parameters.Add("$id", SqliteType.Integer);
        for (var i = 0; i < orderedStationIds.Count; i++)
        {
            orderParam.Value = i;
            idParam.Value = orderedStationIds[i];
            await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }

        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
    }
}
