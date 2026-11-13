using RadioFlow.Core.Models;

namespace RadioFlow.Core.Interfaces;

/// <summary>Radio Browser gibi uzak istasyon kataloğu.</summary>
public interface IRadioCatalogService
{
    Task<IReadOnlyList<RadioStation>> SearchAsync(StationQuery query, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<RadioStation>> GetPopularAsync(string? countryCode, int limit, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<CatalogCategory>> GetCountriesAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<CatalogCategory>> GetTagsAsync(int limit, CancellationToken cancellationToken = default);

    /// <summary>Radio Browser'ın önerdiği "tıklama" bildirimi. Hata durumunda sessizce yutulur.</summary>
    Task RegisterClickAsync(string externalId, CancellationToken cancellationToken = default);
}

/// <summary>Katalog erişilemediğinde fırlatılır; Message kullanıcıya gösterilebilir.</summary>
public sealed class CatalogUnavailableException : Exception
{
    public CatalogUnavailableException()
    {
    }

    public CatalogUnavailableException(string message)
        : base(message)
    {
    }

    public CatalogUnavailableException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
