using System;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Shell;
using RadioFlow.App.Interop;

namespace RadioFlow.App.Services;

/// <summary>
/// Windows 11 (22H2+, derleme 22621) üzerinde DWM sistem arka planı (Acrylic/Mica), koyu başlık modu ve
/// yuvarlatılmış köşeleri uygular. Desteklenmeyen sistemlerde opak düz renk arka plana geçer.
/// Efekt tamamen DWM tarafından çizilir; uygulamada sürekli çalışan animasyon veya zamanlayıcı yoktur.
/// </summary>
public static class WindowBackdrop
{
    public static bool IsBackdropSupported { get; } =
        OperatingSystem.IsWindowsVersionAtLeast(10, 0, 22621);

    public static bool IsRoundedCornerSupported { get; } =
        OperatingSystem.IsWindowsVersionAtLeast(10, 0, 22000);

    /// <param name="window">Hedef pencere.</param>
    /// <param name="acrylic">true: Acrylic (widget), false: Mica (ana pencere).</param>
    /// <param name="captionHeight">Sürüklenebilir başlık yüksekliği (0 = yok).</param>
    /// <param name="resizable">Kenarlardan yeniden boyutlandırma.</param>
    public static void Attach(Window window, bool acrylic, double captionHeight, bool resizable)
    {
        ArgumentNullException.ThrowIfNull(window);

        WindowChrome.SetWindowChrome(window, new WindowChrome
        {
            CaptionHeight = captionHeight,
            CornerRadius = new CornerRadius(0),
            GlassFrameThickness = IsBackdropSupported ? new Thickness(-1) : new Thickness(0),
            ResizeBorderThickness = resizable ? new Thickness(6) : new Thickness(0),
            UseAeroCaptionButtons = false,
        });

        if (IsBackdropSupported)
        {
            window.Background = Brushes.Transparent;
        }
        else
        {
            window.SetResourceReference(Window.BackgroundProperty, "Brush.SolidBackground");
        }

        void OnSourceInitialized(object? sender, EventArgs e)
        {
            window.SourceInitialized -= OnSourceInitialized;
            Apply(window, acrylic, ThemeService.IsDarkActive);
        }

        if (new WindowInteropHelper(window).Handle != IntPtr.Zero)
        {
            Apply(window, acrylic, ThemeService.IsDarkActive);
        }
        else
        {
            window.SourceInitialized += OnSourceInitialized;
        }

        window.SetValue(AcrylicProperty, acrylic);
    }

    public static void Apply(Window window, bool acrylic, bool dark)
    {
        var hwnd = new WindowInteropHelper(window).Handle;
        if (hwnd == IntPtr.Zero)
        {
            return;
        }

        try
        {
            var darkValue = dark ? 1 : 0;
            NativeMethods.DwmSetWindowAttribute(hwnd, NativeMethods.DWMWA_USE_IMMERSIVE_DARK_MODE, ref darkValue, sizeof(int));

            if (IsRoundedCornerSupported)
            {
                var corner = NativeMethods.DWMWCP_ROUND;
                NativeMethods.DwmSetWindowAttribute(hwnd, NativeMethods.DWMWA_WINDOW_CORNER_PREFERENCE, ref corner, sizeof(int));
            }

            if (IsBackdropSupported)
            {
                if (HwndSource.FromHwnd(hwnd) is { CompositionTarget: { } target })
                {
                    target.BackgroundColor = Colors.Transparent;
                }

                var margins = new NativeMethods.MARGINS(-1);
                NativeMethods.DwmExtendFrameIntoClientArea(hwnd, ref margins);
                var backdrop = acrylic ? NativeMethods.DWMSBT_TRANSIENTWINDOW : NativeMethods.DWMSBT_MAINWINDOW;
                NativeMethods.DwmSetWindowAttribute(hwnd, NativeMethods.DWMWA_SYSTEMBACKDROP_TYPE, ref backdrop, sizeof(int));
            }
        }
        catch (DllNotFoundException)
        {
            // DWM yoksa (ör. sunucu çekirdeği) düz arka plan kalır.
        }
    }

    /// <summary>Tema değişince açık pencerelerin DWM koyu/açık modunu günceller.</summary>
    public static void RefreshAll(bool dark)
    {
        foreach (Window window in Application.Current.Windows)
        {
            if (window.ReadLocalValue(AcrylicProperty) is bool acrylic)
            {
                Apply(window, acrylic, dark);
            }
        }
    }

    private static readonly DependencyProperty AcrylicProperty =
        DependencyProperty.RegisterAttached("Acrylic", typeof(bool), typeof(WindowBackdrop), new PropertyMetadata(false));
}
