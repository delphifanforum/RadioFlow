using System;
using System.Runtime.InteropServices;

namespace RadioFlow.App.Interop;

internal static class NativeMethods
{
    public const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;
    public const int DWMWA_WINDOW_CORNER_PREFERENCE = 33;
    public const int DWMWA_SYSTEMBACKDROP_TYPE = 38;

    public const int DWMWCP_ROUND = 2;
    public const int DWMWCP_ROUNDSMALL = 3;

    public const int DWMSBT_NONE = 1;
    public const int DWMSBT_MAINWINDOW = 2;      // Mica
    public const int DWMSBT_TRANSIENTWINDOW = 3; // Acrylic

    public const int APPMODEL_ERROR_NO_PACKAGE = 15700;

    [StructLayout(LayoutKind.Sequential)]
    public struct MARGINS
    {
        public int Left;
        public int Right;
        public int Top;
        public int Bottom;

        public MARGINS(int all)
        {
            Left = Right = Top = Bottom = all;
        }
    }

    [DllImport("dwmapi.dll")]
    public static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);

    [DllImport("dwmapi.dll")]
    public static extern int DwmExtendFrameIntoClientArea(IntPtr hwnd, ref MARGINS margins);

    [DllImport("kernel32.dll")]
    public static extern int GetCurrentPackageFullName(ref int packageFullNameLength, IntPtr packageFullName);

    /// <summary>Uygulama MSIX paketi kimliğiyle mi çalışıyor?</summary>
    public static bool IsPackaged()
    {
        try
        {
            var length = 0;
            return GetCurrentPackageFullName(ref length, IntPtr.Zero) != APPMODEL_ERROR_NO_PACKAGE;
        }
        catch (EntryPointNotFoundException)
        {
            return false;
        }
    }
}
