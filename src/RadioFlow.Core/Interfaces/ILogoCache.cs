namespace RadioFlow.Core.Interfaces;

/// <summary>İstasyon logolarını güvenli şekilde indirip diskte önbelleğe alır.</summary>
public interface ILogoCache
{
    /// <summary>Yerel dosya yolunu döndürür; logo alınamazsa null.</summary>
    Task<string?> GetLogoPathAsync(string? logoUrl, CancellationToken cancellationToken = default);
}
