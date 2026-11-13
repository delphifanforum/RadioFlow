using System.Globalization;
using Microsoft.Data.Sqlite;
using RadioFlow.Core.Models;

namespace RadioFlow.Infrastructure.Persistence;

internal static class StationMapping
{
    public const string Columns =
        "s.Id, s.ExternalId, s.Name, s.Country, s.CountryCode, s.Language, s.Tags, s.LogoUrl, s.Homepage, s.StreamUrl, s.Codec, s.Bitrate, s.IsCustom, s.CreatedAt";

    public static RadioStation Read(SqliteDataReader r) => new()
    {
        Id = r.GetInt64(0),
        ExternalId = r.IsDBNull(1) ? null : r.GetString(1),
        Name = r.GetString(2),
        Country = r.IsDBNull(3) ? null : r.GetString(3),
        CountryCode = r.IsDBNull(4) ? null : r.GetString(4),
        Language = r.IsDBNull(5) ? null : r.GetString(5),
        Tags = r.IsDBNull(6) ? null : r.GetString(6),
        LogoUrl = r.IsDBNull(7) ? null : r.GetString(7),
        Homepage = r.IsDBNull(8) ? null : r.GetString(8),
        StreamUrl = r.GetString(9),
        Codec = r.IsDBNull(10) ? null : r.GetString(10),
        Bitrate = r.GetInt32(11),
        IsCustom = r.GetInt64(12) != 0,
        CreatedAt = ParseDate(r.GetString(13)),
    };

    public static string FormatDate(DateTimeOffset value) => value.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture);

    public static DateTimeOffset ParseDate(string value) =>
        DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var parsed)
            ? parsed
            : DateTimeOffset.MinValue;

    public static object Db(string? value) => value is null ? DBNull.Value : value;

    public static async Task<List<RadioStation>> ReadAllAsync(SqliteCommand command, CancellationToken cancellationToken)
    {
        var list = new List<RadioStation>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            list.Add(Read(reader));
        }

        return list;
    }
}
