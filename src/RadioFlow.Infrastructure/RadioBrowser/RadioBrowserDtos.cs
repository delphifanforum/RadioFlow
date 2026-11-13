using System.Text.Json.Serialization;

namespace RadioFlow.Infrastructure.RadioBrowser;

/// <summary>Radio Browser /json/stations/* yanıt öğesi (yalnızca kullanılan alanlar).</summary>
public sealed class RadioBrowserStationDto
{
    [JsonPropertyName("stationuuid")]
    public string? StationUuid { get; set; }

    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("url")]
    public string? Url { get; set; }

    [JsonPropertyName("url_resolved")]
    public string? UrlResolved { get; set; }

    [JsonPropertyName("homepage")]
    public string? Homepage { get; set; }

    [JsonPropertyName("favicon")]
    public string? Favicon { get; set; }

    [JsonPropertyName("tags")]
    public string? Tags { get; set; }

    [JsonPropertyName("country")]
    public string? Country { get; set; }

    [JsonPropertyName("countrycode")]
    public string? CountryCode { get; set; }

    [JsonPropertyName("language")]
    public string? Language { get; set; }

    [JsonPropertyName("codec")]
    public string? Codec { get; set; }

    [JsonPropertyName("bitrate")]
    public int Bitrate { get; set; }

    [JsonPropertyName("votes")]
    public int Votes { get; set; }

    [JsonPropertyName("clickcount")]
    public int ClickCount { get; set; }

    [JsonPropertyName("lastcheckok")]
    public int LastCheckOk { get; set; } = 1;
}

/// <summary>/json/countries yanıt öğesi.</summary>
public sealed class RadioBrowserCountryDto
{
    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("iso_3166_1")]
    public string? Iso3166 { get; set; }

    [JsonPropertyName("stationcount")]
    public int StationCount { get; set; }
}

/// <summary>/json/tags yanıt öğesi.</summary>
public sealed class RadioBrowserTagDto
{
    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("stationcount")]
    public int StationCount { get; set; }
}
