using RadioFlow.Core.Constants;
using RadioFlow.Core.Interfaces;

namespace RadioFlow.Infrastructure;

/// <summary>
/// Tüm kullanıcı verisi %LOCALAPPDATA%\RadioFlow altında tutulur; uygulama dizinine asla yazılmaz.
/// </summary>
public sealed class AppPaths : IAppPaths
{
    public AppPaths(string? rootDirectory = null)
    {
        DataDirectory = rootDirectory ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData, Environment.SpecialFolderOption.Create),
            AppConstants.AppName);
        DatabasePath = Path.Combine(DataDirectory, AppConstants.DatabaseFileName);
        LogsDirectory = Path.Combine(DataDirectory, AppConstants.LogsFolderName);
        LogoCacheDirectory = Path.Combine(DataDirectory, AppConstants.CacheFolderName, "Logos");

        Directory.CreateDirectory(DataDirectory);
        Directory.CreateDirectory(LogsDirectory);
        Directory.CreateDirectory(LogoCacheDirectory);
    }

    public string DataDirectory { get; }
    public string DatabasePath { get; }
    public string LogsDirectory { get; }
    public string LogoCacheDirectory { get; }
}
