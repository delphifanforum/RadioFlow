namespace RadioFlow.Core.Enums;

/// <summary>Merkezi oynatıcının yaşam döngüsü durumları.</summary>
public enum PlayerState
{
    Stopped,
    Connecting,
    Buffering,
    Playing,
    Paused,
    Reconnecting,
    Error,
}
