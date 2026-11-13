using System;
using Microsoft.Extensions.Logging;
using RadioFlow.App.ViewModels;
using RadioFlow.Core.Enums;
using RadioFlow.Core.Interfaces;
using RadioFlow.Core.Models;
using Windows.Media;
using Windows.Storage.Streams;

namespace RadioFlow.App.Services;

/// <summary>
/// Windows Sistem Medya Taşıma Kontrolleri (SMTC) entegrasyonu: klavye medya tuşları, ses seviyesi
/// açılır penceresi ve kilit ekranındaki medya bilgisi. Masaüstü (Win32) uygulamalar için desteklenen
/// yöntem olarak Windows.Media.Playback.MediaPlayer'ın SMTC örneği kullanılır; MediaPlayer ses çalmak için
/// KULLANILMAZ (CommandManager kapalı), ses LibVLC'den gelir.
/// Herhangi bir hata olursa entegrasyon kapatılır ve temel oynatma etkilenmez.
/// </summary>
public sealed class MediaControlsService : IDisposable
{
    private readonly IRadioPlayerService _playerService;
    private readonly PlayerViewModel _player;
    private readonly IUiDispatcher _dispatcher;
    private readonly ILogger<MediaControlsService> _logger;
    private Windows.Media.Playback.MediaPlayer? _mediaPlayer;
    private SystemMediaTransportControls? _smtc;

    public MediaControlsService(
        IRadioPlayerService playerService,
        PlayerViewModel player,
        IUiDispatcher dispatcher,
        ILogger<MediaControlsService> logger)
    {
        _playerService = playerService;
        _player = player;
        _dispatcher = dispatcher;
        _logger = logger;
    }

    public bool IsAvailable => _smtc is not null;

    public void Initialize()
    {
        try
        {
            _mediaPlayer = new Windows.Media.Playback.MediaPlayer();
            _mediaPlayer.CommandManager.IsEnabled = false;
            _smtc = _mediaPlayer.SystemMediaTransportControls;
            _smtc.IsEnabled = true;
            _smtc.IsPlayEnabled = true;
            _smtc.IsPauseEnabled = true;
            _smtc.IsStopEnabled = true;
            _smtc.IsNextEnabled = true;
            _smtc.IsPreviousEnabled = true;
            _smtc.PlaybackStatus = MediaPlaybackStatus.Closed;
            _smtc.ButtonPressed += OnButtonPressed;

            _playerService.StateChanged += OnStateChanged;
            _playerService.NowPlayingChanged += OnNowPlayingChanged;
            _logger.LogInformation("Sistem medya kontrolleri etkin.");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Sistem medya kontrolleri başlatılamadı; medya tuşları devre dışı.");
            Dispose();
        }
    }

    public void Dispose()
    {
        _playerService.StateChanged -= OnStateChanged;
        _playerService.NowPlayingChanged -= OnNowPlayingChanged;
        if (_smtc is not null)
        {
            try
            {
                _smtc.ButtonPressed -= OnButtonPressed;
                _smtc.DisplayUpdater.ClearAll();
                _smtc.IsEnabled = false;
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "SMTC kapatılırken hata.");
            }

            _smtc = null;
        }

        _mediaPlayer?.Dispose();
        _mediaPlayer = null;
    }

    private void OnButtonPressed(SystemMediaTransportControls sender, SystemMediaTransportControlsButtonPressedEventArgs args)
    {
        // Bu olay arka plan iş parçacığında gelir.
        var button = args.Button;
        _dispatcher.Post(() =>
        {
            switch (button)
            {
                case SystemMediaTransportControlsButton.Play:
                case SystemMediaTransportControlsButton.Pause:
                    _player.PlayPauseCommand.Execute(null);
                    break;
                case SystemMediaTransportControlsButton.Stop:
                    _player.StopCommand.Execute(null);
                    break;
                case SystemMediaTransportControlsButton.Next:
                    _player.NextCommand.Execute(null);
                    break;
                case SystemMediaTransportControlsButton.Previous:
                    _player.PreviousCommand.Execute(null);
                    break;
            }
        });
    }

    private void OnStateChanged(object? sender, PlayerStateChangedEventArgs e)
    {
        var smtc = _smtc;
        if (smtc is null)
        {
            return;
        }

        try
        {
            smtc.PlaybackStatus = e.NewState switch
            {
                PlayerState.Playing => MediaPlaybackStatus.Playing,
                PlayerState.Paused => MediaPlaybackStatus.Paused,
                PlayerState.Connecting or PlayerState.Buffering or PlayerState.Reconnecting => MediaPlaybackStatus.Changing,
                PlayerState.Stopped => MediaPlaybackStatus.Stopped,
                _ => MediaPlaybackStatus.Stopped,
            };

            if (e.NewState is PlayerState.Connecting or PlayerState.Playing)
            {
                UpdateDisplay(e.Station, _playerService.NowPlaying);
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "SMTC durumu güncellenemedi.");
        }
    }

    private void OnNowPlayingChanged(object? sender, NowPlayingChangedEventArgs e)
    {
        try
        {
            UpdateDisplay(e.Station, e.Info);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "SMTC metadata güncellenemedi.");
        }
    }

    private void UpdateDisplay(RadioStation? station, NowPlayingInfo? info)
    {
        var smtc = _smtc;
        if (smtc is null || station is null)
        {
            return;
        }

        var updater = smtc.DisplayUpdater;
        updater.Type = MediaPlaybackType.Music;
        // Metadata yoksa yalnızca istasyon adı gösterilir; sahte şarkı adı üretilmez.
        updater.MusicProperties.Title = info?.Title ?? station.Name;
        updater.MusicProperties.Artist = info?.Artist ?? (info is null ? "Canlı yayın" : station.Name);
        updater.MusicProperties.AlbumTitle = station.Name;

        if (station.LogoUrl is not null && Uri.TryCreate(station.LogoUrl, UriKind.Absolute, out var logo)
            && (logo.Scheme == Uri.UriSchemeHttps || logo.Scheme == Uri.UriSchemeHttp))
        {
            updater.Thumbnail = RandomAccessStreamReference.CreateFromUri(logo);
        }
        else
        {
            updater.Thumbnail = null;
        }

        updater.Update();
    }
}
