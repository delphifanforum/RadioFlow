using System.Runtime.Versioning;
using Microsoft.Extensions.Logging;
using Microsoft.Win32;
using RadioFlow.Core.Constants;
using RadioFlow.Core.Interfaces;

namespace RadioFlow.Infrastructure.WindowsIntegration;

/// <summary>
/// Bağımsız (paketlenmemiş) EXE için oturum açılışında başlatma: HKCU\...\Run anahtarı.
/// Yönetici yetkisi gerektirmez. Yalnızca kullanıcı ayarlardan açıkça etkinleştirdiğinde yazılır.
/// MSIX paketli kurulumda bu sınıf kullanılmaz (bkz. App katmanındaki StartupTask uygulaması).
/// </summary>
public sealed class RegistryStartupService(ILogger<RegistryStartupService> logger) : IStartupService
{
    internal const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    public const string StartupArgument = "--startup";

    public bool IsSupported => OperatingSystem.IsWindows() && Environment.ProcessPath is not null;

    public Task<bool> IsEnabledAsync()
    {
        if (!OperatingSystem.IsWindows())
        {
            return Task.FromResult(false);
        }

        return Task.FromResult(ReadValue() is not null);
    }

    public Task<bool> SetEnabledAsync(bool enabled)
    {
        if (!OperatingSystem.IsWindows() || Environment.ProcessPath is not { } exePath)
        {
            return Task.FromResult(false);
        }

        try
        {
            Write(enabled, exePath);
            logger.LogInformation("Başlangıçta çalıştırma {State}.", enabled ? "etkinleştirildi" : "kapatıldı");
            return Task.FromResult(enabled);
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or System.Security.SecurityException or IOException)
        {
            logger.LogError(ex, "Başlangıç kaydı değiştirilemedi.");
            return Task.FromResult(ReadValue() is not null);
        }
    }

    [SupportedOSPlatform("windows")]
    private static string? ReadValue()
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: false);
        return key?.GetValue(AppConstants.AppName) as string;
    }

    [SupportedOSPlatform("windows")]
    private static void Write(bool enabled, string exePath)
    {
        using var key = Registry.CurrentUser.CreateSubKey(RunKeyPath, writable: true);
        if (enabled)
        {
            key.SetValue(AppConstants.AppName, $"\"{exePath}\" {StartupArgument}", RegistryValueKind.String);
        }
        else if (key.GetValue(AppConstants.AppName) is not null)
        {
            key.DeleteValue(AppConstants.AppName, throwOnMissingValue: false);
        }
    }
}
