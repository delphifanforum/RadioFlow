using Microsoft.Data.Sqlite;

namespace RadioFlow.Infrastructure.Persistence;

public sealed class SqliteConnectionFactory
{
    private readonly string _connectionString;

    public SqliteConnectionFactory(string databasePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(databasePath);
        DatabasePath = databasePath;
        _connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = databasePath,
            Mode = SqliteOpenMode.ReadWriteCreate,
            ForeignKeys = true,
            Pooling = true,
            DefaultTimeout = 10,
        }.ToString();
    }

    public string DatabasePath { get; }

    public async Task<SqliteConnection> OpenAsync(CancellationToken cancellationToken = default)
    {
        var connection = new SqliteConnection(_connectionString);
        try
        {
            await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
            return connection;
        }
        catch
        {
            await connection.DisposeAsync().ConfigureAwait(false);
            throw;
        }
    }
}
