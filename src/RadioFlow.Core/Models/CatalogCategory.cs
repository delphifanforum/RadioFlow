namespace RadioFlow.Core.Models;

/// <summary>Ülke veya tür (etiket) listesi öğesi.</summary>
/// <param name="Key">Filtre değeri (ülke kodu veya etiket adı).</param>
/// <param name="DisplayName">Gösterilecek ad.</param>
/// <param name="StationCount">Katalogdaki istasyon sayısı.</param>
public sealed record CatalogCategory(string Key, string DisplayName, int StationCount);
