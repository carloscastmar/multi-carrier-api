using System.Net;
using System.Text;
using System.Text.Json.Nodes;
using MultiCarrierApi.Application.Shipments.Models;
using MultiCarrierApi.Domain.Enums;
using MultiCarrierApi.Infrastructure.Carriers;
using MultiCarrierApi.Infrastructure.Carriers.Ups;
using Microsoft.Extensions.Options;

namespace MultiCarrierApi.Application.UnitTests.Carriers;

public class UpsShipmentAdapterTests
{
    private const string TokenJson = """{"access_token":"tok-1","expires_in":"14399"}""";

    private const string SuccessJson = """
        {"ShipmentResponse":{"ShipmentResults":{"ShipmentIdentificationNumber":"1Z999AA10123456784",
        "PackageResults":[{"TrackingNumber":"1Z999AA10123456784","ShippingLabel":{"GraphicImage":"WlBM"}}]}}}
        """;

    private static readonly ShipmentRequest Request = new()
    {
        Carrier = CarrierType.Ups,
        ShipFrom = new AddressDto { Name = "A", Street1 = "1 Main", City = "Atlanta", State = "GA", PostalCode = "30301", CountryCode = "US" },
        ShipTo = new AddressDto { Name = "B", Street1 = "2 Main", Street2 = "Apt 3", City = "New York", State = "NY", PostalCode = "10001", CountryCode = "US" },
        Packages =
        [
            new PackageDto
            {
                Weight = new WeightDto { Value = 2.5m, Unit = WeightUnit.Kg },
                Dimensions = new DimensionsDto { Length = 10, Width = 5, Height = 4, Unit = DimensionUnit.Cm }
            }
        ]
    };

    private FakeHandler _handler = null!;
    private UpsShipmentAdapter _adapter = null!;

    [SetUp]
    public void SetUp()
    {
        _handler = new FakeHandler();
        var options = new TestOptions(new CarriersOptions
        {
            Ups = new CarrierOptions
            {
                UseMock = false,
                ApiUrl = "https://ups.test",
                AccountNumber = "A1B2C3",
                ClientId = "id",
                ClientSecret = "secret"
            }
        });
        var factory = new FakeFactory(_handler);
        var tokens = new UpsTokenProvider(factory, options, TimeProvider.System);
        _adapter = new UpsShipmentAdapter(factory, options, tokens);
    }

    [TearDown]
    public void TearDown() => _handler.Dispose();

    [Test]
    public async Task ShouldMapSuccessfulResponse()
    {
        _handler.Enqueue(HttpStatusCode.OK, TokenJson);
        _handler.Enqueue(HttpStatusCode.OK, SuccessJson);

        var result = await _adapter.CreateShipmentAsync(Request, CancellationToken.None);

        result.Succeeded.ShouldBeTrue();
        result.TrackingNumber.ShouldBe("1Z999AA10123456784");
        result.LabelData.ShouldBe("WlBM");
        result.LabelFormat.ShouldBe(LabelFormat.Zpl);
        result.CarrierReference.ShouldBe("1Z999AA10123456784");
    }

    [Test]
    public async Task ShouldSendBearerTokenAndMapRequest()
    {
        _handler.Enqueue(HttpStatusCode.OK, TokenJson);
        _handler.Enqueue(HttpStatusCode.OK, SuccessJson);

        await _adapter.CreateShipmentAsync(Request, CancellationToken.None);

        var token = _handler.Requests[0];
        token.Uri.ShouldBe("https://ups.test/security/v1/oauth/token");
        token.Authorization.ShouldBe("Basic " + Convert.ToBase64String("id:secret"u8.ToArray()));

        var ship = _handler.Requests[1];
        ship.Uri.ShouldBe("https://ups.test/api/shipments/v2409/ship");
        ship.Authorization.ShouldBe("Bearer tok-1");

        var shipment = JsonNode.Parse(ship.Body)!["ShipmentRequest"]!["Shipment"]!;
        shipment["Shipper"]!["ShipperNumber"]!.GetValue<string>().ShouldBe("A1B2C3");
        shipment["Service"]!["Code"]!.GetValue<string>().ShouldBe("03");
        shipment["ShipTo"]!["Address"]!["AddressLine"]!.AsArray().Count.ShouldBe(2);
        var package = shipment["Package"]!.AsArray()[0]!;
        package["PackageWeight"]!["UnitOfMeasurement"]!["Code"]!.GetValue<string>().ShouldBe("KGS");
        package["PackageWeight"]!["Weight"]!.GetValue<string>().ShouldBe("2.5");
        package["Dimensions"]!["UnitOfMeasurement"]!["Code"]!.GetValue<string>().ShouldBe("CM");
    }

    [Test]
    public async Task ShouldReuseCachedToken()
    {
        _handler.Enqueue(HttpStatusCode.OK, TokenJson);
        _handler.Enqueue(HttpStatusCode.OK, SuccessJson);
        _handler.Enqueue(HttpStatusCode.OK, SuccessJson);

        await _adapter.CreateShipmentAsync(Request, CancellationToken.None);
        await _adapter.CreateShipmentAsync(Request, CancellationToken.None);

        _handler.Requests.Count(r => r.Uri.EndsWith("/oauth/token")).ShouldBe(1);
    }

    [TestCase("Next Day Air", "01")]
    [TestCase("3DaySelect", "12")]
    [TestCase("59", "59")]
    public async Task ShouldMapServiceLevel(string serviceLevel, string expectedCode)
    {
        _handler.Enqueue(HttpStatusCode.OK, TokenJson);
        _handler.Enqueue(HttpStatusCode.OK, SuccessJson);

        await _adapter.CreateShipmentAsync(Request with { ServiceLevel = serviceLevel }, CancellationToken.None);

        JsonNode.Parse(_handler.Requests[1].Body)!["ShipmentRequest"]!["Shipment"]!["Service"]!["Code"]!
            .GetValue<string>().ShouldBe(expectedCode);
    }

    [Test]
    public async Task ShouldFailForUnknownServiceLevelWithoutCallingUps()
    {
        var result = await _adapter.CreateShipmentAsync(Request with { ServiceLevel = "Teleport" }, CancellationToken.None);

        result.Succeeded.ShouldBeFalse();
        result.Errors.Single().Field.ShouldBe("ServiceLevel");
        _handler.Requests.ShouldBeEmpty();
    }

    [Test]
    public async Task ShouldReturnFailureWhenUpsRejectsTheShipment()
    {
        _handler.Enqueue(HttpStatusCode.OK, TokenJson);
        _handler.Enqueue(HttpStatusCode.BadRequest,
            """{"response":{"errors":[{"code":"120100","message":"Missing or invalid shipper number"}]}}""");

        var result = await _adapter.CreateShipmentAsync(Request, CancellationToken.None);

        result.Succeeded.ShouldBeFalse();
        result.Errors.Single().Code.ShouldBe("120100");
        result.Errors.Single().Message.ShouldBe("Missing or invalid shipper number");
    }

    [Test]
    public async Task ShouldThrowAndRefreshTokenOnUnauthorized()
    {
        _handler.Enqueue(HttpStatusCode.OK, TokenJson);
        _handler.Enqueue(HttpStatusCode.Unauthorized, """{"response":{"errors":[{"code":"250002","message":"Invalid Authentication Information"}]}}""");
        _handler.Enqueue(HttpStatusCode.OK, TokenJson);
        _handler.Enqueue(HttpStatusCode.OK, SuccessJson);

        await Should.ThrowAsync<HttpRequestException>(
            () => _adapter.CreateShipmentAsync(Request, CancellationToken.None));
        var retry = await _adapter.CreateShipmentAsync(Request, CancellationToken.None);

        retry.Succeeded.ShouldBeTrue();
        _handler.Requests.Count(r => r.Uri.EndsWith("/oauth/token")).ShouldBe(2);
    }

    private record RecordedRequest(string Uri, string? Authorization, string Body);

    private sealed class FakeHandler : HttpMessageHandler
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

    private sealed class FakeFactory(HttpMessageHandler handler) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new(handler, disposeHandler: false);
    }

    private sealed class TestOptions(CarriersOptions value) : IOptionsMonitor<CarriersOptions>
    {
        public CarriersOptions CurrentValue => value;

        public CarriersOptions Get(string? name) => value;

        public IDisposable? OnChange(Action<CarriersOptions, string?> listener) => null;
    }
}
