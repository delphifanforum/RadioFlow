using RadioFlow.Core.Models;

namespace RadioFlow.Core.Interfaces;

public interface ISettingsService
{
    /// <summary>Yüklü ayarların canlı kopyası.</summary>
    AppSettings Current { get; }

    Task<AppSettings> LoadAsync(CancellationToken cancellationToken = default);

    Task SaveAsync(CancellationToken cancellationToken = default);

    /// <summary>Ayarları değiştirip kaydeder ve <see cref="Changed"/> olayını tetikler.</summary>
    Task UpdateAsync(Action<AppSettings> update, CancellationToken cancellationToken = default);

    event EventHandler<AppSettings>? Changed;
}

/// <summary>Settings tablosu için anahtar/değer deposu.</summary>
public interface ISettingsStore
{
    Task<IReadOnlyDictionary<string, string>> GetAllAsync(CancellationToken cancellationToken = default);

    Task SetManyAsync(IReadOnlyDictionary<string, string> values, CancellationToken cancellationToken = default);
}
