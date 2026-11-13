using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using RadioFlow.App.Services;
using RadioFlow.Core.Enums;
using RadioFlow.Core.Interfaces;
using RadioFlow.Core.Models;

namespace RadioFlow.App.ViewModels;

/// <summary>Mini widget: oynatıcı + widget'a özgü ayarlar (şeffaflık, üstte tutma, konumu sabitleme, boyut, görünüm modu).</summary>
public sealed partial class WidgetViewModel : ObservableObject
{
    private readonly ISettingsService _settings;
    private readonly IShellService _shell;
    private readonly ILibraryService _library;
    private readonly IUiDispatcher _dispatcher;
    private readonly ILogger<WidgetViewModel> _logger;

    [ObservableProperty]
    private double _tintOpacity;

    [ObservableProperty]
    private bool _isAlwaysOnTop;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(LockGlyph))]
    [NotifyPropertyChangedFor(nameof(LockToolTip))]
    private bool _isLocked;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(WidgetWidth))]
    [NotifyPropertyChangedFor(nameof(WidgetHeight))]
    [NotifyPropertyChangedFor(nameof(LogoSize))]
    [NotifyPropertyChangedFor(nameof(TitleFontSize))]
    [NotifyPropertyChangedFor(nameof(IsCompact))]
    [NotifyPropertyChangedFor(nameof(IsExpanded))]
    private WidgetSize _size;

    /// <summary>Ayarlardan gelir: açıkken ikincil kontroller yalnızca fare widget üzerindeyken görünür.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowSecondaryControls))]
    private bool _autoHideControls;

    /// <summary>Widget üzerinde fare olup olmadığı; code-behind'daki MouseEnter/MouseLeave günceller.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowSecondaryControls))]
    private bool _isPointerOver;

    /// <summary>İstasyon hızlı değiştirme paneli (favoriler) açık mı.</summary>
    [ObservableProperty]
    private bool _isStationPickerOpen;

    public WidgetViewModel(
        PlayerViewModel player,
        ISettingsService settings,
        IShellService shell,
        ILibraryService library,
        IUiDispatcher dispatcher,
        ILogger<WidgetViewModel> logger)
    {
        Player = player;
        _settings = settings;
        _shell = shell;
        _library = library;
        _dispatcher = dispatcher;
        _logger = logger;
        ApplySettings(settings.Current);
        settings.Changed += (_, s) => _dispatcher.Post(() => ApplySettings(s));
        _library.FavoritesChanged += (_, _) => _dispatcher.Post(() =>
        {
            if (IsStationPickerOpen)
            {
                _ = LoadFavoriteStationsAsync();
            }
        });
    }

    public PlayerViewModel Player { get; }

    public ObservableCollection<RadioStation> FavoriteStations { get; } = [];

    /// <summary>Kontroller her zaman görünür olmalı (oto-gizle kapalı) veya fare widget üzerinde.</summary>
    public bool ShowSecondaryControls => !AutoHideControls || IsPointerOver;

    public bool IsCompact => Size == WidgetSize.Compact;

    public bool IsExpanded => Size == WidgetSize.Large;

    public double WidgetWidth => Size switch { WidgetSize.Compact => 260, WidgetSize.Large => 450, _ => 360 };

    public double WidgetHeight => Size switch { WidgetSize.Compact => 84, WidgetSize.Large => 230, _ => 150 };

    public double LogoSize => Size switch { WidgetSize.Compact => 44, WidgetSize.Large => 88, _ => 64 };

    public double TitleFontSize => Size switch { WidgetSize.Compact => 13, WidgetSize.Large => 19, _ => 15 };

    public string LockGlyph => IsLocked ? "" : "";

    public string LockToolTip => IsLocked ? "Konum sabit (çözmek için tıklayın)" : "Konumu sabitle";

    [RelayCommand]
    private void Expand() => _shell.ShowMainWindow();

    [RelayCommand]
    private void OpenSettings() => _shell.ShowSettings();

    [RelayCommand]
    private void Close() => _shell.CloseWidget();

    [RelayCommand]
    private Task ExitAsync() => _shell.ExitAsync();

    [RelayCommand]
    private Task ToggleAlwaysOnTopAsync() => _settings.UpdateAsync(s => s.AlwaysOnTop = !s.AlwaysOnTop);

    [RelayCommand]
    private Task ToggleLockAsync() => _settings.UpdateAsync(s => s.WidgetLocked = !s.WidgetLocked);

    [RelayCommand]
    private Task SetSizeAsync(WidgetSize size) => _settings.UpdateAsync(s => s.WidgetSize = size);

    [RelayCommand]
    private Task ToggleAutoHideAsync() => _settings.UpdateAsync(s => s.AutoHideWidgetControls = !s.AutoHideWidgetControls);

    /// <summary>İstasyon adına tıklanınca favori hızlı değiştirme panelini açar/kapatır.</summary>
    [RelayCommand]
    private async Task ToggleStationPickerAsync()
    {
        if (IsStationPickerOpen)
        {
            IsStationPickerOpen = false;
            return;
        }

        await LoadFavoriteStationsAsync();
        IsStationPickerOpen = true;
    }

    [RelayCommand]
    private async Task PickStationAsync(RadioStation? station)
    {
        IsStationPickerOpen = false;
        if (station is not null)
        {
            await Player.PlayStationAsync(station);
        }
    }

    public void OnPointerEnter() => IsPointerOver = true;

    public void OnPointerLeave() => IsPointerOver = false;

    public Task SavePositionAsync(double left, double top) =>
        _settings.UpdateAsync(s =>
        {
            s.WidgetLeft = left;
            s.WidgetTop = top;
        });

    private async Task LoadFavoriteStationsAsync()
    {
        try
        {
            var favorites = await _library.GetFavoritesAsync();
            FavoriteStations.Clear();
            foreach (var station in favorites)
            {
                FavoriteStations.Add(station);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Hızlı istasyon paneli için favoriler yüklenemedi.");
        }
    }

    private void ApplySettings(AppSettings s)
    {
        TintOpacity = s.WidgetOpacity;
        IsAlwaysOnTop = s.AlwaysOnTop;
        IsLocked = s.WidgetLocked;
        Size = s.WidgetSize;
        AutoHideControls = s.AutoHideWidgetControls;
    }
}
