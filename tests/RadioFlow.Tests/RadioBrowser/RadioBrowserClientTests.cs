using System.Net;
using Microsoft.Extensions.Logging.Abstractions;
using RadioFlow.Core.Interfaces;
using RadioFlow.Core.Models;
using RadioFlow.Infrastructure.RadioBrowser;
using RadioFlow.Infrastructure.Security;
using RadioFlow.Tests.Fakes;

namespace RadioFlow.Tests.RadioBrowser;

/// <summary>
/// Radio Browser istemcisi sahte HTTP işleyicisiyle test edilir; gerçek internet gerekmez.
/// JSON örneği API'nin /json/stations/search yanıt biçimini temsil eder.
/// </summary>
public sealed class RadioBrowserClientTests
{
    private const string StationsJson = """
        [
          {
            "changeuuid": "x",
            "stationuuid": "9617a958-0601-11e8-ae97-52543be04c81",
            "name": "  Radyo İstanbul  ",
            "url": "http://stream.example.com/radyo.pls",
            "url_resolved": "https://stream.example.com/radyo.mp3",
            "homepage": "https://radyo.example.com/",
            "favicon": "https://radyo.example.com/logo.png",
            "tags": "pop,türkçe pop,haber",
            "country": "Türkiye",
            "countrycode": "tr",
            "state": "İstanbul",
            "language": "turkish",
            "votes": 120,
            "codec": "MP3",
            "bitrate": "128",
            "hls": 0,
            "lastcheckok": 1,
            "clickcount": 5000
          },
          {
            "stationuuid": "11111111-2222-3333-4444-555555555555",
            "name": "Tehlikeli",
            "url": "file:///C:/Windows/win.ini",
            "url_resolved": "file:///C:/Windows/win.ini",
            "favicon": "javascript:alert(1)",
            "tags": "",
            "codec": "MP3",
            "bitrate": 0
          },
          {
            "stationuuid": "22222222-2222-3333-4444-555555555555",
            "name": "Logo Yok",
            "url": "https://ok.example.com/live",
            "url_resolved": "",
            "favicon": "file:///etc/passwd",
            "tags": "jazz",
            "codec": "AAC",
            "bitrate": 64
          }
        ]
        """;

    private static (RadioBrowserClient Client, StubHttpMessageHandler Handler, FixedServerProvider Servers) Create(
        Func<HttpRequestMessage, HttpResponseMessage> responder,
        params string[] servers)
    {
        var handler = new StubHttpMessageHandler(responder);
        var provider = new FixedServerProvider(servers.Length > 0 ? servers : ["test1.api.radio-browser.info"]);
        var client = new RadioBrowserClient(
            new HttpClient(handler),
            provider,
            new StreamUrlValidator(),
            NullLogger<RadioBrowserClient>.Instance);
        return (client, handler, provider);
    }

    // Test 5: Radyo API JSON dönüşümü
    [Fact]
    public async Task Search_MapsJsonToStations_AndDropsUnsafeEntries()
    {
        var (client, _, _) = Create(_ => StubHttpMessageHandler.Json(StationsJson));

        var stations = await client.SearchAsync(new StationQuery { Name = "radyo" }, TestContext.Current.CancellationToken);

        Assert.Equal(2, stations.Count);
        var first = stations[0];
        Assert.Equal("9617a958-0601-11e8-ae97-52543be04c81", first.ExternalId);
        Assert.Equal("Radyo İstanbul", first.Name);
        Assert.Equal("https://stream.example.com/radyo.mp3", first.StreamUrl); // url_resolved tercih edilir
        Assert.Equal("https://radyo.example.com/logo.png", first.LogoUrl);
        Assert.Equal("TR", first.CountryCode);
        Assert.Equal("Türkiye", first.Country);
        Assert.Equal("MP3", first.Codec);
        Assert.Equal(128, first.Bitrate); // string sayı da okunur
        Assert.Equal(new[] { "pop", "türkçe pop", "haber" }, first.TagList.ToArray());
        Assert.Equal("pop", first.PrimaryGenre);
        Assert.False(first.IsCustom);

        var second = stations[1];
        Assert.Equal("https://ok.example.com/live", second.StreamUrl); // url_resolved boşsa url
        Assert.Null(second.LogoUrl); // güvensiz logo atıldı
        Assert.DoesNotContain(stations, s => s.Name == "Tehlikeli");
    }

    [Fact]
    public async Task Search_EncodesTurkishCharactersAndFilters()
    {
        var (client, handler, _) = Create(_ => StubHttpMessageHandler.Json("[]"));

        await client.SearchAsync(
            new StationQuery { Name = "Şarkı Ğüzel İ", CountryCode = "tr", Tag = "türkçe pop", Limit = 50 },
            TestContext.Current.CancellationToken);

        var uri = Assert.Single(handler.Requests);
        Assert.Equal("https", uri.Scheme);
        Assert.Equal("/json/stations/search", uri.AbsolutePath);
        var query = uri.Query;
        Assert.Contains("name=%C5%9Eark%C4%B1%20%C4%9E%C3%BCzel%20%C4%B0", query, StringComparison.Ordinal);
        Assert.Contains("countrycode=TR", query, StringComparison.Ordinal);
        Assert.Contains("tag=t%C3%BCrk%C3%A7e%20pop", query, StringComparison.Ordinal);
        Assert.Contains("hidebroken=true", query, StringComparison.Ordinal);
        Assert.Contains("limit=50", query, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Search_FailsOverToNextServer_OnServerError()
    {
        var (client, handler, servers) = Create(
            req => req.RequestUri!.Host.StartsWith("bad", StringComparison.Ordinal)
                ? new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)
                : StubHttpMessageHandler.Json(StationsJson),
            "bad.api.radio-browser.info",
            "good.api.radio-browser.info");

        var stations = await client.SearchAsync(new StationQuery(), TestContext.Current.CancellationToken);

        Assert.Equal(2, stations.Count);
        Assert.Equal(2, handler.Requests.Count);
        Assert.Equal(new[] { "bad.api.radio-browser.info" }, servers.Failures.ToArray());
    }

    [Fact]
    public async Task Search_WhenAllServersFail_ThrowsCatalogUnavailableWithTurkishMessage()
    {
        var (client, handler, _) = Create(_ => throw new HttpRequestException("no network"));

        var ex = await Assert.ThrowsAsync<CatalogUnavailableException>(
            () => client.SearchAsync(new StationQuery(), TestContext.Current.CancellationToken));

        Assert.Contains("ulaşılamıyor", ex.Message, StringComparison.Ordinal);
        Assert.Equal(RadioBrowserClient.MaxAttempts, handler.Requests.Count); // sınırlı yeniden deneme
    }

    [Fact]
    public async Task GetCountriesAndTags_MapJson()
    {
        var (client, _, _) = Create(req => req.RequestUri!.AbsolutePath switch
        {
            "/json/countries" => StubHttpMessageHandler.Json("""
                [{"name":"Turkey","iso_3166_1":"TR","stationcount":"900"},
                 {"name":"","iso_3166_1":"XX","stationcount":3},
                 {"name":"Germany","iso_3166_1":"DE","stationcount":5000}]
                """),
            _ => StubHttpMessageHandler.Json("""[{"name":"pop","stationcount":12000},{"name":"","stationcount":1}]"""),
        });

        var countries = await client.GetCountriesAsync(TestContext.Current.CancellationToken);
        var tags = await client.GetTagsAsync(100, TestContext.Current.CancellationToken);

        Assert.Equal(2, countries.Count);
        Assert.Equal(new CatalogCategory("TR", "Turkey", 900), countries[0]);
        Assert.Equal(new CatalogCategory("pop", "pop", 12000), Assert.Single(tags));
    }

    [Fact]
    public async Task Search_HonoursCancellation()
    {
        var (client, _, _) = Create(_ => StubHttpMessageHandler.Json("[]"));
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => client.SearchAsync(new StationQuery(), cts.Token));
    }
}
