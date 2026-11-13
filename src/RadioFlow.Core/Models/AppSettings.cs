using RadioFlow.Core.Enums;

namespace RadioFlow.Core.Models;

/// <summary>Kalıcı uygulama ayarları. Her özellik Settings tablosunda ayrı bir anahtar olarak saklanır.</summary>
public sealed class AppSettings
{
    public AppTheme Theme { get; set; } = AppTheme.Dark;

    /// <summary>Widget arka plan katmanının opaklığı (0.3 – 1.0).</summary>
    public double WidgetOpacity { get; set; } = 0.85;

    public bool AlwaysOnTop { get; set; }
    public bool WidgetLocked { get; set; }
    public WidgetSize WidgetSize { get; set; } = WidgetSize.Normal;
    public bool RunAtStartup { get; set; }
    public bool ShowWidgetOnStartup { get; set; } = true;
    public bool AutoPlayLastStation { get; set; }
    public int DefaultVolume { get; set; } = 70;
    public int LastVolume { get; set; } = 70;
    public bool IsMuted { get; set; }
    public string Language { get; set; } = "tr-TR";
    public int MaxReconnectAttempts { get; set; } = 5;
    public int InitialReconnectDelaySeconds { get; set; } = 2;
    /// <summary>Widget üzerinde fare yokken ikincil kontrolleri (önceki/sonraki, durdur, favori, ses, genişlet) gizler.</summary>
    public bool AutoHideWidgetControls { get; set; }

    public bool MinimizeToTray { get; set; } = true;
    public bool ShowNotifications { get; set; } = true;
    public bool NotifyOnSongChange { get; set; }
    public string DefaultCountryCode { get; set; } = Constants.AppConstants.DefaultCountryCode;

    public double? WidgetLeft { get; set; }
    public double? WidgetTop { get; set; }
    public double? MainLeft { get; set; }
    public double? MainTop { get; set; }
    public double MainWidth { get; set; } = 1050;
    public double MainHeight { get; set; } = 700;

    public long? LastStationId { get; set; }

    public AppSettings Clone() => (AppSettings)MemberwiseClone();

    /// <summary>Değerleri geçerli aralıklara sıkıştırır (bozuk/elle düzenlenmiş veriye karşı).</summary>
    public void Normalize()
    {
        WidgetOpacity = Math.Clamp(WidgetOpacity, 0.5, 1.0);
        DefaultVolume = Math.Clamp(DefaultVolume, 0, 100);
        LastVolume = Math.Clamp(LastVolume, 0, 100);
        MaxReconnectAttempts = Math.Clamp(MaxReconnectAttempts, 0, 20);
        InitialReconnectDelaySeconds = Math.Clamp(InitialReconnectDelaySeconds, 1, 60);
        MainWidth = Math.Clamp(MainWidth, 800, 4000);
        MainHeight = Math.Clamp(MainHeight, 560, 3000);
        if (string.IsNullOrWhiteSpace(Language))
        {
            Language = "tr-TR";
        }
    }

    public PlaybackOptions ToPlaybackOptions() => new()
    {
        MaxReconnectAttempts = MaxReconnectAttempts,
        InitialReconnectDelay = TimeSpan.FromSeconds(InitialReconnectDelaySeconds),
    };
}
