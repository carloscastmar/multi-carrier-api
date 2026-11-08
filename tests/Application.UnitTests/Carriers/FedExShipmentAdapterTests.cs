using System.Net;
using System.Text.Json.Nodes;
using MultiCarrierApi.Application.Shipments.Models;
using MultiCarrierApi.Domain.Enums;
using MultiCarrierApi.Infrastructure.Carriers;
using MultiCarrierApi.Infrastructure.Carriers.FedEx;

namespace MultiCarrierApi.Application.UnitTests.Carriers;

public class FedExShipmentAdapterTests
{
    private const string TokenJson = """{"access_token":"tok-1","token_type":"bearer","expires_in":3599}""";

    private const string SuccessJson = """
        {"output":{"transactionShipments":[{"masterTrackingNumber":"794999999999",
        "pieceResponses":[{"trackingNumber":"794999999999","packageDocuments":[{"encodedLabel":"WlBM"}]}]}]}}
        """;

    private static readonly ShipmentRequest Request = new()
    {
        Carrier = CarrierType.FedEx,
        ShipFrom = new AddressDto { Name = "A", Street1 = "1 Main", City = "Memphis", State = "TN", PostalCode = "38117", CountryCode = "US", Phone = "9015550100" },
        ShipTo = new AddressDto { Name = "B", Street1 = "2 Main", Street2 = "Apt 3", City = "New York", State = "NY", PostalCode = "10001", CountryCode = "US", Phone = "2125550100" },
        Packages =
        [
            new PackageDto
            {
                Weight = new WeightDto { Value = 2.5m, Unit = WeightUnit.Kg },
                Dimensions = new DimensionsDto { Length = 10, Width = 5, Height = 4, Unit = DimensionUnit.Cm }
            }
        ]
    };

    private FakeHttpHandler _handler = null!;
    private FedExShipmentAdapter _adapter = null!;

    [SetUp]
    public void SetUp()
    {
        _handler = new FakeHttpHandler();
        var options = new TestCarriersOptions(new CarriersOptions
        {
            FedEx = new CarrierOptions
            {
                UseMock = false,
                ApiUrl = "https://fedex.test",
                AccountNumber = "123456789",
                ClientId = "id",
                ClientSecret = "secret"
            }
        });
        var factory = new FakeHttpClientFactory(_handler);
        var tokens = new FedExTokenProvider(factory, options, TimeProvider.System);
        _adapter = new FedExShipmentAdapter(factory, options, tokens);
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
        result.TrackingNumber.ShouldBe("794999999999");
        result.LabelData.ShouldBe("WlBM");
        result.LabelFormat.ShouldBe(LabelFormat.Zpl);
        result.CarrierReference.ShouldBe("794999999999");
    }

    [Test]
    public async Task ShouldSendTokenRequestAndMapShipmentRequest()
    {
        _handler.Enqueue(HttpStatusCode.OK, TokenJson);
        _handler.Enqueue(HttpStatusCode.OK, SuccessJson);

        await _adapter.CreateShipmentAsync(Request, CancellationToken.None);

        var token = _handler.Requests[0];
        token.Uri.ShouldBe("https://fedex.test/oauth/token");
        token.Body.ShouldContain("grant_type=client_credentials");
        token.Body.ShouldContain("client_id=id");
        token.Body.ShouldContain("client_secret=secret");

        var ship = _handler.Requests[1];
        ship.Uri.ShouldBe("https://fedex.test/ship/v1/shipments");
        ship.Authorization.ShouldBe("Bearer tok-1");

        var body = JsonNode.Parse(ship.Body)!;
        body["accountNumber"]!["value"]!.GetValue<string>().ShouldBe("123456789");
        var shipment = body["requestedShipment"]!;
        shipment["serviceType"]!.GetValue<string>().ShouldBe("FEDEX_GROUND");
        shipment["labelSpecification"]!["imageType"]!.GetValue<string>().ShouldBe("ZPLII");
        shipment["shipper"]!["contact"]!["phoneNumber"]!.GetValue<string>().ShouldBe("9015550100");
        shipment["recipients"]!.AsArray()[0]!["address"]!["streetLines"]!.AsArray().Count.ShouldBe(2);
        var package = shipment["requestedPackageLineItems"]!.AsArray()[0]!;
        package["weight"]!["units"]!.GetValue<string>().ShouldBe("KG");
        package["dimensions"]!["units"]!.GetValue<string>().ShouldBe("CM");
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

    [TestCase("Priority Overnight", "PRIORITY_OVERNIGHT")]
    [TestCase("2Day", "FEDEX_2_DAY")]
    [TestCase("FEDEX_EXPRESS_SAVER", "FEDEX_EXPRESS_SAVER")]
    [TestCase("Ground Home Delivery", "GROUND_HOME_DELIVERY")]
    public async Task ShouldMapServiceLevel(string serviceLevel, string expected)
    {
        _handler.Enqueue(HttpStatusCode.OK, TokenJson);
        _handler.Enqueue(HttpStatusCode.OK, SuccessJson);

        await _adapter.CreateShipmentAsync(Request with { ServiceLevel = serviceLevel }, CancellationToken.None);

        JsonNode.Parse(_handler.Requests[1].Body)!["requestedShipment"]!["serviceType"]!
            .GetValue<string>().ShouldBe(expected);
    }

    [Test]
    public async Task ShouldFailForUnknownServiceLevelWithoutCallingFedEx()
    {
        var result = await _adapter.CreateShipmentAsync(Request with { ServiceLevel = "Teleport" }, CancellationToken.None);

        result.Succeeded.ShouldBeFalse();
        result.Errors.Single().Field.ShouldBe("ServiceLevel");
        _handler.Requests.ShouldBeEmpty();
    }

    [Test]
    public async Task ShouldOmitPhoneWhenNotProvided()
    {
        _handler.Enqueue(HttpStatusCode.OK, TokenJson);
        _handler.Enqueue(HttpStatusCode.OK, SuccessJson);
        var request = Request with { ShipTo = Request.ShipTo with { Phone = null } };

        await _adapter.CreateShipmentAsync(request, CancellationToken.None);

        JsonNode.Parse(_handler.Requests[1].Body)!["requestedShipment"]!["recipients"]!.AsArray()[0]!["contact"]!["phoneNumber"]
            .ShouldBeNull();
    }

    [Test]
    public async Task ShouldReturnFailureWhenFedExRejectsTheShipment()
    {
        _handler.Enqueue(HttpStatusCode.OK, TokenJson);
        _handler.Enqueue(HttpStatusCode.BadRequest,
            """{"errors":[{"code":"RECIPIENTS.CONTACT.PHONENUMBER.REQUIRED","message":"Recipient phone number is required."}]}""");

        var result = await _adapter.CreateShipmentAsync(Request, CancellationToken.None);

        result.Succeeded.ShouldBeFalse();
        result.Errors.Single().Code.ShouldBe("RECIPIENTS.CONTACT.PHONENUMBER.REQUIRED");
    }

    [Test]
    public async Task ShouldThrowAndRefreshTokenOnUnauthorized()
    {
        _handler.Enqueue(HttpStatusCode.OK, TokenJson);
        _handler.Enqueue(HttpStatusCode.Unauthorized, """{"errors":[{"code":"NOT.AUTHORIZED.ERROR","message":"Access token expired."}]}""");
        _handler.Enqueue(HttpStatusCode.OK, TokenJson);
        _handler.Enqueue(HttpStatusCode.OK, SuccessJson);

        await Should.ThrowAsync<HttpRequestException>(
            () => _adapter.CreateShipmentAsync(Request, CancellationToken.None));
        var retry = await _adapter.CreateShipmentAsync(Request, CancellationToken.None);

        retry.Succeeded.ShouldBeTrue();
        _handler.Requests.Count(r => r.Uri.EndsWith("/oauth/token")).ShouldBe(2);
    }
}
