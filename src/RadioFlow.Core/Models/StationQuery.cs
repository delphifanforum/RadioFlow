namespace RadioFlow.Core.Models;

/// <summary>Katalog arama ölçütleri.</summary>
public sealed record StationQuery
{
    public string? Name { get; init; }
    public string? CountryCode { get; init; }
    public string? Tag { get; init; }
    public int Limit { get; init; } = Constants.AppConstants.DefaultSearchLimit;
    public int Offset { get; init; }
    public StationOrder Order { get; init; } = StationOrder.ClickCount;
}

public enum StationOrder
{
    ClickCount,
    Votes,
    Name,
}
