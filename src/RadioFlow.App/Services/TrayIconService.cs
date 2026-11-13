using System;
using System.Drawing;
using System.Windows.Forms;
using Microsoft.Extensions.Logging;
using RadioFlow.App.ViewModels;
using RadioFlow.Core.Enums;
using RadioFlow.Core.Interfaces;
using RadioFlow.Core.Models;
using Application = System.Windows.Application;

namespace RadioFlow.App.Services;

/// <summary>
/// Sistem tepsisi simgesi ve menüsü (WinForms NotifyIcon). Bildirimler tepsi balonları ile gösterilir.
/// </summary>
public sealed class TrayIconService : IDisposable
{
    private readonly PlayerViewModel _player;
    private readonly IRadioPlayerService _playerService;
    private readonly ISettingsService _settings;
    private readonly IUiDispatcher _dispatcher;
    private readonly ILogger<TrayIconService> _logger;
    private NotifyIcon? _notifyIcon;
    private ToolStripMenuItem? _playPauseItem;
    private ToolStripMenuItem? _muteItem;
    private RadioStation? _lastAnnouncedStation;
    private IShellService? _shell;

    public TrayIconService(
        PlayerViewModel player,
        IRadioPlayerService playerService,
        ISettingsService settings,
        IUiDispatcher dispatcher,
        ILogger<TrayIconService> logger)
    {
        _player = player;
        _playerService = playerService;
        _settings = settings;
        _dispatcher = dispatcher;
        _logger = logger;
    }

    public void Initialize(IShellService shell)
    {
        _shell = shell;
        var menu = new ContextMenuStrip { ShowImageMargin = false };
        menu.Items.Add("RadioFlow'u Aç", null, (_, _) => _dispatcher.Post(shell.ShowMainWindow));
        menu.Items.Add("Widget'ı Göster", null, (_, _) => _dispatcher.Post(shell.ShowWidget));
        menu.Items.Add(new ToolStripSeparator());
        _playPauseItem = new ToolStripMenuItem("Oynat / Duraklat", null, (_, _) => _dispatcher.Post(() => _player.PlayPauseCommand.Execute(null)));
        menu.Items.Add(_playPauseItem);
        menu.Items.Add("Durdur", null, (_, _) => _dispatcher.Post(() => _player.StopCommand.Execute(null)));
        menu.Items.Add("Önceki Radyo", null, (_, _) => _dispatcher.Post(() => _player.PreviousCommand.Execute(null)));
        menu.Items.Add("Sonraki Radyo", null, (_, _) => _dispatcher.Post(() => _player.NextCommand.Execute(null)));
        _muteItem = new ToolStripMenuItem("Sessize Al", null, (_, _) => _dispatcher.Post(() => _player.ToggleMuteCommand.Execute(null)));
        menu.Items.Add(_muteItem);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Ayarlar", null, (_, _) => _dispatcher.Post(shell.ShowSettings));
        menu.Items.Add("Çıkış", null, (_, _) => _dispatcher.Post(() => _ = shell.ExitAsync()));
        menu.Opening += (_, _) =>
        {
            _muteItem.Checked = _player.IsMuted;
            _playPauseItem.Text = _player.IsActive ? "Duraklat" : "Oynat";
        };

        _notifyIcon = new NotifyIcon
        {
            Icon = LoadIcon(),
            Text = "RadioFlow",
            ContextMenuStrip = menu,
            Visible = true,
        };
        _notifyIcon.MouseClick += (_, e) =>
        {
            if (e.Button == MouseButtons.Left)
            {
                _dispatcher.Post(shell.ShowWidget);
            }
        };
        _notifyIcon.DoubleClick += (_, _) => _dispatcher.Post(shell.ShowMainWindow);

        _playerService.StateChanged += OnStateChanged;
        _playerService.NowPlayingChanged += OnNowPlayingChanged;
    }

    public void ShowNotification(string title, string text, ToolTipIcon icon = ToolTipIcon.None)
    {
        if (_notifyIcon is null || !_settings.Current.ShowNotifications)
        {
            return;
        }

        try
        {
            _notifyIcon.ShowBalloonTip(3000, title, text, icon);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Bildirim gösterilemedi.");
        }
    }

    public void Dispose()
    {
        _playerService.StateChanged -= OnStateChanged;
        _playerService.NowPlayingChanged -= OnNowPlayingChanged;
        if (_notifyIcon is not null)
        {
            _notifyIcon.Visible = false;
            _notifyIcon.ContextMenuStrip?.Dispose();
            _notifyIcon.Icon?.Dispose();
            _notifyIcon.Dispose();
            _notifyIcon = null;
        }
    }

    private static Icon LoadIcon()
    {
        var info = Application.GetResourceStream(new Uri("pack://application:,,,/RadioFlow;component/Assets/radioflow.ico"));
        if (info?.Stream is null)
        {
            return SystemIcons.Application;
        }

        using var stream = info.Stream;
        return new Icon(stream, SystemInformation.SmallIconSize);
    }

    private static string Truncate(string text, int max) => text.Length <= max ? text : text[..(max - 1)] + "…";

    private void OnStateChanged(object? sender, PlayerStateChangedEventArgs e) => _dispatcher.Post(() =>
    {
        if (_notifyIcon is null)
        {
            return;
        }

        var station = e.Station?.Name;
        // NotifyIcon.Text en fazla 127 karakter olabilir.
        _notifyIcon.Text = Truncate(station is null ? "RadioFlow" : $"RadioFlow — {station}", 120);

        if (e.NewState == PlayerState.Playing && e.Station is not null && !e.Station.IsSameStationAs(_lastAnnouncedStation))
        {
            _lastAnnouncedStation = e.Station;
            ShowNotification("Şimdi çalıyor", e.Station.Name);
        }
        else if (e.NewState == PlayerState.Error && e.Message is not null)
        {
            _lastAnnouncedStation = null;
            ShowNotification("Bağlantı sorunu", e.Message, ToolTipIcon.Warning);
        }
    });

    private void OnNowPlayingChanged(object? sender, NowPlayingChangedEventArgs e) => _dispatcher.Post(() =>
    {
        if (e.Info is not null && _settings.Current.NotifyOnSongChange)
        {
            ShowNotification(e.Station?.Name ?? "RadioFlow", e.Info.DisplayText);
        }
    });
}
