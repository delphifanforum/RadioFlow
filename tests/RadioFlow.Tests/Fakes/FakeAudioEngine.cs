using RadioFlow.Core.Enums;
using RadioFlow.Core.Interfaces;
using RadioFlow.Core.Models;

namespace RadioFlow.Tests.Fakes;

/// <summary>Gerçek yayın açmayan, davranışı test tarafından belirlenen ses motoru.</summary>
internal sealed class FakeAudioEngine : IAudioEngine
{
    public enum Behavior
    {
        Succeed,
        Fail,
        Hang,
    }

    private readonly List<Uri> _played = [];
    private int _playCount;
    private int _stopCount;
    private long _lastGeneration;

    public event EventHandler<AudioEngineEvent>? EngineEvent;

    public Behavior Mode { get; set; } = Behavior.Succeed;

    public Func<Uri, Behavior>? BehaviorSelector { get; set; }

    public bool CanPause { get; set; } = true;

    public int PlayCount => Volatile.Read(ref _playCount);

    public int StopCount => Volatile.Read(ref _stopCount);

    public IReadOnlyList<Uri> PlayedUris
    {
        get
        {
            lock (_played)
            {
                return _played.ToList();
            }
        }
    }

    public int Volume { get; set; }

    public bool IsMuted { get; set; }

    public void Play(Uri streamUri, long generation)
    {
        Interlocked.Increment(ref _playCount);
        Interlocked.Exchange(ref _lastGeneration, generation);
        lock (_played)
        {
            _played.Add(streamUri);
        }

        var behavior = BehaviorSelector?.Invoke(streamUri) ?? Mode;
        Raise(AudioEngineEventKind.Opening, generation);
        switch (behavior)
        {
            case Behavior.Succeed:
                Raise(AudioEngineEventKind.Buffering, generation, 50f);
                Raise(AudioEngineEventKind.Playing, generation);
                break;
            case Behavior.Fail:
                Raise(AudioEngineEventKind.Error, generation);
                break;
            case Behavior.Hang:
                break;
        }
    }

    public void Stop() => Interlocked.Increment(ref _stopCount);

    public bool TryPause() => CanPause;

    public void Resume()
    {
    }

    /// <summary>Çalan yayın için olay üretir (ör. bağlantı kopması, metadata).</summary>
    public void RaiseForCurrent(AudioEngineEventKind kind, string? metadata = null) =>
        EngineEvent?.Invoke(this, new AudioEngineEvent(kind, Interlocked.Read(ref _lastGeneration), Metadata: metadata));

    public void Dispose()
    {
    }

    private void Raise(AudioEngineEventKind kind, long generation, float buffer = 0) =>
        EngineEvent?.Invoke(this, new AudioEngineEvent(kind, generation, buffer));
}
