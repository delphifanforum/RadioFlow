using RadioFlow.Core.Enums;

namespace RadioFlow.Core.Models;

public sealed class PlayerStateChangedEventArgs(
    PlayerState oldState,
    PlayerState newState,
    RadioStation? station,
    string? message = null,
    int reconnectAttempt = 0) : EventArgs
{
    public PlayerState OldState { get; } = oldState;
    public PlayerState NewState { get; } = newState;
    public RadioStation? Station { get; } = station;

    /// <summary>Kullanıcıya gösterilebilir Türkçe açıklama (stack trace içermez).</summary>
    public string? Message { get; } = message;

    public int ReconnectAttempt { get; } = reconnectAttempt;
}

public sealed class NowPlayingChangedEventArgs(RadioStation? station, NowPlayingInfo? info) : EventArgs
{
    public RadioStation? Station { get; } = station;
    public NowPlayingInfo? Info { get; } = info;
}
