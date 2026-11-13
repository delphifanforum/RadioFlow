using System.Net;
using System.Text;

namespace RadioFlow.Tests.Fakes;

/// <summary>Ağa çıkmadan sabit yanıt döndüren HTTP işleyicisi.</summary>
internal sealed class StubHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> responder) : HttpMessageHandler
{
    public List<Uri> Requests { get; } = [];

    public static HttpResponseMessage Json(string json, HttpStatusCode status = HttpStatusCode.OK) =>
        new(status) { Content = new StringContent(json, Encoding.UTF8, "application/json") };

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        lock (Requests)
        {
            Requests.Add(request.RequestUri!);
        }

        var response = responder(request);
        response.RequestMessage ??= request;
        return Task.FromResult(response);
    }
}
