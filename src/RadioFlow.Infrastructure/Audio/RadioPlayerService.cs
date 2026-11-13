using Microsoft.Extensions.Logging;
using RadioFlow.Core.Enums;
using RadioFlow.Core.Interfaces;
using RadioFlow.Core.Models;

namespace RadioFlow.Infrastructure.Audio;

/// <summary>
/// Uygulamanın tek merkezi oynatıcısı. Durum makinesi, kontrollü yeniden bağlanma (artan gecikme +
/// deneme sınırı), istasyon değişiminde önceki işlemlerin iptali ve eşzamanlı Play yarışlarının
/// engellenmesi burada yönetilir. Gerçek ses çıkışı <see cref="IAudioEngine"/> (LibVLC) üzerindedir.
/// </summary>
public sealed class RadioPlayerService : IRadioPlayerService, IDisposable
{
    private const string ConnectFailedMessage =
        "Yayına bağlanılamadı. İstasyon şu anda yayında olmayabilir veya internet bağlantınızda sorun olabilir.";

    private readonly IAudioEngine _engine;
    private readonly IPlaylistResolver _playlistResolver;
    private readonly IStreamUrlValidator _validator;
    private readonly Func<PlaybackOptions> _optionsProvider;
    private readonly ILogger<RadioPlayerService> _logger;
    private readonly TimeProvider _timeProvider;

    // Motor komutlarını (Play/Stop/Pause) sıraya sokar; aynı anda iki Play'in yarışmasını engeller.
    private readonly SemaphoreSlim _engineGate = new(1, 1);

    // Aşağıdaki alanlar _sync ile korunur.
    private readonly Lock _sync = new();
    private PlaybackSession? _session;
    private PlayerState _state = PlayerState.Stopped;
    private RadioStation? _station;
    private NowPlayingInfo? _nowPlaying;
    private string? _lastError;
    private bool _pausedInEngine;
    private DateTimeOffset _pausedAt;

    private long _generationCounter;
    private int _volume = 70;
    private bool _muted;
    private bool _disposed;

    public RadioPlayerService(
        IAudioEngine engine,
        IPlaylistResolver playlistResolver,
        IStreamUrlValidator validator,
        Func<PlaybackOptions> optionsProvider,
        ILogger<RadioPlayerService> logger,
        TimeProvider? timeProvider = null)
    {
        _engine = engine;
        _playlistResolver = playlistResolver;
        _validator = validator;
        _optionsProvider = optionsProvider;
        _logger = logger;
        _timeProvider = timeProvider ?? TimeProvider.System;
        _engine.EngineEvent += OnEngineEvent;
    }

    public event EventHandler<PlayerStateChangedEventArgs>? StateChanged;

    public event EventHandler<NowPlayingChangedEventArgs>? NowPlayingChanged;

    public PlayerState CurrentState
    {
        get { lock (_sync) { return _state; } }
    }

    public RadioStation? CurrentStation
    {
        get { lock (_sync) { return _station; } }
    }

    public NowPlayingInfo? NowPlaying
    {
        get { lock (_sync) { return _nowPlaying; } }
    }

    public string? LastErrorMessage
    {
        get { lock (_sync) { return _lastError; } }
    }

    public int Volume => Volatile.Read(ref _volume);

    public bool IsMuted => Volatile.Read(ref _muted);

    public async Task PlayAsync(RadioStation station, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(station);
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (!_validator.TryValidate(station.StreamUrl, out var uri, out var error, allowPrivateHosts: station.IsCustom))
        {
            _logger.LogWarning("Geçersiz yayın adresi reddedildi. İstasyon={Station} Hata={Error}", station.Name, error);
            var previous = DetachSession();
            previous?.Cancel();
            await StopEngineAsync().ConfigureAwait(false);
            lock (_sync)
            {
                _station = station;
            }

            SetState(null, PlayerState.Error, error ?? "Geçersiz yayın adresi.", force: true);
            return;
        }

        var session = BeginSession(station);
        _logger.LogInformation("Oynatma isteği: {Station} ({Url})", station.Name, uri);

        using var registration = cancellationToken.Register(static s => ((PlaybackSession)s!).Cancel(), session);
        await RunConnectLoopAsync(session, uri!, startAttempt: 0).ConfigureAwait(false);
    }

    public async Task StopAsync()
    {
        var session = DetachSession();
        session?.Cancel();
        await StopEngineAsync().ConfigureAwait(false);
        lock (_sync)
        {
            _pausedInEngine = false;
        }

        SetState(null, PlayerState.Stopped, null, force: true);
        ClearNowPlaying();
        _logger.LogInformation("Oynatma durduruldu.");
    }

    public async Task PauseAsync()
    {
        PlaybackSession? session;
        lock (_sync)
        {
            session = _session;
            if (session is null || _state is not (PlayerState.Playing or PlayerState.Buffering))
            {
                return;
            }
        }

        bool pausedInEngine;
        await _engineGate.WaitAsync().ConfigureAwait(false);
        try
        {
            pausedInEngine = await Task.Run(_engine.TryPause).ConfigureAwait(false);
        }
        finally
        {
            _engineGate.Release();
        }

        string? message = null;
        if (!pausedInEngine)
        {
            // Canlı yayın duraklatmayı desteklemiyor: bağlantıyı kapat, devam edildiğinde yeniden bağlan.
            if (DetachSession() is { } detached)
            {
                detached.Cancel();
            }

            await StopEngineAsync().ConfigureAwait(false);
            message = "Canlı yayın duraklatılamıyor; yayın durduruldu. Devam ettirdiğinizde yeniden bağlanılacak.";
        }

        lock (_sync)
        {
            _pausedInEngine = pausedInEngine;
            _pausedAt = _timeProvider.GetUtcNow();
        }

        SetState(null, PlayerState.Paused, message, force: true);
    }

    public async Task ResumeAsync()
    {
        RadioStation? station;
        PlaybackSession? session;
        bool resumeInEngine;
        lock (_sync)
        {
            station = _station;
            session = _session;
            if (_state is PlayerState.Playing or PlayerState.Connecting or PlayerState.Buffering or PlayerState.Reconnecting)
            {
                return;
            }

            var threshold = _optionsProvider().LivePauseReconnectThreshold;
            resumeInEngine = _state == PlayerState.Paused
                && _pausedInEngine
                && session is { EndedWhilePaused: false }
                && _timeProvider.GetUtcNow() - _pausedAt < threshold;
            _pausedInEngine = false;
        }

        if (resumeInEngine && session is not null)
        {
            await _engineGate.WaitAsync().ConfigureAwait(false);
            try
            {
                await Task.Run(_engine.Resume).ConfigureAwait(false);
            }
            finally
            {
                _engineGate.Release();
            }

            SetState(session, PlayerState.Playing, null);
            return;
        }

        if (station is not null)
        {
            // Uzun duraklatma veya duraklatılamayan yayın: canlı yayına yeniden bağlan.
            await PlayAsync(station).ConfigureAwait(false);
        }
    }

    public void SetVolume(int volume)
    {
        var clamped = Math.Clamp(volume, 0, 100);
        Volatile.Write(ref _volume, clamped);
        _engine.Volume = clamped;
    }

    public void SetMuted(bool muted)
    {
        Volatile.Write(ref _muted, muted);
        _engine.IsMuted = muted;
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _engine.EngineEvent -= OnEngineEvent;
        DetachSession()?.Cancel();
    }

    private PlaybackSession BeginSession(RadioStation station)
    {
        PlaybackSession? previous;
        var session = new PlaybackSession(station);
        bool hadNowPlaying;
        lock (_sync)
        {
            previous = _session;
            _session = session;
            _station = station;
            _pausedInEngine = false;
            hadNowPlaying = _nowPlaying is not null;
            _nowPlaying = null;
            session.LoopActive = true;
        }

        if (previous is not null)
        {
            _logger.LogInformation("Önceki oynatma işlemi iptal edildi: {Station}", previous.Station.Name);
            previous.Cancel();
        }

        if (hadNowPlaying)
        {
            NowPlayingChanged?.Invoke(this, new NowPlayingChangedEventArgs(station, null));
        }

        return session;
    }

    private PlaybackSession? DetachSession()
    {
        lock (_sync)
        {
            var session = _session;
            _session = null;
            return session;
        }
    }

    private async Task RunConnectLoopAsync(PlaybackSession session, Uri uri, int startAttempt)
    {
        var options = _optionsProvider();
        var token = session.Token;
        var attempt = startAttempt;
        try
        {
            while (true)
            {
                token.ThrowIfCancellationRequested();

                if (attempt > 0)
                {
                    if (attempt > options.MaxReconnectAttempts)
                    {
                        _logger.LogError(
                            "Yeniden bağlanma deneme sınırına ulaşıldı ({Max}). İstasyon={Station}",
                            options.MaxReconnectAttempts,
                            session.Station.Name);
                        if (IsCurrent(session))
                        {
                            await StopEngineAsync().ConfigureAwait(false);
                        }

                        SetState(session, PlayerState.Error, ConnectFailedMessage);
                        return;
                    }

                    var delay = options.GetReconnectDelay(attempt);
                    SetState(
                        session,
                        PlayerState.Reconnecting,
                        $"Bağlantı koptu, yeniden bağlanılıyor ({attempt}/{options.MaxReconnectAttempts})…",
                        attempt);
                    _logger.LogWarning(
                        "Yeniden bağlanma denemesi {Attempt}/{Max}, {Delay} sonra. İstasyon={Station}",
                        attempt,
                        options.MaxReconnectAttempts,
                        delay,
                        session.Station.Name);
                    await Task.Delay(delay, _timeProvider, token).ConfigureAwait(false);
                }
                else
                {
                    SetState(session, PlayerState.Connecting, null);
                }

                bool connected;
                try
                {
                    connected = await TryConnectOnceAsync(session, uri, options.ConnectTimeout).ConfigureAwait(false);
                }
                catch (Exception ex) when (ex is not OperationCanceledException || !token.IsCancellationRequested)
                {
                    _logger.LogWarning(ex, "Bağlantı denemesi başarısız. İstasyon={Station}", session.Station.Name);
                    connected = false;
                }

                if (connected)
                {
                    lock (_sync)
                    {
                        session.LoopActive = false;
                    }

                    SetState(session, PlayerState.Playing, null);
                    _logger.LogInformation("Çalıyor: {Station}", session.Station.Name);
                    return;
                }

                attempt++;
            }
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
            _logger.LogDebug("Bağlantı döngüsü iptal edildi: {Station}", session.Station.Name);
        }
        finally
        {
            lock (_sync)
            {
                session.LoopActive = false;
            }
        }
    }

    private async Task<bool> TryConnectOnceAsync(PlaybackSession session, Uri uri, TimeSpan connectTimeout)
    {
        var token = session.Token;
        var resolved = await _playlistResolver.ResolveAsync(uri, token).ConfigureAwait(false);
        var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

        await _engineGate.WaitAsync(token).ConfigureAwait(false);
        try
        {
            token.ThrowIfCancellationRequested();
            var generation = Interlocked.Increment(ref _generationCounter);
            session.Generation = generation;
            session.ConnectTcs = tcs;
            var volume = Volume;
            var muted = IsMuted;
            await Task.Run(
                () =>
                {
                    _engine.Volume = volume;
                    _engine.IsMuted = muted;
                    _engine.Play(resolved, generation);
                },
                CancellationToken.None).ConfigureAwait(false);
        }
        finally
        {
            _engineGate.Release();
        }

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(token);
        timeoutCts.CancelAfter(connectTimeout);
        try
        {
            return await tcs.Task.WaitAsync(timeoutCts.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (!token.IsCancellationRequested)
        {
            _logger.LogWarning("Bağlantı zaman aşımı ({Timeout}). İstasyon={Station}", connectTimeout, session.Station.Name);
            return false;
        }
        finally
        {
            session.ConnectTcs = null;
        }
    }

    private void OnEngineEvent(object? sender, AudioEngineEvent e)
    {
        PlaybackSession? session;
        lock (_sync)
        {
            session = _session;
        }

        if (session is null || session.IsCancelled || e.Generation != session.Generation)
        {
            return; // Eski veya iptal edilmiş yayına ait gecikmiş olay.
        }

        switch (e.Kind)
        {
            case AudioEngineEventKind.Buffering:
                if (e.BufferPercent < 100f && session.ConnectTcs is not null)
                {
                    SetState(session, PlayerState.Buffering, null);
                }

                break;

            case AudioEngineEventKind.Playing:
                if (session.ConnectTcs is { } connectTcs)
                {
                    connectTcs.TrySetResult(true);
                }
                else
                {
                    SetState(session, PlayerState.Playing, null);
                }

                break;

            case AudioEngineEventKind.Error:
            case AudioEngineEventKind.EndReached:
                _logger.LogWarning("Yayın olayı: {Kind} {Error}. İstasyon={Station}", e.Kind, e.ErrorMessage, session.Station.Name);
                if (session.ConnectTcs is { } failedTcs)
                {
                    failedTcs.TrySetResult(false);
                    break;
                }

                HandleStreamDrop(session);
                break;

            case AudioEngineEventKind.MetadataChanged:
                UpdateNowPlaying(session, NowPlayingInfo.Parse(e.Metadata));
                break;
        }
    }

    private void HandleStreamDrop(PlaybackSession session)
    {
        Uri? uri = null;
        lock (_sync)
        {
            if (_session != session)
            {
                return;
            }

            if (_state == PlayerState.Paused)
            {
                session.EndedWhilePaused = true;
                return;
            }

            if (session.LoopActive || _state is not (PlayerState.Playing or PlayerState.Buffering))
            {
                return;
            }

            if (_validator.TryValidate(session.Station.StreamUrl, out var parsed, out _, session.Station.IsCustom))
            {
                session.LoopActive = true;
                uri = parsed;
            }
        }

        if (uri is not null)
        {
            _ = RunConnectLoopAsync(session, uri, startAttempt: 1);
        }
    }

    private void UpdateNowPlaying(PlaybackSession session, NowPlayingInfo? info)
    {
        lock (_sync)
        {
            if (_session != session || Equals(_nowPlaying, info))
            {
                return;
            }

            _nowPlaying = info;
        }

        _logger.LogDebug("Şarkı bilgisi: {NowPlaying}", info?.RawText);
        NowPlayingChanged?.Invoke(this, new NowPlayingChangedEventArgs(session.Station, info));
    }

    private void ClearNowPlaying()
    {
        RadioStation? station;
        lock (_sync)
        {
            if (_nowPlaying is null)
            {
                return;
            }

            _nowPlaying = null;
            station = _station;
        }

        NowPlayingChanged?.Invoke(this, new NowPlayingChangedEventArgs(station, null));
    }

    private bool IsCurrent(PlaybackSession session)
    {
        lock (_sync)
        {
            return _session == session;
        }
    }

    private async Task StopEngineAsync()
    {
        await _engineGate.WaitAsync().ConfigureAwait(false);
        try
        {
            await Task.Run(_engine.Stop).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Ses motoru durdurulurken hata oluştu.");
        }
        finally
        {
            _engineGate.Release();
        }
    }

    /// <summary>
    /// Durumu değiştirir. <paramref name="session"/> verilirse yalnızca hâlâ güncel oturumsa uygulanır.
    /// </summary>
    private void SetState(PlaybackSession? session, PlayerState newState, string? message, int attempt = 0, bool force = false)
    {
        PlayerState oldState;
        RadioStation? station;
        lock (_sync)
        {
            if (!force && (session is null || _session != session || session.IsCancelled))
            {
                return;
            }

            oldState = _state;
            if (oldState == newState && newState != PlayerState.Reconnecting && message == _lastError)
            {
                return;
            }

            _state = newState;
            _lastError = newState is PlayerState.Error or PlayerState.Paused ? message : null;
            station = _station;
        }

        _logger.LogInformation("Oynatıcı durumu: {Old} → {New} ({Station})", oldState, newState, station?.Name);
        StateChanged?.Invoke(this, new PlayerStateChangedEventArgs(oldState, newState, station, message, attempt));
    }

    private sealed class PlaybackSession(RadioStation station)
    {
        private readonly CancellationTokenSource _cts = new();
        private TaskCompletionSource<bool>? _connectTcs;
        private long _generation;

        public RadioStation Station { get; } = station;

        public CancellationToken Token => _cts.Token;

        public bool IsCancelled => _cts.IsCancellationRequested;

        /// <summary>Bir bağlantı/yeniden bağlanma döngüsü çalışıyor mu (yalnızca _sync altında okunur/yazılır).</summary>
        public bool LoopActive { get; set; }

        public bool EndedWhilePaused { get; set; }

        public long Generation
        {
            get => Interlocked.Read(ref _generation);
            set => Interlocked.Exchange(ref _generation, value);
        }

        public TaskCompletionSource<bool>? ConnectTcs
        {
            get => Volatile.Read(ref _connectTcs);
            set => Volatile.Write(ref _connectTcs, value);
        }

        public void Cancel()
        {
            try
            {
                _cts.Cancel();
            }
            catch (ObjectDisposedException)
            {
                // Yoksay.
            }
        }
    }
}
