using Microsoft.Data.Sqlite;
using RadioFlow.Core.Interfaces;
using RadioFlow.Core.Models;
using RadioFlow.Core.Text;
using static RadioFlow.Infrastructure.Persistence.StationMapping;

namespace RadioFlow.Infrastructure.Persistence;

public sealed class StationRepository(SqliteConnectionFactory connectionFactory) : IStationRepository
{
    public async Task<RadioStation> UpsertAsync(RadioStation station, CancellationToken cancellationToken = default)
    {
        await using var connection = await connectionFactory.OpenAsync(cancellationToken).ConfigureAwait(false);
        var id = await UpsertCoreAsync(connection, null, station, cancellationToken).ConfigureAwait(false);
        return station with { Id = id };
    }

    public async Task UpsertManyAsync(IEnumerable<RadioStation> stations, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(stations);
        await using var connection = await connectionFactory.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var transaction = (SqliteTransaction)await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        foreach (var station in stations)
        {
            await UpsertCoreAsync(connection, transaction, station, cancellationToken).ConfigureAwait(false);
        }

        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<RadioStation?> GetByIdAsync(long id, CancellationToken cancellationToken = default)
    {
        await using var connection = await connectionFactory.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        command.CommandText = $"SELECT {Columns} FROM RadioStations s WHERE s.Id = $id;";
        command.Parameters.AddWithValue("$id", id);
        var list = await ReadAllAsync(command, cancellationToken).ConfigureAwait(false);
        return list.Count > 0 ? list[0] : null;
    }

    public async Task<RadioStation?> GetByExternalIdAsync(string externalId, CancellationToken cancellationToken = default)
    {
        await using var connection = await connectionFactory.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        command.CommandText = $"SELECT {Columns} FROM RadioStations s WHERE s.ExternalId = $ext;";
        command.Parameters.AddWithValue("$ext", externalId);
        var list = await ReadAllAsync(command, cancellationToken).ConfigureAwait(false);
        return list.Count > 0 ? list[0] : null;
    }

    public async Task<IReadOnlyList<RadioStation>> SearchCachedAsync(StationQuery query, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        await using var connection = await connectionFactory.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var command = connection.CreateCommand();

        var where = new List<string>();
        if (!string.IsNullOrWhiteSpace(query.Name))
        {
            where.Add("(s.NameFolded LIKE $name ESCAPE '\\' OR s.TagsFolded LIKE $name ESCAPE '\\')");
            command.Parameters.AddWithValue("$name", "%" + EscapeLike(TextFolding.Fold(query.Name.Trim())) + "%");
        }

        if (!string.IsNullOrWhiteSpace(query.CountryCode))
        {
            where.Add("s.CountryCode = $cc");
            command.Parameters.AddWithValue("$cc", query.CountryCode.Trim().ToUpperInvariant());
        }

        if (!string.IsNullOrWhiteSpace(query.Tag))
        {
            where.Add("(',' || s.TagsFolded || ',') LIKE $tag ESCAPE '\\'");
            command.Parameters.AddWithValue("$tag", "%," + EscapeLike(TextFolding.Fold(query.Tag.Trim())) + ",%");
        }

        var whereSql = where.Count > 0 ? "WHERE " + string.Join(" AND ", where) : string.Empty;
        command.CommandText = $"SELECT {Columns} FROM RadioStations s {whereSql} ORDER BY s.Name COLLATE NOCASE LIMIT $limit OFFSET $offset;";
        command.Parameters.AddWithValue("$limit", Math.Clamp(query.Limit, 1, 1000));
        command.Parameters.AddWithValue("$offset", Math.Max(0, query.Offset));
        return await ReadAllAsync(command, cancellationToken).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<RadioStation>> GetCustomStationsAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = await connectionFactory.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        command.CommandText = $"SELECT {Columns} FROM RadioStations s WHERE s.IsCustom = 1 ORDER BY s.Name COLLATE NOCASE;";
        return await ReadAllAsync(command, cancellationToken).ConfigureAwait(false);
    }

    public async Task<bool> DeleteCustomAsync(long id, CancellationToken cancellationToken = default)
    {
        await using var connection = await connectionFactory.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM RadioStations WHERE Id = $id AND IsCustom = 1;";
        command.Parameters.AddWithValue("$id", id);
        return await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false) > 0;
    }

    internal static string EscapeLike(string value) =>
        value.Replace("\\", "\\\\", StringComparison.Ordinal)
             .Replace("%", "\\%", StringComparison.Ordinal)
             .Replace("_", "\\_", StringComparison.Ordinal);

    private static async Task<long> UpsertCoreAsync(
        SqliteConnection connection,
        SqliteTransaction? transaction,
        RadioStation station,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;

        var now = FormatDate(DateTimeOffset.UtcNow);
        command.Parameters.AddWithValue("$ext", Db(station.ExternalId));
        command.Parameters.AddWithValue("$name", station.Name);
        command.Parameters.AddWithValue("$nameFolded", TextFolding.Fold(station.Name));
        command.Parameters.AddWithValue("$country", Db(station.Country));
        command.Parameters.AddWithValue("$cc", Db(station.CountryCode));
        command.Parameters.AddWithValue("$lang", Db(station.Language));
        command.Parameters.AddWithValue("$tags", Db(station.Tags));
        command.Parameters.AddWithValue("$tagsFolded", TextFolding.Fold(station.Tags).Replace(", ", ",", StringComparison.Ordinal));
        command.Parameters.AddWithValue("$logo", Db(station.LogoUrl));
        command.Parameters.AddWithValue("$home", Db(station.Homepage));
        command.Parameters.AddWithValue("$stream", station.StreamUrl);
        command.Parameters.AddWithValue("$codec", Db(station.Codec));
        command.Parameters.AddWithValue("$bitrate", station.Bitrate);
        command.Parameters.AddWithValue("$custom", station.IsCustom ? 1 : 0);
        command.Parameters.AddWithValue("$created", FormatDate(station.CreatedAt));
        command.Parameters.AddWithValue("$now", now);

        const string setClause = """
            Name = $name, NameFolded = $nameFolded, Country = $country, CountryCode = $cc, Language = $lang,
            Tags = $tags, TagsFolded = $tagsFolded, LogoUrl = $logo, Homepage = $home, StreamUrl = $stream,
            Codec = $codec, Bitrate = $bitrate, UpdatedAt = $now
            """;

        if (station.Id != 0)
        {
            command.Parameters.AddWithValue("$id", station.Id);
            command.CommandText = $"UPDATE RadioStations SET {setClause}, IsCustom = $custom WHERE Id = $id;";
            if (await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false) > 0)
            {
                return station.Id;
            }

            command.Parameters.RemoveAt("$id");
        }

        const string insertColumns = """
            INSERT INTO RadioStations
                (ExternalId, Name, NameFolded, Country, CountryCode, Language, Tags, TagsFolded, LogoUrl, Homepage,
                 StreamUrl, Codec, Bitrate, IsCustom, CreatedAt, UpdatedAt)
            VALUES
                ($ext, $name, $nameFolded, $country, $cc, $lang, $tags, $tagsFolded, $logo, $home,
                 $stream, $codec, $bitrate, $custom, $created, $now)
            """;

        command.CommandText = station.ExternalId is null
            ? insertColumns + " RETURNING Id;"
            : insertColumns + $" ON CONFLICT(ExternalId) DO UPDATE SET {setClause} RETURNING Id;";

        var result = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
        return Convert.ToInt64(result, System.Globalization.CultureInfo.InvariantCulture);
    }
}
