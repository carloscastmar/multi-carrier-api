using System.Globalization;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Options;

namespace MultiCarrierApi.Infrastructure.Carriers.FedEx;

public class FedExTokenProvider
{
    private static readonly TimeSpan ExpiryMargin = TimeSpan.FromMinutes(1);

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IOptionsMonitor<CarriersOptions> _options;
    private readonly TimeProvider _timeProvider;
    private readonly SemaphoreSlim _lock = new(1, 1);

    private string? _accessToken;
    private DateTimeOffset _expiresAt;

    public FedExTokenProvider(
        IHttpClientFactory httpClientFactory,
        IOptionsMonitor<CarriersOptions> options,
        TimeProvider timeProvider)
    {
        _httpClientFactory = httpClientFactory;
        _options = options;
        _timeProvider = timeProvider;
    }

    public async Task<string> GetAccessTokenAsync(CancellationToken cancellationToken)
    {
        await _lock.WaitAsync(cancellationToken);
        try
        {
            if (_accessToken is not null && _timeProvider.GetUtcNow() < _expiresAt)
            {
                return _accessToken;
            }

            var options = _options.CurrentValue.FedEx;

            using var request = new HttpRequestMessage(
                HttpMethod.Post, $"{options.ApiUrl!.TrimEnd('/')}/oauth/token")
            {
                Content = new FormUrlEncodedContent(
                [
                    new("grant_type", "client_credentials"),
                    new("client_id", options.ClientId ?? string.Empty),
                    new("client_secret", options.ClientSecret ?? string.Empty)
                ])
            };

            using var client = _httpClientFactory.CreateClient(FedExShipmentAdapter.HttpClientName);
            using var response = await client.SendAsync(request, cancellationToken);
            response.EnsureSuccessStatusCode();

            var body = JsonNode.Parse(await response.Content.ReadAsStringAsync(cancellationToken))!;
            _accessToken = body["access_token"]!.GetValue<string>();
            var expiresInSeconds = double.Parse(body["expires_in"]!.ToString(), CultureInfo.InvariantCulture);
            _expiresAt = _timeProvider.GetUtcNow().AddSeconds(expiresInSeconds) - ExpiryMargin;

            return _accessToken;
        }
        finally
        {
            _lock.Release();
        }
    }

    public void Invalidate()
    {
        _accessToken = null;
    }
}
