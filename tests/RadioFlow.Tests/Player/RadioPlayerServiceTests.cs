using Microsoft.Extensions.Logging.Abstractions;
using RadioFlow.Core.Enums;
using RadioFlow.Core.Models;
using RadioFlow.Infrastructure.Audio;
using RadioFlow.Infrastructure.Security;
using RadioFlow.Tests.Fakes;

namespace RadioFlow.Tests.Player;

public sealed class RadioPlayerServiceTests
{
    private static readonly RadioStation StationA = new() { Name = "A FM", StreamUrl = "https://a.example.com/live" };
    private static readonly RadioStation StationB = new() { Name = "B FM", StreamUrl = "https://b.example.com/live" };

    private static (RadioPlayerService Player, FakeAudioEngine Engine, StateRecorder Recorder) Create(PlaybackOptions? options = null)
    {
        var engine = new FakeAudioEngine();
        var opts = options ?? new PlaybackOptions
        {
            MaxReconnectAttempts = 3,
            InitialReconnectDelay = TimeSpan.FromMilliseconds(10),
            MaxReconnectDelay = TimeSpan.FromMilliseconds(40),
            ConnectTimeout = TimeSpan.FromSeconds(2),
        };
        var player = new RadioPlayerService(
            engine,
            new PassThroughPlaylistResolver(),
            new StreamUrlValidator(),
            () => opts,
            NullLogger<RadioPlayerService>.Instance);
        return (player, engine, new StateRecorder(player));
    }

    // Test 7: Player state geçişleri
    [Fact]
    public async Task Play_Pause_Resume_Stop_FollowsExpectedStateTransitions()
    {
        var (player, engine, recorder) = Create();

        await player.PlayAsync(StationA, TestContext.Current.CancellationToken);
        Assert.Equal(PlayerState.Playing, player.CurrentState);
        Assert.Equal(StationA, player.CurrentStation);

        await player.PauseAsync();
        Assert.Equal(PlayerState.Paused, player.CurrentState);

        await player.ResumeAsync();
        Assert.Equal(PlayerState.Playing, player.CurrentState);
        Assert.Equal(1, engine.PlayCount); // Motor içinde devam edildi, yeniden bağlanılmadı.

        await player.StopAsync();
        Assert.Equal(PlayerState.Stopped, player.CurrentState);

        Assert.Equal(
            new[] { PlayerState.Connecting, PlayerState.Buffering, PlayerState.Playing, PlayerState.Paused, PlayerState.Playing, PlayerState.Stopped },
            recorder.States.ToArray());
    }

    [Fact]
    public async Task Pause_WhenStreamCannotPause_StopsAndReconnectsOnResume()
    {
        var (player, engine, _) = Create();
        engine.CanPause = false;

        await player.PlayAsync(StationA, TestContext.Current.CancellationToken);
        await player.PauseAsync();

        Assert.Equal(PlayerState.Paused, player.CurrentState);
        Assert.NotNull(player.LastErrorMessage); // Kullanıcıya açıklama gösterilir.
        Assert.True(engine.StopCount >= 1);

        await player.ResumeAsync();
        Assert.Equal(PlayerState.Playing, player.CurrentState);
        Assert.Equal(2, engine.PlayCount);
    }

    // Test 8: Reconnect retry sınırı
    [Fact]
    public async Task Play_WhenStreamAlwaysFails_StopsAfterMaxAttemptsWithError()
    {
        var (player, engine, recorder) = Create();
        engine.Mode = FakeAudioEngine.Behavior.Fail;

        await player.PlayAsync(StationA, TestContext.Current.CancellationToken);

        Assert.Equal(PlayerState.Error, player.CurrentState);
        Assert.Equal(1 + 3, engine.PlayCount); // ilk deneme + 3 yeniden deneme
        Assert.Equal(3, recorder.States.Count(s => s == PlayerState.Reconnecting));
        Assert.False(string.IsNullOrWhiteSpace(player.LastErrorMessage));
        Assert.DoesNotContain("Exception", player.LastErrorMessage, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Play_WithZeroRetries_FailsAfterSingleAttempt()
    {
        var (player, engine, _) = Create(new PlaybackOptions
        {
            MaxReconnectAttempts = 0,
            InitialReconnectDelay = TimeSpan.FromMilliseconds(10),
            ConnectTimeout = TimeSpan.FromSeconds(1),
        });
        engine.Mode = FakeAudioEngine.Behavior.Fail;

        await player.PlayAsync(StationA, TestContext.Current.CancellationToken);

        Assert.Equal(PlayerState.Error, player.CurrentState);
        Assert.Equal(1, engine.PlayCount);
    }

    [Fact]
    public async Task StreamDrop_WhilePlaying_ReconnectsAutomatically()
    {
        var (player, engine, recorder) = Create();
        await player.PlayAsync(StationA, TestContext.Current.CancellationToken);

        engine.RaiseForCurrent(AudioEngineEventKind.EndReached);

        await recorder.WaitForAsync(PlayerState.Playing);
        Assert.Contains(PlayerState.Reconnecting, recorder.States);
        Assert.Equal(2, engine.PlayCount);
    }

    [Fact]
    public async Task Stop_DuringReconnect_CancelsRetries()
    {
        var (player, engine, recorder) = Create(new PlaybackOptions
        {
            MaxReconnectAttempts = 5,
            InitialReconnectDelay = TimeSpan.FromMilliseconds(300),
            MaxReconnectDelay = TimeSpan.FromSeconds(1),
            ConnectTimeout = TimeSpan.FromSeconds(1),
        });
        await player.PlayAsync(StationA, TestContext.Current.CancellationToken);
        engine.Mode = FakeAudioEngine.Behavior.Fail;

        engine.RaiseForCurrent(AudioEngineEventKind.Error);
        await recorder.WaitForAsync(PlayerState.Reconnecting);
        await player.StopAsync();
        var playsAtStop = engine.PlayCount;

        await Task.Delay(800, TestContext.Current.CancellationToken);
        Assert.Equal(PlayerState.Stopped, player.CurrentState);
        Assert.Equal(playsAtStop, engine.PlayCount);
    }

    // Test 9: İstasyon değiştirilince önceki işlemin iptali
    [Fact]
    public async Task Play_NewStation_CancelsPendingConnectionOfPreviousStation()
    {
        var (player, engine, recorder) = Create(new PlaybackOptions
        {
            MaxReconnectAttempts = 3,
            InitialReconnectDelay = TimeSpan.FromMilliseconds(10),
            ConnectTimeout = TimeSpan.FromSeconds(10),
        });
        engine.BehaviorSelector = uri => uri.Host.StartsWith("a.", StringComparison.Ordinal)
            ? FakeAudioEngine.Behavior.Hang
            : FakeAudioEngine.Behavior.Succeed;

        var first = player.PlayAsync(StationA, TestContext.Current.CancellationToken);
        await recorder.WaitForAsync(PlayerState.Connecting);

        await player.PlayAsync(StationB, TestContext.Current.CancellationToken);

        // Önceki işlem 10 sn zaman aşımını beklemeden iptal edilmiş olmalı.
        await first.WaitAsync(TimeSpan.FromSeconds(2), TestContext.Current.CancellationToken);

        Assert.Equal(PlayerState.Playing, player.CurrentState);
        Assert.Equal(StationB, player.CurrentStation);
        Assert.Equal(new Uri(StationB.StreamUrl), engine.PlayedUris[^1]);
        Assert.DoesNotContain(PlayerState.Error, recorder.States);
        Assert.DoesNotContain(PlayerState.Reconnecting, recorder.States);
    }

    [Fact]
    public async Task ConcurrentPlayCalls_LastOneWins()
    {
        var (player, engine, _) = Create();

        var tasks = Enumerable.Range(0, 10)
            .Select(i => player.PlayAsync(new RadioStation { Name = $"S{i}", StreamUrl = $"https://s{i}.example.com/live" }))
            .ToArray();
        await Task.WhenAll(tasks).WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);

        Assert.Equal(PlayerState.Playing, player.CurrentState);
        Assert.Equal("S9", player.CurrentStation!.Name);
        Assert.True(engine.PlayCount <= 10);
    }

    // Test 6 (oynatıcı tarafı): Hatalı stream URL reddi
    [Theory]
    [InlineData("file:///C:/Windows/win.ini")]
    [InlineData("javascript:alert(1)")]
    [InlineData("not a url")]
    public async Task Play_WithInvalidUrl_IsRejectedWithoutTouchingEngine(string url)
    {
        var (player, engine, _) = Create();

        await player.PlayAsync(new RadioStation { Name = "Kötü", StreamUrl = url }, TestContext.Current.CancellationToken);

        Assert.Equal(PlayerState.Error, player.CurrentState);
        Assert.Equal(0, engine.PlayCount);
        Assert.NotNull(player.LastErrorMessage);
    }

    [Fact]
    public async Task Metadata_IsParsedAndPublished_AndNeverInvented()
    {
        var (player, engine, _) = Create();
        NowPlayingChangedEventArgs? last = null;
        player.NowPlayingChanged += (_, e) => last = e;

        await player.PlayAsync(StationA, TestContext.Current.CancellationToken);
        Assert.Null(player.NowPlaying); // Metadata gelmeden şarkı bilgisi yok.

        engine.RaiseForCurrent(AudioEngineEventKind.MetadataChanged, "Sezen Aksu - Gülümse");
        Assert.NotNull(last);
        Assert.Equal("Sezen Aksu", player.NowPlaying!.Artist);
        Assert.Equal("Gülümse", player.NowPlaying.Title);

        await player.StopAsync();
        Assert.Null(player.NowPlaying);
    }

    [Fact]
    public async Task Volume_IsClampedAndForwardedToEngine()
    {
        var (player, engine, _) = Create();
        player.SetVolume(150);
        Assert.Equal(100, player.Volume);
        player.SetVolume(-5);
        Assert.Equal(0, engine.Volume);
        player.SetMuted(true);
        Assert.True(engine.IsMuted);
        await Task.CompletedTask;
    }
}
