using System;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using RadioFlow.App.Services;
using RadioFlow.Core.Enums;
using RadioFlow.Core.Interfaces;
using RadioFlow.Core.Models;

namespace RadioFlow.App.ViewModels;

/// <summary>
/// Mini widget, ana penceredeki "Now Playing" çubuğu, tepsi ve medya tuşları tarafından paylaşılan TEK
/// oynatıcı görünüm modeli. Pencereler arasında geçişte aynı örnek kullanıldığı için ses kesilmez.
/// </summary>
public sealed partial class PlayerViewModel : ObservableObject
{
    private readonly IRadioPlayerService _player;
    private readonly ILibraryService _library;
    private readonly IRadioCatalogService _catalog;
    private readonly ILogoCache _logoCache;
    private readonly ISettingsService _settings;
    private readonly IUiDispatcher _dispatcher;
    private readonly ILogger<PlayerViewModel> _logger;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasStation))]
    [NotifyPropertyChangedFor(nameof(StationName))]
    [NotifyPropertyChangedFor(nameof(StationInitial))]
    private RadioStation? _currentStation;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsActive))]
    [NotifyPropertyChangedFor(nameof(IsBusy))]
    [NotifyPropertyChangedFor(nameof(PlayPauseGlyph))]
    [NotifyPropertyChangedFor(nameof(PlayPauseToolTip))]
    [NotifyPropertyChangedFor(nameof(IsError))]
    [NotifyPropertyChangedFor(nameof(SecondaryText))]
    private PlayerState _state = PlayerState.Stopped;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SecondaryText))]
    private string _statusText = "Bir istasyon seçin";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasNowPlaying))]
    [NotifyPropertyChangedFor(nameof(SecondaryText))]
    private string? _nowPlayingText;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(VolumeGlyph))]
    private int _volume = 70;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(VolumeGlyph))]
    private bool _isMuted;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(FavoriteGlyph))]
    private bool _isFavorite;

    [ObservableProperty]
    private string? _logoPath;

    public PlayerViewModel(
        IRadioPlayerService player,
        ILibraryService library,
        IRadioCatalogService catalog,
        ILogoCache logoCache,
        ISettingsService settings,
        IUiDispatcher dispatcher,
        ILogger<PlayerViewModel> logger)
    {
        _player = player;
        _library = library;
        _catalog = catalog;
        _logoCache = logoCache;
        _settings = settings;
        _dispatcher = dispatcher;
        _logger = logger;

        _player.StateChanged += (_, e) => _dispatcher.Post(() => OnPlayerStateChanged(e));
        _player.NowPlayingChanged += (_, e) => _dispatcher.Post(() => NowPlayingText = e.Info?.DisplayText);
        _library.FavoritesChanged += (_, _) => _dispatcher.Post(() => _ = RefreshFavoriteAsync());
    }

    public bool HasStation => CurrentStation is not null;

    public string StationName => CurrentStation?.Name ?? "RadioFlow";

    public string StationInitial => string.IsNullOrWhiteSpace(CurrentStation?.Name) ? "R" : CurrentStation!.Name.Trim()[..1].ToUpperInvariant();

    /// <summary>Şarkı bilgisi varsa onu, yoksa durum metnini gösterir (sahte şarkı adı üretilmez).</summary>
    public string SecondaryText => !string.IsNullOrWhiteSpace(NowPlayingText) && State == PlayerState.Playing ? NowPlayingText! : StatusText;

    public bool HasNowPlaying => !string.IsNullOrWhiteSpace(NowPlayingText);

    public bool IsActive => State is PlayerState.Playing or PlayerState.Buffering or PlayerState.Connecting or PlayerState.Reconnecting;

    public bool IsBusy => State is PlayerState.Buffering or PlayerState.Connecting or PlayerState.Reconnecting;

    public bool IsError => State == PlayerState.Error;

    public string PlayPauseGlyph => IsActive ? "" : "";

    public string PlayPauseToolTip => IsActive ? "Duraklat" : "Oynat";

    public string FavoriteGlyph => IsFavorite ? "" : "";

    public string VolumeGlyph => IsMuted || Volume == 0 ? "" : Volume < 34 ? "" : Volume < 67 ? "" : "";

    /// <summary>Ayarlardan ses seviyesi ve sessiz durumunu yükler.</summary>
    public void InitializeFromSettings()
    {
        // Uygulama her açılışta ayarlardaki varsayılan ses seviyesiyle başlar.
        var s = _settings.Current;
        Volume = s.DefaultVolume;
        IsMuted = s.IsMuted;
        _player.SetVolume(Volume);
        _player.SetMuted(IsMuted);
    }

    public async Task PlayStationAsync(RadioStation station)
    {
        ArgumentNullException.ThrowIfNull(station);
        try
        {
            CurrentStation = station;
            NowPlayingText = null;
            _ = LoadStationDetailsAsync(station);
            if (!string.IsNullOrEmpty(station.ExternalId))
            {
                // Radio Browser'ın önerdiği tıklama bildirimi (popülerlik istatistiği); hata yutulur.
                _ = _catalog.RegisterClickAsync(station.ExternalId);
            }

            await _player.PlayAsync(station);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Oynatma başlatılamadı: {Station}", station.Name);
            StatusText = "Oynatma başlatılamadı.";
        }
    }

    public async Task RestoreLastStationAsync(bool autoPlay)
    {
        if (_settings.Current.LastStationId is not { } id)
        {
            return;
        }

        try
        {
            var station = await _library.GetStationAsync(id);
            if (station is null)
            {
                return;
            }

            if (autoPlay)
            {
                await PlayStationAsync(station);
            }
            else
            {
                CurrentStation = station;
                StatusText = "Oynatmak için ▶ düğmesine basın";
                await LoadStationDetailsAsync(station);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Son istasyon yüklenemedi.");
        }
    }

    public Task SaveAudioSettingsAsync() =>
        _settings.UpdateAsync(s =>
        {
            s.LastVolume = Volume;
            s.IsMuted = IsMuted;
        });

    [RelayCommand]
    private async Task PlayPauseAsync()
    {
        switch (_player.CurrentState)
        {
            case PlayerState.Playing:
            case PlayerState.Buffering:
                await _player.PauseAsync();
                break;
            case PlayerState.Connecting:
            case PlayerState.Reconnecting:
                await _player.StopAsync();
                break;
            case PlayerState.Paused:
                await _player.ResumeAsync();
                break;
            default:
                if (CurrentStation is not null)
                {
                    await PlayStationAsync(CurrentStation);
                }
                else
                {
                    await NextAsync();
                }

                break;
        }
    }

    [RelayCommand]
    private Task StopAsync() => _player.StopAsync();

    [RelayCommand]
    private Task NextAsync() => PlayAdjacentAsync(+1);

    [RelayCommand]
    private Task PreviousAsync() => PlayAdjacentAsync(-1);

    [RelayCommand]
    private async Task ToggleFavoriteAsync()
    {
        if (CurrentStation is null)
        {
            return;
        }

        try
        {
            var persisted = await _library.EnsurePersistedAsync(CurrentStation);
            IsFavorite = await _library.ToggleFavoriteAsync(persisted);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Favori güncellenemedi.");
            StatusText = "Favori güncellenemedi.";
        }
    }

    [RelayCommand]
    private void ToggleMute()
    {
        IsMuted = !IsMuted;
    }

    partial void OnVolumeChanged(int value)
    {
        _player.SetVolume(value);
        if (value > 0 && IsMuted)
        {
            IsMuted = false;
        }
    }

    partial void OnIsMutedChanged(bool value) => _player.SetMuted(value);

    private async Task PlayAdjacentAsync(int direction)
    {
        try
        {
            var next = await _library.GetAdjacentFavoriteAsync(CurrentStation, direction);
            if (next is null)
            {
                StatusText = "Favori istasyon yok. ★ ile favori ekleyin.";
                return;
            }

            await PlayStationAsync(next);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Favori istasyona geçilemedi.");
        }
    }

    private async Task LoadStationDetailsAsync(RadioStation station)
    {
        LogoPath = null;
        try
        {
            IsFavorite = await _library.IsFavoriteAsync(station);
            var path = await _logoCache.GetLogoPathAsync(station.LogoUrl);
            if (station.IsSameStationAs(CurrentStation))
            {
                LogoPath = path;
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "İstasyon ayrıntıları yüklenemedi.");
        }
    }

    private async Task RefreshFavoriteAsync()
    {
        if (CurrentStation is { } station)
        {
            try
            {
                IsFavorite = await _library.IsFavoriteAsync(station);
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Favori durumu okunamadı.");
            }
        }
    }

    private void OnPlayerStateChanged(PlayerStateChangedEventArgs e)
    {
        State = e.NewState;
        if (e.Station is not null && !e.Station.IsSameStationAs(CurrentStation))
        {
            CurrentStation = e.Station;
            _ = LoadStationDetailsAsync(e.Station);
        }

        StatusText = e.NewState switch
        {
            PlayerState.Stopped => "Durduruldu",
            PlayerState.Connecting => "Bağlanıyor…",
            PlayerState.Buffering => "Arabelleğe alınıyor…",
            PlayerState.Playing => "Canlı yayın",
            PlayerState.Paused => e.Message ?? "Duraklatıldı",
            PlayerState.Reconnecting => e.Message ?? "Yeniden bağlanılıyor…",
            PlayerState.Error => e.Message ?? "Yayın oynatılamadı.",
            _ => StatusText,
        };

        if (e.NewState is PlayerState.Stopped or PlayerState.Error)
        {
            NowPlayingText = null;
        }
    }
}
