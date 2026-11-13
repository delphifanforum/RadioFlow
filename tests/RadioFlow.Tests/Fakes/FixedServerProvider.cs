using RadioFlow.Infrastructure.RadioBrowser;

namespace RadioFlow.Tests.Fakes;

internal sealed class FixedServerProvider(params string[] servers) : IRadioBrowserServerProvider
{
    public List<string> Failures { get; } = [];

    public Task<IReadOnlyList<string>> GetServersAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<string>>(servers);

    public void ReportFailure(string server) => Failures.Add(server);
}
