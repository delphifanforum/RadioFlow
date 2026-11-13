namespace RadioFlow.Core.Interfaces;

/// <summary>Windows oturum açılışında çalıştırma (bağımsız EXE: HKCU Run, MSIX: StartupTask).</summary>
public interface IStartupService
{
    bool IsSupported { get; }

    Task<bool> IsEnabledAsync();

    /// <summary>Durumu değiştirir; gerçekleşen durumu döndürür (MSIX'te kullanıcı/politika engelleyebilir).</summary>
    Task<bool> SetEnabledAsync(bool enabled);
}
