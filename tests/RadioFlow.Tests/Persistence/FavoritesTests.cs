using Microsoft.Extensions.Logging.Abstractions;
using RadioFlow.Core.Models;
using RadioFlow.Infrastructure.Persistence;
using RadioFlow.Infrastructure.Security;
using RadioFlow.Infrastructure.Services;
using RadioFlow.Tests.Fakes;

namespace RadioFlow.Tests.Persistence;

public sealed class FavoritesTests
{
    private static LibraryService CreateLibrary(TestDatabase db) => new(
        new StationRepository(db.Factory),
        new FavoritesRepository(db.Factory),
        new HistoryRepository(db.Factory),
        new StreamUrlValidator(),
        NullLogger<LibraryService>.Instance);

    private static RadioStation Station(string id, string name) => new()
    {
        ExternalId = id,
        Name = name,
        StreamUrl = $"https://{id}.example.com/live",
        CountryCode = "TR",
        Tags = "pop,türkçe",
    };

    // Test 1: Favori ekleme
    [Fact]
    public async Task ToggleFavorite_AddsStationToFavorites()
    {
        await using var db = await TestDatabase.CreateAsync();
        var library = CreateLibrary(db);
        var changed = 0;
        library.FavoritesChanged += (_, _) => changed++;

        var isFavorite = await library.ToggleFavoriteAsync(Station("a1", "Radyo A"), TestContext.Current.CancellationToken);

        Assert.True(isFavorite);
        var favorites = await library.GetFavoritesAsync(TestContext.Current.CancellationToken);
        var single = Assert.Single(favorites);
        Assert.Equal("Radyo A", single.Name);
        Assert.NotEqual(0, single.Id);
        Assert.True(await library.IsFavoriteAsync(Station("a1", "Radyo A"), TestContext.Current.CancellationToken));
        Assert.Equal(1, changed);
    }

    [Fact]
    public async Task AddFavorite_Twice_DoesNotDuplicate()
    {
        await using var db = await TestDatabase.CreateAsync();
        var stations = new StationRepository(db.Factory);
        var favorites = new FavoritesRepository(db.Factory);
        var saved = await stations.UpsertAsync(Station("a1", "Radyo A"), TestContext.Current.CancellationToken);

        Assert.True(await favorites.AddAsync(saved.Id, TestContext.Current.CancellationToken));
        Assert.False(await favorites.AddAsync(saved.Id, TestContext.Current.CancellationToken));
        Assert.Single(await favorites.GetAllAsync(TestContext.Current.CancellationToken));
    }

    // Test 2: Favori kaldırma
    [Fact]
    public async Task ToggleFavorite_Twice_RemovesStationFromFavorites()
    {
        await using var db = await TestDatabase.CreateAsync();
        var library = CreateLibrary(db);
        var station = Station("a1", "Radyo A");

        await library.ToggleFavoriteAsync(station, TestContext.Current.CancellationToken);
        var isFavorite = await library.ToggleFavoriteAsync(station, TestContext.Current.CancellationToken);

        Assert.False(isFavorite);
        Assert.Empty(await library.GetFavoritesAsync(TestContext.Current.CancellationToken));
        // İstasyon önbellekte kalır, yalnızca favori kaydı silinir.
        Assert.NotNull(await new StationRepository(db.Factory).GetByExternalIdAsync("a1", TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task MoveFavorite_ChangesOrder_AndAdjacentNavigationWraps()
    {
        await using var db = await TestDatabase.CreateAsync();
        var library = CreateLibrary(db);
        var ct = TestContext.Current.CancellationToken;
        await library.ToggleFavoriteAsync(Station("a", "A"), ct);
        await library.ToggleFavoriteAsync(Station("b", "B"), ct);
        await library.ToggleFavoriteAsync(Station("c", "C"), ct);

        var c = (await library.GetFavoritesAsync(ct))[2];
        await library.MoveFavoriteAsync(c, -2, ct);

        var order = (await library.GetFavoritesAsync(ct)).Select(s => s.Name).ToArray();
        Assert.Equal(new[] { "C", "A", "B" }, order);

        var favorites = await library.GetFavoritesAsync(ct);
        Assert.Equal("A", (await library.GetAdjacentFavoriteAsync(favorites[0], +1, ct))!.Name);
        Assert.Equal("B", (await library.GetAdjacentFavoriteAsync(favorites[0], -1, ct))!.Name); // başa sarar
        Assert.Equal("C", (await library.GetAdjacentFavoriteAsync(favorites[2], +1, ct))!.Name);
        Assert.Equal("C", (await library.GetAdjacentFavoriteAsync(null, +1, ct))!.Name);
    }

    [Fact]
    public async Task AddCustomStation_ValidatesUrl_AndAddsToFavorites()
    {
        await using var db = await TestDatabase.CreateAsync();
        var library = CreateLibrary(db);
        var ct = TestContext.Current.CancellationToken;

        await Assert.ThrowsAsync<ArgumentException>(() => library.AddCustomStationAsync("Kötü", "file:///c:/a.mp3", null, null, ct));

        var custom = await library.AddCustomStationAsync("Ev Radyosu", "http://192.168.1.20:8000/stream", null, "yerel", ct);
        Assert.True(custom.IsCustom);
        Assert.Contains(await library.GetFavoritesAsync(ct), s => s.Id == custom.Id);

        Assert.True(await library.DeleteCustomStationAsync(custom, ct));
        Assert.Empty(await library.GetFavoritesAsync(ct)); // ON DELETE CASCADE
    }

    [Fact]
    public async Task CachedSearch_IsTurkishAware()
    {
        await using var db = await TestDatabase.CreateAsync();
        var repo = new StationRepository(db.Factory);
        var ct = TestContext.Current.CancellationToken;
        await repo.UpsertManyAsync(
        [
            Station("1", "İSTANBUL FM"),
            Station("2", "Şarkı Radyo"),
            Station("3", "Ankara 100%"),
        ], ct);

        Assert.Single(await repo.SearchCachedAsync(new StationQuery { Name = "istanbul" }, ct));
        Assert.Single(await repo.SearchCachedAsync(new StationQuery { Name = "ISTANBUL" }, ct));
        Assert.Single(await repo.SearchCachedAsync(new StationQuery { Name = "sarki" }, ct));
        Assert.Single(await repo.SearchCachedAsync(new StationQuery { Name = "100%" }, ct));
        Assert.Equal(3, (await repo.SearchCachedAsync(new StationQuery { Tag = "Türkçe" }, ct)).Count);
    }

    [Fact]
    public async Task History_RecordsRecentStationsInOrder()
    {
        await using var db = await TestDatabase.CreateAsync();
        var repo = new StationRepository(db.Factory);
        var history = new HistoryRepository(db.Factory);
        var ct = TestContext.Current.CancellationToken;
        var a = await repo.UpsertAsync(Station("a", "A"), ct);
        var b = await repo.UpsertAsync(Station("b", "B"), ct);
        var now = DateTimeOffset.UtcNow;

        var h1 = await history.StartAsync(a.Id, now.AddMinutes(-10), ct);
        await history.EndAsync(h1, 120, ct);
        await history.StartAsync(b.Id, now.AddMinutes(-5), ct);
        await history.StartAsync(a.Id, now, ct);

        var recent = await history.GetRecentStationsAsync(10, ct);
        Assert.Equal(new[] { "A", "B" }, recent.Select(s => s.Name).ToArray());
    }
}
