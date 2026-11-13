using System;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RadioFlow.App.Services;
using RadioFlow.Core.Enums;
using RadioFlow.Core.Interfaces;
using RadioFlow.Core.Models;

namespace RadioFlow.App.ViewModels;

/// <summary>Mini widget: oynatıcı + widget'a özgü ayarlar (şeffaflık, üstte tutma, konumu sabitleme, boyut).</summary>
public sealed partial class WidgetViewModel : ObservableObject
{
    private readonly ISettingsService _settings;
    private readonly IShellService _shell;
    private readonly IUiDispatcher _dispatcher;

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
    private WidgetSize _size;

    public WidgetViewModel(PlayerViewModel player, ISettingsService settings, IShellService shell, IUiDispatcher dispatcher)
    {
        Player = player;
        _settings = settings;
        _shell = shell;
        _dispatcher = dispatcher;
        ApplySettings(settings.Current);
        settings.Changed += (_, s) => _dispatcher.Post(() => ApplySettings(s));
    }

    public PlayerViewModel Player { get; }

    public double WidgetWidth => Size switch { WidgetSize.Compact => 320, WidgetSize.Large => 440, _ => 360 };

    public double WidgetHeight => Size switch { WidgetSize.Compact => 132, WidgetSize.Large => 180, _ => 150 };

    public double LogoSize => Size switch { WidgetSize.Compact => 52, WidgetSize.Large => 84, _ => 64 };

    public double TitleFontSize => Size switch { WidgetSize.Compact => 14, WidgetSize.Large => 18, _ => 15 };

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

    public Task SavePositionAsync(double left, double top) =>
        _settings.UpdateAsync(s =>
        {
            s.WidgetLeft = left;
            s.WidgetTop = top;
        });

    private void ApplySettings(AppSettings s)
    {
        TintOpacity = s.WidgetOpacity;
        IsAlwaysOnTop = s.AlwaysOnTop;
        IsLocked = s.WidgetLocked;
        Size = s.WidgetSize;
    }
}
