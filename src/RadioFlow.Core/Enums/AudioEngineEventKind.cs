namespace RadioFlow.Core.Enums;

/// <summary>Ses motorundan (LibVLC veya test sahtesi) gelen ham olay türleri.</summary>
public enum AudioEngineEventKind
{
    Opening,
    Buffering,
    Playing,
    Paused,
    Stopped,
    EndReached,
    Error,
    MetadataChanged,
}
