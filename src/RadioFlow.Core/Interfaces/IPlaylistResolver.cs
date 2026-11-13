namespace RadioFlow.Core.Interfaces;

/// <summary>M3U / PLS çalma listelerini gerçek yayın adresine çözer.</summary>
public interface IPlaylistResolver
{
    Task<Uri> ResolveAsync(Uri url, CancellationToken cancellationToken = default);
}
