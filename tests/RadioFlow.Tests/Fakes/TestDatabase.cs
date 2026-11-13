using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging.Abstractions;
using RadioFlow.Infrastructure;
using RadioFlow.Infrastructure.Persistence;

namespace RadioFlow.Tests.Fakes;

/// <summary>Her test için geçici dizinde gerçek bir SQLite veritabanı.</summary>
internal sealed class TestDatabase : IAsyncDisposable
{
    private TestDatabase(string root)
    {
        Root = root;
        Paths = new AppPaths(root);
        Factory = new SqliteConnectionFactory(Paths.DatabasePath);
    }

    public string Root { get; }

    public AppPaths Paths { get; }

    public SqliteConnectionFactory Factory { get; }

    public static async Task<TestDatabase> CreateAsync(bool initialize = true)
    {
        var root = Path.Combine(Path.GetTempPath(), "RadioFlowTests", Guid.NewGuid().ToString("N"));
        var db = new TestDatabase(root);
        if (initialize)
        {
            await db.CreateInitializer().InitializeAsync();
        }

        return db;
    }

    public DatabaseInitializer CreateInitializer() => new(Factory, NullLogger<DatabaseInitializer>.Instance);

    public ValueTask DisposeAsync()
    {
        SqliteConnection.ClearAllPools();
        try
        {
            Directory.Delete(Root, recursive: true);
        }
        catch (IOException)
        {
            // Geçici dosya; silinemezse sorun değil.
        }

        return ValueTask.CompletedTask;
    }
}
