using Microsoft.Data.Sqlite;
using RadioFlow.Core.Interfaces;

namespace RadioFlow.Infrastructure.Persistence;

public sealed class SettingsStore(SqliteConnectionFactory connectionFactory) : ISettingsStore
{
    public async Task<IReadOnlyDictionary<string, string>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = await connectionFactory.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT Key, Value FROM Settings;";
        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            result[reader.GetString(0)] = reader.GetString(1);
        }

        return result;
    }

    public async Task SetManyAsync(IReadOnlyDictionary<string, string> values, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(values);
        await using var connection = await connectionFactory.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var transaction = (SqliteTransaction)await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            INSERT INTO Settings (Key, Value) VALUES ($key, $value)
            ON CONFLICT(Key) DO UPDATE SET Value = excluded.Value;
            """;
        var keyParam = command.Parameters.Add("$key", SqliteType.Text);
        var valueParam = command.Parameters.Add("$value", SqliteType.Text);
        foreach (var (key, value) in values)
        {
            keyParam.Value = key;
            valueParam.Value = value;
            await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }

        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
    }
}
