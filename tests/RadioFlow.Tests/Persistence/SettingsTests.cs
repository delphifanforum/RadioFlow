using Microsoft.Extensions.Logging.Abstractions;
using RadioFlow.Core.Enums;
using RadioFlow.Infrastructure.Persistence;
using RadioFlow.Infrastructure.Services;
using RadioFlow.Tests.Fakes;

namespace RadioFlow.Tests.Persistence;

public sealed class SettingsTests
{
    private static SettingsService Create(TestDatabase db) =>
        new(new SettingsStore(db.Factory), NullLogger<SettingsService>.Instance);

    // Test 3: Ayarların kaydedilmesi
    [Fact]
    public async Task Update_PersistsValuesToSettingsTable()
    {
        await using var db = await TestDatabase.CreateAsync();
        var service = Create(db);
        var ct = TestContext.Current.CancellationToken;
        await service.LoadAsync(ct);
        var raised = false;
        service.Changed += (_, _) => raised = true;

        await service.UpdateAsync(s =>
        {
            s.Theme = AppTheme.Light;
            s.AlwaysOnTop = true;
            s.DefaultVolume = 42;
            s.WidgetLeft = 123.5;
            s.LastStationId = 7;
        }, ct);

        Assert.True(raised);
        var rows = await new SettingsStore(db.Factory).GetAllAsync(ct);
        Assert.Equal("\"Light\"", rows["app.Theme"]);
        Assert.Equal("true", rows["app.AlwaysOnTop"]);
        Assert.Equal("42", rows["app.DefaultVolume"]);
    }

    // Test 4: Ayarların tekrar yüklenmesi
    [Fact]
    public async Task Load_InNewInstance_RestoresSavedValues()
    {
        await using var db = await TestDatabase.CreateAsync();
        var ct = TestContext.Current.CancellationToken;
        var first = Create(db);
        await first.LoadAsync(ct);
        await first.UpdateAsync(s =>
        {
            s.Theme = AppTheme.Light;
            s.WidgetOpacity = 0.6;
            s.WidgetSize = WidgetSize.Large;
            s.MinimizeToTray = false;
            s.WidgetLeft = 321;
            s.WidgetTop = null;
            s.LastStationId = 99;
        }, ct);

        var second = Create(db);
        var loaded = await second.LoadAsync(ct);

        Assert.Equal(AppTheme.Light, loaded.Theme);
        Assert.Equal(0.6, loaded.WidgetOpacity, 3);
        Assert.Equal(WidgetSize.Large, loaded.WidgetSize);
        Assert.False(loaded.MinimizeToTray);
        Assert.Equal(321, loaded.WidgetLeft);
        Assert.Null(loaded.WidgetTop);
        Assert.Equal(99, loaded.LastStationId);
    }

    [Fact]
    public async Task Load_WithCorruptOrOutOfRangeValues_FallsBackPerKey()
    {
        await using var db = await TestDatabase.CreateAsync();
        var ct = TestContext.Current.CancellationToken;
        await new SettingsStore(db.Factory).SetManyAsync(new Dictionary<string, string>
        {
            ["app.Theme"] = "\"NoSuchTheme\"",
            ["app.DefaultVolume"] = "500",
            ["app.AlwaysOnTop"] = "true",
            ["app.Unknown"] = "{}",
        }, ct);

        var loaded = await Create(db).LoadAsync(ct);

        Assert.Equal(AppTheme.Dark, loaded.Theme);       // bozuk değer → varsayılan
        Assert.Equal(100, loaded.DefaultVolume);         // aralığa sıkıştırıldı
        Assert.True(loaded.AlwaysOnTop);                 // geçerli değer korundu
    }

    [Fact]
    public async Task Load_OnEmptyDatabase_ReturnsDefaults()
    {
        await using var db = await TestDatabase.CreateAsync();
        var loaded = await Create(db).LoadAsync(TestContext.Current.CancellationToken);
        Assert.Equal(AppTheme.Dark, loaded.Theme);
        Assert.True(loaded.ShowWidgetOnStartup);
        Assert.False(loaded.RunAtStartup); // Başlangıçta çalıştırma varsayılan olarak kapalı.
        Assert.False(loaded.AutoHideWidgetControls); // RadioFlow 2.0: varsayılan olarak kapalı.
    }

    // RadioFlow 2.0: "Kontrolleri otomatik gizle" ayarı kaydedilip yeniden yüklenebilmeli.
    [Fact]
    public async Task Update_PersistsAutoHideWidgetControls_AcrossReload()
    {
        await using var db = await TestDatabase.CreateAsync();
        var ct = TestContext.Current.CancellationToken;
        var first = Create(db);
        await first.LoadAsync(ct);
        await first.UpdateAsync(s => s.AutoHideWidgetControls = true, ct);

        var second = Create(db);
        var loaded = await second.LoadAsync(ct);

        Assert.True(loaded.AutoHideWidgetControls);
    }

    // RadioFlow 2.0: widget opaklığı artık %50-%100 aralığına sıkıştırılır.
    [Fact]
    public void Normalize_ClampsWidgetOpacity_ToFiftyToHundredPercent()
    {
        var tooLow = new RadioFlow.Core.Models.AppSettings { WidgetOpacity = 0.1 };
        tooLow.Normalize();
        Assert.Equal(0.5, tooLow.WidgetOpacity, 3);

        var tooHigh = new RadioFlow.Core.Models.AppSettings { WidgetOpacity = 1.5 };
        tooHigh.Normalize();
        Assert.Equal(1.0, tooHigh.WidgetOpacity, 3);
    }
}
