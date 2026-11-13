using System.Threading.Channels;
using LibVLCSharp.Shared;
using Microsoft.Extensions.Logging;
using RadioFlow.Core.Enums;
using RadioFlow.Core.Interfaces;
using RadioFlow.Core.Models;

namespace RadioFlow.Infrastructure.Audio;

/// <summary>
/// LibVLC tabanlı ses motoru. Uygulama boyunca TEK bir <see cref="LibVLC"/> ve TEK bir
/// <see cref="MediaPlayer"/> örneği kullanılır.
/// <para>
/// LibVLC olay geri çağrıları içinden LibVLC API'si çağırmak kilitlenmeye yol açabildiği için tüm
/// olaylar sıralı bir <see cref="Channel{T}"/> kuyruğuna yazılır ve ayrı bir tüketici görevde
/// (polling olmadan, olay güdümlü) dışarıya iletilir.
/// </para>
/// </summary>
public sealed class LibVlcAudioEngine : IAudioEngine
{
    private readonly ILogger<LibVlcAudioEngine> _logger;
    private readonly Lock _sync = new();
    private readonly LibVLC _libVlc;
    private readonly MediaPlayer _player;
    private readonly Channel<QueuedEvent> _events =
        Channel.CreateUnbounded<QueuedEvent>(new UnboundedChannelOptions { SingleReader = true, SingleWriter = false });
    private readonly Task _pump;

    private MediaContext? _current;
    private int _volume = 70;
    private bool _muted;
    private bool _disposed;

    public LibVlcAudioEngine(ILogger<LibVlcAudioEngine> logger)
    {
        _logger = logger;

        // VideoLAN.LibVLC.Windows paketindeki yerel kütüphaneleri (libvlc\win-x64) yükler.
        LibVLCSharp.Shared.Core.Initialize();

        _libVlc = new LibVLC(
            "--no-video",
            "--no-lua",          // Lua betik eklentilerini kapat (gereksiz saldırı yüzeyi)
            "--ignore-config",   // Kullanıcının VLC ayarlarından etkilenme
            "--no-stats",
            "--no-sub-autodetect-file");
        _player = new MediaPlayer(_libVlc);
        _player.Buffering += OnBuffering;
        _player.EncounteredError += OnPlayerError;

        _pump = Task.Run(PumpEventsAsync);
        _logger.LogInformation("LibVLC başlatıldı. Sürüm: {Version}", _libVlc.Version);
    }

    public event EventHandler<AudioEngineEvent>? EngineEvent;

    public int Volume
    {
        get => Volatile.Read(ref _volume);
        set
        {
            var v = Math.Clamp(value, 0, 100);
            Volatile.Write(ref _volume, v);
            lock (_sync)
            {
                if (!_disposed)
                {
                    _player.Volume = v;
                }
            }
        }
    }

    public bool IsMuted
    {
        get => Volatile.Read(ref _muted);
        set
        {
            Volatile.Write(ref _muted, value);
            lock (_sync)
            {
                if (!_disposed)
                {
                    _player.Mute = value;
                }
            }
        }
    }

    public void Play(Uri streamUri, long generation)
    {
        ArgumentNullException.ThrowIfNull(streamUri);
        lock (_sync)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);

            var previous = _current;
            var media = new Media(
                _libVlc,
                streamUri,
                ":network-caching=2000",
                ":http-reconnect",
                ":no-video");
            var context = new MediaContext(media, generation, this);
            _current = context;

            _player.Play(media);

            previous?.Dispose();
        }
    }

    public void Stop()
    {
        lock (_sync)
        {
            if (_disposed)
            {
                return;
            }

            _player.Stop();
            _current?.Dispose();
            _current = null;
        }
    }

    public bool TryPause()
    {
        lock (_sync)
        {
            if (_disposed || !_player.CanPause)
            {
                return false;
            }

            _player.SetPause(true);
            return true;
        }
    }

    public void Resume()
    {
        lock (_sync)
        {
            if (!_disposed)
            {
                _player.SetPause(false);
            }
        }
    }

    public void Dispose()
    {
        lock (_sync)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _player.Buffering -= OnBuffering;
            _player.EncounteredError -= OnPlayerError;
            try
            {
                _player.Stop();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "LibVLC durdurulurken hata.");
            }

            _current?.Dispose();
            _current = null;
            _player.Dispose();
            _libVlc.Dispose();
        }

        _events.Writer.TryComplete();
        try
        {
            _pump.Wait(TimeSpan.FromSeconds(2));
        }
        catch (AggregateException)
        {
            // Yoksay.
        }

        _logger.LogInformation("LibVLC kaynakları serbest bırakıldı.");
    }

    private long CurrentGeneration
    {
        get
        {
            lock (_sync)
            {
                return _current?.Generation ?? -1;
            }
        }
    }

    private void Enqueue(QueuedEvent queued) => _events.Writer.TryWrite(queued);

    private void OnBuffering(object? sender, MediaPlayerBufferingEventArgs e) =>
        Enqueue(new QueuedEvent(new AudioEngineEvent(AudioEngineEventKind.Buffering, -1, e.Cache), null));

    private void OnPlayerError(object? sender, EventArgs e) =>
        Enqueue(new QueuedEvent(new AudioEngineEvent(AudioEngineEventKind.Error, -1, ErrorMessage: "LibVLC EncounteredError"), null));

    private async Task PumpEventsAsync()
    {
        await foreach (var queued in _events.Reader.ReadAllAsync().ConfigureAwait(false))
        {
            try
            {
                var e = queued.Event;

                // Oyuncu düzeyindeki olaylar (nesil -1) o anki medyaya atanır.
                if (e.Generation < 0)
                {
                    e = e with { Generation = CurrentGeneration };
                }

                if (e.Kind == AudioEngineEventKind.MetadataChanged && queued.Context is { } ctx)
                {
                    var meta = ctx.TryReadNowPlaying();
                    if (meta is null)
                    {
                        continue;
                    }

                    e = e with { Metadata = meta };
                }

                if (e.Kind == AudioEngineEventKind.Playing)
                {
                    // LibVLC 3 yeni ses çıkışı açıldığında ses seviyesini sıfırlayabilir; yeniden uygula.
                    lock (_sync)
                    {
                        if (!_disposed)
                        {
                            _player.Volume = Volume;
                            _player.Mute = IsMuted;
                        }
                    }
                }

                EngineEvent?.Invoke(this, e);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ses motoru olayı işlenirken hata.");
            }
        }
    }

    private readonly record struct QueuedEvent(AudioEngineEvent Event, MediaContext? Context);

    /// <summary>Tek bir Media nesnesi ve ona bağlı olay işleyicileri.</summary>
    private sealed class MediaContext : IDisposable
    {
        private readonly LibVlcAudioEngine _owner;
        private bool _disposed;

        public MediaContext(Media media, long generation, LibVlcAudioEngine owner)
        {
            Media = media;
            Generation = generation;
            _owner = owner;
            Media.StateChanged += OnStateChanged;
            Media.MetaChanged += OnMetaChanged;
        }

        public Media Media { get; }

        public long Generation { get; }

        public string? TryReadNowPlaying()
        {
            lock (_owner._sync)
            {
                if (_disposed)
                {
                    return null;
                }

                var nowPlaying = Media.Meta(MetadataType.NowPlaying);
                return string.IsNullOrWhiteSpace(nowPlaying) ? null : nowPlaying;
            }
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            Media.StateChanged -= OnStateChanged;
            Media.MetaChanged -= OnMetaChanged;
            Media.Dispose();
        }

        private void OnStateChanged(object? sender, MediaStateChangedEventArgs e)
        {
            AudioEngineEventKind? kind = e.State switch
            {
                VLCState.Opening => AudioEngineEventKind.Opening,
                VLCState.Buffering => AudioEngineEventKind.Buffering,
                VLCState.Playing => AudioEngineEventKind.Playing,
                VLCState.Paused => AudioEngineEventKind.Paused,
                VLCState.Stopped => AudioEngineEventKind.Stopped,
                VLCState.Ended => AudioEngineEventKind.EndReached,
                VLCState.Error => AudioEngineEventKind.Error,
                _ => null,
            };

            if (kind is { } k && k != AudioEngineEventKind.Buffering)
            {
                _owner.Enqueue(new QueuedEvent(new AudioEngineEvent(k, Generation), this));
            }
        }

        private void OnMetaChanged(object? sender, MediaMetaChangedEventArgs e)
        {
            if (e.MetadataType is MetadataType.NowPlaying or MetadataType.Title)
            {
                _owner.Enqueue(new QueuedEvent(new AudioEngineEvent(AudioEngineEventKind.MetadataChanged, Generation), this));
            }
        }
    }
}
