namespace RadioFlow.Core.Interfaces;

/// <summary>Kullanıcıya ait veri dizinleri (%LOCALAPPDATA%\RadioFlow).</summary>
public interface IAppPaths
{
    string DataDirectory { get; }
    string DatabasePath { get; }
    string LogsDirectory { get; }
    string LogoCacheDirectory { get; }
}
