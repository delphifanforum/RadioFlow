using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;

namespace RadioFlow.Infrastructure.Persistence;

/// <summary>
/// Basit, sürümlü şema geçiş sistemi. Sürüm PRAGMA user_version içinde tutulur; her geçiş bir
/// işlem (transaction) içinde uygulanır. Var olan kullanıcı verisi silinmez.
/// </summary>
public sealed class DatabaseInitializer(SqliteConnectionFactory connectionFactory, ILogger<DatabaseInitializer> logger)
{
    internal static readonly IReadOnlyList<(int Version, string Sql)> Migrations =
    [
        (1, """
            CREATE TABLE IF NOT EXISTS RadioStations (
                Id           INTEGER PRIMARY KEY AUTOINCREMENT,
                ExternalId   TEXT NULL UNIQUE,
                Name         TEXT NOT NULL,
                NameFolded   TEXT NOT NULL DEFAULT '',
                Country      TEXT NULL,
                CountryCode  TEXT NULL,
                Language     TEXT NULL,
                Tags         TEXT NULL,
                TagsFolded   TEXT NOT NULL DEFAULT '',
                LogoUrl      TEXT NULL,
                Homepage     TEXT NULL,
                StreamUrl    TEXT NOT NULL,
                Codec        TEXT NULL,
                Bitrate      INTEGER NOT NULL DEFAULT 0,
                IsCustom     INTEGER NOT NULL DEFAULT 0,
                CreatedAt    TEXT NOT NULL,
                UpdatedAt    TEXT NOT NULL
            );
            CREATE INDEX IF NOT EXISTS IX_RadioStations_CountryCode ON RadioStations(CountryCode);
            CREATE INDEX IF NOT EXISTS IX_RadioStations_NameFolded ON RadioStations(NameFolded);

            CREATE TABLE IF NOT EXISTS Favorites (
                Id         INTEGER PRIMARY KEY AUTOINCREMENT,
                StationId  INTEGER NOT NULL UNIQUE REFERENCES RadioStations(Id) ON DELETE CASCADE,
                SortOrder  INTEGER NOT NULL,
                CreatedAt  TEXT NOT NULL
            );
            CREATE INDEX IF NOT EXISTS IX_Favorites_SortOrder ON Favorites(SortOrder);

            CREATE TABLE IF NOT EXISTS ListeningHistory (
                Id               INTEGER PRIMARY KEY AUTOINCREMENT,
                StationId        INTEGER NOT NULL REFERENCES RadioStations(Id) ON DELETE CASCADE,
                StartedAt        TEXT NOT NULL,
                DurationSeconds  INTEGER NOT NULL DEFAULT 0
            );
            CREATE INDEX IF NOT EXISTS IX_ListeningHistory_StartedAt ON ListeningHistory(StartedAt);

            CREATE TABLE IF NOT EXISTS Settings (
                Key    TEXT NOT NULL PRIMARY KEY,
                Value  TEXT NOT NULL
            );
            """),
    ];

    public static int LatestVersion => Migrations[^1].Version;

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        var directory = Path.GetDirectoryName(connectionFactory.DatabasePath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        await using var connection = await connectionFactory.OpenAsync(cancellationToken).ConfigureAwait(false);

        await using (var wal = connection.CreateCommand())
        {
            wal.CommandText = "PRAGMA journal_mode=WAL;";
            await wal.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }

        var current = await GetUserVersionAsync(connection, cancellationToken).ConfigureAwait(false);
        foreach (var (version, sql) in Migrations.Where(m => m.Version > current).OrderBy(m => m.Version))
        {
            logger.LogInformation("Veritabanı geçişi uygulanıyor: v{Version}", version);
            await using var transaction = (SqliteTransaction)await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
            await using (var command = connection.CreateCommand())
            {
                command.Transaction = transaction;
                command.CommandText = sql;
                await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            }

            await using (var versionCommand = connection.CreateCommand())
            {
                versionCommand.Transaction = transaction;
                // PRAGMA parametre kabul etmez; değer kaynak koddaki sabit tamsayıdır.
                versionCommand.CommandText = $"PRAGMA user_version = {version:D};";
                await versionCommand.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            }

            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        }

        logger.LogInformation("Veritabanı hazır: {Path} (şema v{Version})", connectionFactory.DatabasePath, LatestVersion);
    }

    internal static async Task<int> GetUserVersionAsync(SqliteConnection connection, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = "PRAGMA user_version;";
        var result = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
        return Convert.ToInt32(result, System.Globalization.CultureInfo.InvariantCulture);
    }
}
