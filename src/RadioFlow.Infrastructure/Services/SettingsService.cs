using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using RadioFlow.Core.Interfaces;
using RadioFlow.Core.Models;

namespace RadioFlow.Infrastructure.Services;

/// <summary>
/// <see cref="AppSettings"/> özelliklerini Settings tablosunda "app.&lt;Özellik&gt;" anahtarlarıyla JSON değer olarak saklar.
/// Bozuk veya tanınmayan değerler tek tek atlanır; diğer ayarlar korunur.
/// </summary>
public sealed class SettingsService(ISettingsStore store, ILogger<SettingsService> logger) : ISettingsService
{
    internal const string KeyPrefix = "app.";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        Converters = { new JsonStringEnumConverter() },
    };

    private static readonly PropertyInfo[] Properties = typeof(AppSettings)
        .GetProperties(BindingFlags.Public | BindingFlags.Instance)
        .Where(p => p is { CanRead: true, CanWrite: true })
        .ToArray();

    private readonly SemaphoreSlim _gate = new(1, 1);

    public AppSettings Current { get; private set; } = new();

    public event EventHandler<AppSettings>? Changed;

    public async Task<AppSettings> LoadAsync(CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var values = await store.GetAllAsync(cancellationToken).ConfigureAwait(false);
            var settings = new AppSettings();
            foreach (var property in Properties)
            {
                if (!values.TryGetValue(KeyPrefix + property.Name, out var json))
                {
                    continue;
                }

                try
                {
                    var value = JsonSerializer.Deserialize(json, property.PropertyType, JsonOptions);
                    if (value is not null || Nullable.GetUnderlyingType(property.PropertyType) is not null)
                    {
                        property.SetValue(settings, value);
                    }
                }
                catch (Exception ex) when (ex is JsonException or NotSupportedException or ArgumentException)
                {
                    logger.LogWarning(ex, "Ayar değeri okunamadı, varsayılan kullanılacak: {Key}", property.Name);
                }
            }

            settings.Normalize();
            Current = settings;
            return settings;
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task SaveAsync(CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await SaveCoreAsync(Current, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task UpdateAsync(Action<AppSettings> update, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(update);
        AppSettings snapshot;
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var copy = Current.Clone();
            update(copy);
            copy.Normalize();
            await SaveCoreAsync(copy, cancellationToken).ConfigureAwait(false);
            Current = copy;
            snapshot = copy;
        }
        finally
        {
            _gate.Release();
        }

        Changed?.Invoke(this, snapshot);
    }

    internal static Dictionary<string, string> Serialize(AppSettings settings) =>
        Properties.ToDictionary(
            p => KeyPrefix + p.Name,
            p => JsonSerializer.Serialize(p.GetValue(settings), p.PropertyType, JsonOptions),
            StringComparer.Ordinal);

    private async Task SaveCoreAsync(AppSettings settings, CancellationToken cancellationToken)
    {
        try
        {
            await store.SetManyAsync(Serialize(settings), cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "Ayarlar kaydedilemedi.");
            throw;
        }
    }
}
