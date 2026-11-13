using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using RadioFlow.App.Services;
using RadioFlow.Core.Enums;
using RadioFlow.Core.Interfaces;

namespace RadioFlow.App.ViewModels;

public sealed record Option<T>(T Value, string Label);

/// <summary>Ayarlar penceresi. Değişiklikler "Kaydet" ile uygulanır ve kalıcı hale gelir.</summary>
public sealed partial class SettingsViewModel : ObservableObject
{
    private readonly ISettingsService _settings;
    private readonly IStartupService _startup;
    private readonly IAppPaths _paths;
    private readonly ThemeService _themeService;
    private readonly ILogger<SettingsViewModel> _logger;

    [ObservableProperty] private AppTheme _selectedTheme;
    [ObservableProperty] private double _widgetOpacity;
    [ObservableProperty] private bool _alwaysOnTop;
    [ObservableProperty] private bool _runAtStartup;
    [ObservableProperty] private bool _showWidgetOnStartup;
    [ObservableProperty] private bool _autoPlayLastStation;
    [ObservableProperty] private int _defaultVolume;
    [ObservableProperty] private WidgetSize _widgetSize;
    [ObservableProperty] private double _mainWidth;
    [ObservableProperty] private double _mainHeight;
    [ObservableProperty] private string _language = "tr-TR";
    [ObservableProperty] private int _maxReconnectAttempts;
    [ObservableProperty] private int _initialReconnectDelaySeconds;
    [ObservableProperty] private bool _minimizeToTray;
    [ObservableProperty] private bool _showNotifications;
    [ObservableProperty] private bool _notifyOnSongChange;
    [ObservableProperty] private string _defaultCountryCode = "TR";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasStatus))]
    private string? _statusMessage;

    public SettingsViewModel(
        ISettingsService settings,
        IStartupService startup,
        IAppPaths paths,
        ThemeService theme,
        ILogger<SettingsViewModel> logger)
    {
        _settings = settings;
        _startup = startup;
        _paths = paths;
        _themeService = theme;
        _logger = logger;
    }

    public event EventHandler? CloseRequested;

    public IReadOnlyList<Option<AppTheme>> ThemeOptions { get; } =
    [
        new(AppTheme.Dark, "Koyu"),
        new(AppTheme.Light, "Açık"),
        new(AppTheme.System, "Sistem"),
    ];

    public IReadOnlyList<Option<WidgetSize>> WidgetSizeOptions { get; } =
    [
        new(WidgetSize.Compact, "Kompakt"),
        new(WidgetSize.Normal, "Normal"),
        new(WidgetSize.Large, "Büyük"),
    ];

    /// <summary>Dil altyapısı: şu an yalnızca Türkçe kaynak sözlüğü bulunur.</summary>
    public IReadOnlyList<Option<string>> LanguageOptions { get; } =
    [
        new("tr-TR", "Türkçe"),
    ];

    public bool IsStartupSupported => _startup.IsSupported;

    public string StartupNote => _startup is StartupServiceSelector { IsPackaged: true }
        ? "MSIX paketi: Windows Görev Yöneticisi › Başlangıç uygulamalarından da yönetilebilir."
        : "Bağımsız EXE: kullanıcı kayıt defterine (HKCU\\…\\Run) yazılır; yönetici izni gerekmez.";

    public string DataDirectory => _paths.DataDirectory;

    public bool HasStatus => !string.IsNullOrWhiteSpace(StatusMessage);

    public async Task LoadAsync()
    {
        var s = _settings.Current;
        SelectedTheme = s.Theme;
        WidgetOpacity = s.WidgetOpacity;
        AlwaysOnTop = s.AlwaysOnTop;
        ShowWidgetOnStartup = s.ShowWidgetOnStartup;
        AutoPlayLastStation = s.AutoPlayLastStation;
        DefaultVolume = s.DefaultVolume;
        WidgetSize = s.WidgetSize;
        MainWidth = s.MainWidth;
        MainHeight = s.MainHeight;
        Language = s.Language;
        MaxReconnectAttempts = s.MaxReconnectAttempts;
        InitialReconnectDelaySeconds = s.InitialReconnectDelaySeconds;
        MinimizeToTray = s.MinimizeToTray;
        ShowNotifications = s.ShowNotifications;
        NotifyOnSongChange = s.NotifyOnSongChange;
        DefaultCountryCode = s.DefaultCountryCode;
        StatusMessage = null;

        // Gerçek durum sistemden okunur (kullanıcı Görev Yöneticisi'nden kapatmış olabilir).
        RunAtStartup = _startup.IsSupported && await _startup.IsEnabledAsync();
    }

    /// <summary>Tema seçimi anında önizlenir; iptal edilirse geri alınır.</summary>
    partial void OnSelectedThemeChanged(AppTheme value) => _themeService.Apply(value);

    [RelayCommand]
    private async Task SaveAsync()
    {
        StatusMessage = null;
        var country = (DefaultCountryCode ?? string.Empty).Trim().ToUpperInvariant();
        if (country.Length != 0 && (country.Length != 2 || !char.IsAsciiLetter(country[0]) || !char.IsAsciiLetter(country[1])))
        {
            StatusMessage = "Ülke kodu iki harfli olmalıdır (ör. TR, DE).";
            return;
        }

        try
        {
            var startupResult = RunAtStartup;
            if (_startup.IsSupported && RunAtStartup != await _startup.IsEnabledAsync())
            {
                startupResult = await _startup.SetEnabledAsync(RunAtStartup);
                if (startupResult != RunAtStartup)
                {
                    StatusMessage = "Başlangıçta çalıştırma ayarı Windows tarafından değiştirilemedi.";
                }
            }

            await _settings.UpdateAsync(s =>
            {
                s.Theme = SelectedTheme;
                s.WidgetOpacity = WidgetOpacity;
                s.AlwaysOnTop = AlwaysOnTop;
                s.RunAtStartup = startupResult;
                s.ShowWidgetOnStartup = ShowWidgetOnStartup;
                s.AutoPlayLastStation = AutoPlayLastStation;
                s.DefaultVolume = DefaultVolume;
                s.WidgetSize = WidgetSize;
                s.MainWidth = MainWidth;
                s.MainHeight = MainHeight;
                s.Language = Language;
                s.MaxReconnectAttempts = MaxReconnectAttempts;
                s.InitialReconnectDelaySeconds = InitialReconnectDelaySeconds;
                s.MinimizeToTray = MinimizeToTray;
                s.ShowNotifications = ShowNotifications;
                s.NotifyOnSongChange = NotifyOnSongChange;
                s.DefaultCountryCode = country.Length == 0 ? "TR" : country;
            });

            if (StatusMessage is null)
            {
                CloseRequested?.Invoke(this, EventArgs.Empty);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ayarlar kaydedilemedi.");
            StatusMessage = "Ayarlar kaydedilemedi. Ayrıntılar log dosyasındadır.";
        }
    }

    [RelayCommand]
    private void Cancel()
    {
        _themeService.Apply(_settings.Current.Theme);
        CloseRequested?.Invoke(this, EventArgs.Empty);
    }

    [RelayCommand]
    private void OpenDataFolder()
    {
        try
        {
            Process.Start(new ProcessStartInfo { FileName = _paths.DataDirectory, UseShellExecute = true });
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Veri klasörü açılamadı.");
        }
    }

    [RelayCommand]
    private void OpenLogsFolder()
    {
        try
        {
            Process.Start(new ProcessStartInfo { FileName = _paths.LogsDirectory, UseShellExecute = true });
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Log klasörü açılamadı.");
        }
    }
}
