using System.Net;
using System.Text;
using Microsoft.Extensions.Options;
using MultiCarrierApi.Infrastructure.Carriers;

namespace MultiCarrierApi.Application.UnitTests.Carriers;

public record RecordedRequest(string Uri, string? Authorization, string Body);

public sealed class FakeHttpHandler : HttpMessageHandler
{
    private readonly Queue<(HttpStatusCode Status, string Body)> _responses = new();

    public List<RecordedRequest> Requests { get; } = [];

    public void Enqueue(HttpStatusCode status, string body) => _responses.Enqueue((status, body));

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var body = request.Content is null ? string.Empty : await request.Content.ReadAsStringAsync(cancellationToken);
        Requests.Add(new RecordedRequest(request.RequestUri!.ToString(), request.Headers.Authorization?.ToString(), body));
        var (status, responseBody) = _responses.Dequeue();
        return new HttpResponseMessage(status)
        {
            Content = new StringContent(responseBody, Encoding.UTF8, "application/json")
        };
    }
}

public sealed class FakeHttpClientFactory(HttpMessageHandler handler) : IHttpClientFactory
{
    public HttpClient CreateClient(string name) => new(handler, disposeHandler: false);
}

public sealed class TestCarriersOptions(CarriersOptions value) : IOptionsMonitor<CarriersOptions>
{
    public CarriersOptions CurrentValue => value;

    public CarriersOptions Get(string? name) => value;

    public IDisposable? OnChange(Action<CarriersOptions, string?> listener) => null;
}
