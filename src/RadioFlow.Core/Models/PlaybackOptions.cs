namespace RadioFlow.Core.Models;

/// <summary>Bağlantı ve yeniden bağlanma ayarları.</summary>
public sealed record PlaybackOptions
{
    /// <summary>Bağlantı koptuğunda yapılacak en fazla yeniden deneme sayısı.</summary>
    public int MaxReconnectAttempts { get; init; } = 5;

    /// <summary>İlk yeniden deneme gecikmesi; her denemede iki katına çıkar.</summary>
    public TimeSpan InitialReconnectDelay { get; init; } = TimeSpan.FromSeconds(2);

    public TimeSpan MaxReconnectDelay { get; init; } = TimeSpan.FromSeconds(30);

    /// <summary>Tek bağlantı denemesi için çalmaya başlama zaman aşımı.</summary>
    public TimeSpan ConnectTimeout { get; init; } = TimeSpan.FromSeconds(20);

    /// <summary>Bu süreden uzun duraklatılan canlı yayın devam ettirilirken yeniden bağlanılır.</summary>
    public TimeSpan LivePauseReconnectThreshold { get; init; } = TimeSpan.FromSeconds(30);

    public TimeSpan GetReconnectDelay(int attempt)
    {
        if (attempt <= 1)
        {
            return InitialReconnectDelay;
        }

        var factor = Math.Pow(2, Math.Min(attempt - 1, 10));
        var ms = InitialReconnectDelay.TotalMilliseconds * factor;
        return TimeSpan.FromMilliseconds(Math.Min(ms, MaxReconnectDelay.TotalMilliseconds));
    }
}
