using RadioFlow.Core.Interfaces;

namespace RadioFlow.Tests.Fakes;

internal sealed class PassThroughPlaylistResolver : IPlaylistResolver
{
    public Task<Uri> ResolveAsync(Uri url, CancellationToken cancellationToken = default) => Task.FromResult(url);
}
