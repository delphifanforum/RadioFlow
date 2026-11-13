using System;
using System.Linq;
using System.Windows;
using Microsoft.Win32;
using RadioFlow.Core.Enums;

namespace RadioFlow.App.Services;

/// <summary>Koyu / açık / sistem temasını uygular. Renk sözlüğü çalışma anında değiştirilir (DynamicResource).</summary>
public sealed class ThemeService : IDisposable
{
    private const string DarkUri = "pack://application:,,,/RadioFlow;component/Themes/Colors.Dark.xaml";
    private const string LightUri = "pack://application:,,,/RadioFlow;component/Themes/Colors.Light.xaml";

    private AppTheme _requested = AppTheme.Dark;
    private bool _listening;

    public static bool IsDarkActive { get; private set; } = true;

    public void Apply(AppTheme theme)
    {
        _requested = theme;
        var dark = theme switch
        {
            AppTheme.Light => false,
            AppTheme.System => !IsSystemLightTheme(),
            _ => true,
        };

        if (theme == AppTheme.System && !_listening)
        {
            SystemEvents.UserPreferenceChanged += OnUserPreferenceChanged;
            _listening = true;
        }

        var dictionaries = Application.Current.Resources.MergedDictionaries;
        var existing = dictionaries.FirstOrDefault(d =>
            d.Source is not null && d.Source.OriginalString.Contains("/Themes/Colors.", StringComparison.Ordinal));
        if (existing is not null && IsDarkActive == dark && existing.Source.OriginalString == (dark ? DarkUri : LightUri))
        {
            return;
        }

        var replacement = new ResourceDictionary { Source = new Uri(dark ? DarkUri : LightUri, UriKind.Absolute) };
        if (existing is not null)
        {
            dictionaries[dictionaries.IndexOf(existing)] = replacement;
        }
        else
        {
            dictionaries.Insert(0, replacement);
        }

        IsDarkActive = dark;
        WindowBackdrop.RefreshAll(dark);
    }

    public void Dispose()
    {
        if (_listening)
        {
            SystemEvents.UserPreferenceChanged -= OnUserPreferenceChanged;
            _listening = false;
        }
    }

    private static bool IsSystemLightTheme()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
            return key?.GetValue("AppsUseLightTheme") is int value && value != 0;
        }
        catch (System.Security.SecurityException)
        {
            return false;
        }
    }

    private void OnUserPreferenceChanged(object? sender, UserPreferenceChangedEventArgs e)
    {
        if (e.Category == UserPreferenceCategory.General && _requested == AppTheme.System)
        {
            Application.Current?.Dispatcher.InvokeAsync(() => Apply(AppTheme.System));
        }
    }
}
