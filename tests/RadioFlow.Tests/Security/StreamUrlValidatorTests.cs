using RadioFlow.Infrastructure.Security;

namespace RadioFlow.Tests.Security;

public sealed class StreamUrlValidatorTests
{
    private readonly StreamUrlValidator _validator = new();

    [Theory]
    [InlineData("http://stream.example.com:8000/live")]
    [InlineData("https://radyo.example.com.tr/stream.mp3")]
    [InlineData("https://xn--rnek-zoa.com/yayın")]
    public void Accepts_HttpAndHttps(string url)
    {
        Assert.True(_validator.TryValidate(url, out var uri, out var error));
        Assert.NotNull(uri);
        Assert.Null(error);
    }

    // Test 6: Hatalı stream URL reddi
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("file:///C:/Windows/system32/config")]
    [InlineData("javascript:alert(1)")]
    [InlineData("data:audio/mp3;base64,AAAA")]
    [InlineData("ftp://example.com/a.mp3")]
    [InlineData("smb://server/share")]
    [InlineData("/relative/path.mp3")]
    [InlineData("\\\\server\\share\\a.mp3")]
    [InlineData("http://")]
    [InlineData("http://exa\nmple.com")]
    public void Rejects_InvalidOrDangerousUrls(string? url)
    {
        Assert.False(_validator.TryValidate(url, out var uri, out var error));
        Assert.Null(uri);
        Assert.False(string.IsNullOrWhiteSpace(error));
    }

    [Theory]
    [InlineData("http://localhost:8080/live")]
    [InlineData("http://127.0.0.1/live")]
    [InlineData("http://192.168.1.10/live")]
    [InlineData("http://10.0.0.5/live")]
    [InlineData("http://172.20.1.1/live")]
    [InlineData("http://169.254.169.254/latest/meta-data")]
    [InlineData("http://[::1]/live")]
    [InlineData("http://printer.local/live")]
    public void Rejects_PrivateHosts_ForCatalogData_ButAllowsForCustomStations(string url)
    {
        Assert.False(_validator.TryValidate(url, out _, out _));
        Assert.True(_validator.TryValidate(url, out _, out _, allowPrivateHosts: true));
    }

    [Fact]
    public void Rejects_TooLongUrl()
    {
        var url = "https://example.com/" + new string('a', 3000);
        Assert.False(_validator.TryValidate(url, out _, out _));
    }
}
