using Microsoft.Data.Sqlite;
using RadioFlow.Infrastructure.Persistence;
using RadioFlow.Tests.Fakes;

namespace RadioFlow.Tests.Persistence;

public sealed class DatabaseInitializerTests
{
    // Test 10: Veritabanı ilk oluşturma
    [Fact]
    public async Task Initialize_CreatesDatabaseFileTablesAndSchemaVersion()
    {
        await using var db = await TestDatabase.CreateAsync(initialize: false);
        Assert.False(File.Exists(db.Paths.DatabasePath));

        await db.CreateInitializer().InitializeAsync(TestContext.Current.CancellationToken);

        Assert.True(File.Exists(db.Paths.DatabasePath));
        await using var connection = await db.Factory.OpenAsync(TestContext.Current.CancellationToken);
        var tables = new List<string>();
        await using (var command = connection.CreateCommand())
        {
            command.CommandText = "SELECT name FROM sqlite_master WHERE type = 'table' ORDER BY name;";
            await using var reader = await command.ExecuteReaderAsync(TestContext.Current.CancellationToken);
            while (await reader.ReadAsync(TestContext.Current.CancellationToken))
            {
                tables.Add(reader.GetString(0));
            }
        }

        Assert.Contains("RadioStations", tables);
        Assert.Contains("Favorites", tables);
        Assert.Contains("ListeningHistory", tables);
        Assert.Contains("Settings", tables);
        Assert.Equal(DatabaseInitializer.LatestVersion, await DatabaseInitializer.GetUserVersionAsync(connection, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Initialize_IsIdempotentAndKeepsUserData()
    {
        await using var db = await TestDatabase.CreateAsync();
        var settings = new SettingsStore(db.Factory);
        await settings.SetManyAsync(new Dictionary<string, string> { ["k"] = "v" }, TestContext.Current.CancellationToken);

        await db.CreateInitializer().InitializeAsync(TestContext.Current.CancellationToken);

        var all = await settings.GetAllAsync(TestContext.Current.CancellationToken);
        Assert.Equal("v", all["k"]);
    }

    [Fact]
    public async Task Database_IsCreatedUnderGivenDataDirectory_NotAppDirectory()
    {
        await using var db = await TestDatabase.CreateAsync();
        Assert.StartsWith(db.Root, db.Paths.DatabasePath, StringComparison.Ordinal);
        Assert.StartsWith(db.Root, db.Paths.LogsDirectory, StringComparison.Ordinal);
        Assert.DoesNotContain(AppContext.BaseDirectory, db.Paths.DatabasePath, StringComparison.Ordinal);
        _ = new SqliteConnectionStringBuilder(); // derleme bağımlılığını doğrular
    }
}
