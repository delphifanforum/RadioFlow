using System;
using System.Threading.Tasks;
using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using RadioFlow.App.ViewModels;
using RadioFlow.App.Views;
using RadioFlow.Core.Interfaces;

namespace RadioFlow.App.Services;

/// <summary>
/// Pencere yönetimi ve uygulama yaşam döngüsü. Mini widget ile büyük panel arasında geçiş yalnızca
/// pencereleri gösterip gizler; oynatıcı servisine dokunulmadığı için ses kesilmez.
/// </summary>
public sealed class ShellService(
    IServiceProvider services,
    ISettingsService settings,
    IRadioPlayerService player,
    IPlaybackHistoryTracker historyTracker,
    ThemeService themeService,
    ILogger<ShellService> logger) : IShellService
{
    private TrayIconService? _tray;
    private MediaControlsService? _mediaControls;
    private SettingsWindow? _settingsWindow;
    private bool _exiting;

    public void Initialize()
    {
        settings.Changed += (_, s) => Application.Current.Dispatcher.InvokeAsync(() => themeService.Apply(s.Theme));

        _tray = services.GetRequiredService<TrayIconService>();
        _tray.Initialize(this);

        _mediaControls = services.GetRequiredService<MediaControlsService>();
        _mediaControls.Initialize();

        var s = settings.Current;
        if (s.ShowWidgetOnStartup)
        {
            ShowWidget();
        }
        else if (!s.MinimizeToTray)
        {
            // Tepsi kapalı ve widget gizli ise kullanıcı arayüzsüz kalmasın.
            ShowMainWindow();
        }
        else if (!App.LaunchedAtStartup)
        {
            _tray.ShowNotification("RadioFlow çalışıyor", "Sistem tepsisindeki simgeden erişebilirsiniz.");
        }
    }

    public void ShowWidget()
    {
        var widget = services.GetRequiredService<MiniWidgetWindow>();
        if (!widget.IsVisible)
        {
            widget.Show();
        }

        if (widget.WindowState == WindowState.Minimized)
        {
            widget.WindowState = WindowState.Normal;
        }

        widget.Activate();
    }

    public void HideWidget() => services.GetRequiredService<MiniWidgetWindow>().Hide();

    public void ShowMainWindow()
    {
        var main = services.GetRequiredService<MainWindow>();
        if (!main.IsVisible)
        {
            main.Show();
        }

        if (main.WindowState == WindowState.Minimized)
        {
            main.WindowState = WindowState.Normal;
        }

        main.Activate();
    }

    public void ShowSettings()
    {
        if (_settingsWindow is { IsLoaded: true })
        {
            _settingsWindow.Activate();
            return;
        }

        _settingsWindow = services.GetRequiredService<SettingsWindow>();
        _settingsWindow.Closed += (_, _) => _settingsWindow = null;
        var owner = services.GetRequiredService<MainWindow>();
        if (owner.IsVisible)
        {
            _settingsWindow.Owner = owner;
            _settingsWindow.WindowStartupLocation = WindowStartupLocation.CenterOwner;
        }

        _settingsWindow.Show();
        _settingsWindow.Activate();
    }

    public void CloseWidget()
    {
        if (settings.Current.MinimizeToTray)
        {
            HideWidget();
            _tray?.ShowNotification("RadioFlow arka planda", "Yayın devam ediyor. Tepsi simgesinden yeniden açabilirsiniz.");
            return;
        }

        var main = services.GetRequiredService<MainWindow>();
        if (main.IsVisible)
        {
            HideWidget();
            return;
        }

        _ = ExitAsync();
    }

    public async Task ExitAsync()
    {
        if (_exiting)
        {
            return;
        }

        _exiting = true;
        logger.LogInformation("Çıkış isteniyor; servisler kapatılıyor.");
        try
        {
            if (services.GetService<MiniWidgetWindow>() is { } widget)
            {
                widget.SavePlacement();
            }

            if (services.GetService<MainWindow>() is { } main)
            {
                main.SavePlacement();
            }

            await services.GetRequiredService<PlayerViewModel>().SaveAudioSettingsAsync();
            await historyTracker.FlushAsync();
            await player.StopAsync();
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Kapanış sırasında hata.");
        }

        _mediaControls?.Dispose();
        _tray?.Dispose();
        themeService.Dispose();

        foreach (Window window in Application.Current.Windows)
        {
            if (window is IForceClosable closable)
            {
                closable.ForceClose();
            }
        }

        if (Application.Current is App app)
        {
            await app.ShutdownCoreAsync();
        }

        Application.Current?.Shutdown(0);
    }

    public void ShowError(string message) =>
        MessageBox.Show(message, "RadioFlow", MessageBoxButton.OK, MessageBoxImage.Warning);

    public bool Confirm(string message, string title) =>
        MessageBox.Show(message, title, MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes;
}

/// <summary>Kapatma isteğini gizlemeye çeviren pencerelerin çıkışta gerçekten kapanması için.</summary>
public interface IForceClosable
{
    void ForceClose();
}
