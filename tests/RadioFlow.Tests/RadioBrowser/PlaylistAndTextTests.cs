using System.Net;
using System.Text;
using Microsoft.Extensions.Logging.Abstractions;
using RadioFlow.Core.Models;
using RadioFlow.Core.Text;
using RadioFlow.Infrastructure.Audio;
using RadioFlow.Infrastructure.Security;
using RadioFlow.Infrastructure.Services;
using RadioFlow.Tests.Fakes;

namespace RadioFlow.Tests.RadioBrowser;

public sealed class PlaylistAndTextTests
{
    [Fact]
    public void ParseEntries_ReadsM3uAndPls()
    {
        var m3u = "#EXTM3U\n#EXTINF:-1,Radyo\nhttp://a.example.com/live\n\nhttp://b.example.com/live\n";
        Assert.Equal(new[] { "http://a.example.com/live", "http://b.example.com/live" }, PlaylistResolver.ParseEntries(m3u).ToArray());

        var pls = "[playlist]\nNumberOfEntries=2\nFile1=http://c.example.com:8000/\nTitle1=X\nFile2=http://d.example.com/\nVersion=2";
        Assert.Equal(new[] { "http://c.example.com:8000/", "http://d.example.com/" }, PlaylistResolver.ParseEntries(pls).ToArray());
    }

    [Fact]
    public async Task Resolve_SkipsUnsafeEntries_AndReturnsFirstHttpEntry()
    {
        var handler = new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("file:///C:/secret.mp3\nhttps://safe.example.com/live\n", Encoding.UTF8, "audio/x-mpegurl"),
        });
        var resolver = new PlaylistResolver(new HttpClient(handler), new StreamUrlValidator(), NullLogger<PlaylistResolver>.Instance);

        var result = await resolver.ResolveAsync(new Uri("https://radio.example.com/listen.m3u"), TestContext.Current.CancellationToken);

        Assert.Equal(new Uri("https://safe.example.com/live"), result);
    }

    [Fact]
    public async Task Resolve_PlaylistWithOnlyUnsafeEntries_Throws()
    {
        var handler = new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("[playlist]\nFile1=file:///etc/passwd\n", Encoding.UTF8, "audio/x-scpls"),
        });
        var resolver = new PlaylistResolver(new HttpClient(handler), new StreamUrlValidator(), NullLogger<PlaylistResolver>.Instance);

        await Assert.ThrowsAsync<PlaylistResolutionException>(
            () => resolver.ResolveAsync(new Uri("https://radio.example.com/listen.pls"), TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Resolve_NonPlaylistUrl_IsReturnedWithoutNetworkRequest()
    {
        var handler = new StubHttpMessageHandler(_ => throw new InvalidOperationException("ağ çağrılmamalı"));
        var resolver = new PlaylistResolver(new HttpClient(handler), new StreamUrlValidator(), NullLogger<PlaylistResolver>.Instance);
        var uri = new Uri("https://radio.example.com/stream.mp3");

        Assert.Equal(uri, await resolver.ResolveAsync(uri, TestContext.Current.CancellationToken));
        Assert.Empty(handler.Requests);
    }

    [Theory]
    [InlineData("İSTANBUL", "istanbul")]
    [InlineData("Istanbul", "istanbul")]
    [InlineData("ŞARKI Çalıyor", "sarki caliyor")]
    [InlineData("Gönül Öğretmen Üzüm", "gonul ogretmen uzum")]
    [InlineData(null, "")]
    public void Fold_IsTurkishAware(string? input, string expected) => Assert.Equal(expected, TextFolding.Fold(input));

    [Theory]
    [InlineData("Tarkan - Şımarık", "Tarkan", "Şımarık")]
    [InlineData("Sadece Başlık", null, "Sadece Başlık")]
    public void NowPlaying_ParsesIcyTitle(string raw, string? artist, string title)
    {
        var info = NowPlayingInfo.Parse(raw)!;
        Assert.Equal(artist, info.Artist);
        Assert.Equal(title, info.Title);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    [InlineData("-")]
    public void NowPlaying_EmptyMetadata_ProducesNoSongInfo(string? raw) => Assert.Null(NowPlayingInfo.Parse(raw));

    [Fact]
    public void ReconnectDelay_GrowsExponentiallyAndIsCapped()
    {
        var options = new PlaybackOptions { InitialReconnectDelay = TimeSpan.FromSeconds(2), MaxReconnectDelay = TimeSpan.FromSeconds(30) };
        Assert.Equal(TimeSpan.FromSeconds(2), options.GetReconnectDelay(1));
        Assert.Equal(TimeSpan.FromSeconds(4), options.GetReconnectDelay(2));
        Assert.Equal(TimeSpan.FromSeconds(8), options.GetReconnectDelay(3));
        Assert.Equal(TimeSpan.FromSeconds(30), options.GetReconnectDelay(10));
    }

    [Fact]
    public void LogoSignatureCheck_AcceptsRasterAndRejectsOthers()
    {
        byte[] png = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0, 0, 0, 0];
        Assert.True(LogoCache.LooksLikeRasterImage(png));
        Assert.False(LogoCache.LooksLikeRasterImage(Encoding.UTF8.GetBytes("<svg xmlns='http://www.w3.org/2000/svg'></svg>")));
        Assert.False(LogoCache.LooksLikeRasterImage(Encoding.UTF8.GetBytes("<html><script>")));
    }
}
