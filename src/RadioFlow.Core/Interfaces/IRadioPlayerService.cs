using RadioFlow.Core.Enums;
using RadioFlow.Core.Models;

namespace RadioFlow.Core.Interfaces;

/// <summary>
/// Uygulamadaki TEK merkezi oynatıcı. Mini widget, ana pencere, tepsi menüsü ve sistem medya
/// kontrolleri aynı örneği kullanır; böylece pencere geçişlerinde ses kesilmez.
/// </summary>
public interface IRadioPlayerService
{
    Task PlayAsync(RadioStation station, CancellationToken cancellationToken = default);

    Task StopAsync();

    Task PauseAsync();

    Task ResumeAsync();

    void SetVolume(int volume);

    void SetMuted(bool muted);

    int Volume { get; }

    bool IsMuted { get; }

    PlayerState CurrentState { get; }

    RadioStation? CurrentStation { get; }

    NowPlayingInfo? NowPlaying { get; }

    /// <summary>Son hata mesajı (kullanıcı dostu Türkçe metin).</summary>
    string? LastErrorMessage { get; }

    /// <summary>Olaylar arka plan iş parçacığında tetiklenebilir; UI tarafı kendi dispatcher'ına aktarmalıdır.</summary>
    event EventHandler<PlayerStateChangedEventArgs>? StateChanged;

    event EventHandler<NowPlayingChangedEventArgs>? NowPlayingChanged;
}
