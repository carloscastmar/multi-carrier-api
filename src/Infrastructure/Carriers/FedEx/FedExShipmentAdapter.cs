using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Options;
using MultiCarrierApi.Application.Common.Interfaces;
using MultiCarrierApi.Application.Shipments.Models;
using MultiCarrierApi.Domain.Enums;

namespace MultiCarrierApi.Infrastructure.Carriers.FedEx;

public class FedExShipmentAdapter : ICarrierShipmentAdapter
{
    public const string HttpClientName = "FedEx";

    private const string ShipPath = "/ship/v1/shipments";
    private const string DefaultServiceType = "FEDEX_GROUND";

    private static readonly string[] ServiceTypes =
    [
        "FIRST_OVERNIGHT",
        "PRIORITY_OVERNIGHT",
        "STANDARD_OVERNIGHT",
        "FEDEX_2_DAY_AM",
        "FEDEX_2_DAY",
        "FEDEX_EXPRESS_SAVER",
        "FEDEX_GROUND",
        "GROUND_HOME_DELIVERY"
    ];

    private static readonly Dictionary<string, string> ServiceTypeLookup = BuildServiceTypeLookup();

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IOptionsMonitor<CarriersOptions> _options;
    private readonly FedExTokenProvider _tokenProvider;

    public FedExShipmentAdapter(
        IHttpClientFactory httpClientFactory,
        IOptionsMonitor<CarriersOptions> options,
        FedExTokenProvider tokenProvider)
    {
        _httpClientFactory = httpClientFactory;
        _options = options;
        _tokenProvider = tokenProvider;
    }

    public CarrierType Carrier => CarrierType.FedEx;

    public async Task<CarrierShipmentResponse> CreateShipmentAsync(
        ShipmentRequest request,
        CancellationToken cancellationToken)
    {
        if (!TryResolveServiceType(request.ServiceLevel, out var serviceType))
        {
            return CarrierShipmentResponse.Failure(
                "FedEx does not support the requested service level.",
                [
                    new CarrierError
                    {
                        Code = "invalid_service_level",
                        Message = $"Service level '{request.ServiceLevel}' is not supported for FedEx.",
                        Field = "ServiceLevel"
                    }
                ]);
        }

        var options = _options.CurrentValue.FedEx;
        var token = await _tokenProvider.GetAccessTokenAsync(cancellationToken);

        using var httpRequest = new HttpRequestMessage(
            HttpMethod.Post, $"{options.ApiUrl!.TrimEnd('/')}{ShipPath}")
        {
            Content = JsonContent.Create(BuildBody(request, serviceType, options.AccountNumber!))
        };
        httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        httpRequest.Headers.Add("X-locale", "en_US");

        using var client = _httpClientFactory.CreateClient(HttpClientName);
        using var response = await client.SendAsync(httpRequest, cancellationToken);
        var content = await response.Content.ReadAsStringAsync(cancellationToken);

        if (response.IsSuccessStatusCode)
        {
            return ParseSuccess(content);
        }

        if (response.StatusCode == HttpStatusCode.Unauthorized)
        {
            _tokenProvider.Invalidate();
        }
        else if (response.StatusCode is HttpStatusCode.BadRequest or HttpStatusCode.UnprocessableEntity
                 && TryParseErrors(content, out var errors))
        {
            return CarrierShipmentResponse.Failure("FedEx rejected the shipment.", errors);
        }

        response.EnsureSuccessStatusCode();
        throw new HttpRequestException($"Unexpected FedEx response {(int)response.StatusCode}.");
    }

    private static Dictionary<string, string> BuildServiceTypeLookup()
    {
        var lookup = new Dictionary<string, string>();
        foreach (var serviceType in ServiceTypes)
        {
            var key = Normalise(serviceType);
            lookup[key] = serviceType;
            if (key.StartsWith("fedex", StringComparison.Ordinal))
            {
                lookup[key["fedex".Length..]] = serviceType;
            }
        }

        return lookup;
    }

    private static string Normalise(string value) =>
        new string(value.Where(char.IsLetterOrDigit).ToArray()).ToLowerInvariant();

    private static bool TryResolveServiceType(string? serviceLevel, out string serviceType)
    {
        if (string.IsNullOrWhiteSpace(serviceLevel))
        {
            serviceType = DefaultServiceType;
            return true;
        }

        return ServiceTypeLookup.TryGetValue(Normalise(serviceLevel), out serviceType!);
    }

    private static JsonObject BuildBody(ShipmentRequest request, string serviceType, string accountNumber) => new()
    {
        ["labelResponseOptions"] = "LABEL",
        ["accountNumber"] = new JsonObject { ["value"] = accountNumber },
        ["requestedShipment"] = new JsonObject
        {
            ["shipper"] = BuildParty(request.ShipFrom),
            ["recipients"] = new JsonArray(BuildParty(request.ShipTo)),
            ["serviceType"] = serviceType,
            ["packagingType"] = "YOUR_PACKAGING",
            ["pickupType"] = "USE_SCHEDULED_PICKUP",
            ["shippingChargesPayment"] = new JsonObject { ["paymentType"] = "SENDER" },
            ["labelSpecification"] = new JsonObject
            {
                ["imageType"] = "ZPLII",
                ["labelStockType"] = "STOCK_4X6"
            },
            ["requestedPackageLineItems"] = new JsonArray(request.Packages.Select(BuildPackage).ToArray())
        }
    };

    private static JsonObject BuildParty(AddressDto address)
    {
        var contact = new JsonObject { ["personName"] = address.Name };
        if (!string.IsNullOrWhiteSpace(address.Phone))
        {
            contact["phoneNumber"] = address.Phone;
        }

        var lines = new JsonArray(address.Street1);
        if (!string.IsNullOrWhiteSpace(address.Street2))
        {
            lines.Add(address.Street2);
        }

        return new JsonObject
        {
            ["contact"] = contact,
            ["address"] = new JsonObject
            {
                ["streetLines"] = lines,
                ["city"] = address.City,
                ["stateOrProvinceCode"] = address.State,
                ["postalCode"] = address.PostalCode,
                ["countryCode"] = address.CountryCode
            }
        };
    }

    private static JsonObject BuildPackage(PackageDto package) => new()
    {
        ["weight"] = new JsonObject
        {
            ["units"] = package.Weight.Unit == WeightUnit.Kg ? "KG" : "LB",
            ["value"] = package.Weight.Value
        },
        ["dimensions"] = new JsonObject
        {
            ["length"] = package.Dimensions.Length,
            ["width"] = package.Dimensions.Width,
            ["height"] = package.Dimensions.Height,
            ["units"] = package.Dimensions.Unit == DimensionUnit.Cm ? "CM" : "IN"
        }
    };

    private static CarrierShipmentResponse ParseSuccess(string content)
    {
        var shipment = JsonNode.Parse(content)!["output"]!["transactionShipments"]![0]!;
        var masterTrackingNumber = shipment["masterTrackingNumber"]!.GetValue<string>();
        var firstPiece = shipment["pieceResponses"]![0]!;

        return CarrierShipmentResponse.Success(
            trackingNumber: firstPiece["trackingNumber"]?.GetValue<string>() ?? masterTrackingNumber,
            labelData: firstPiece["packageDocuments"]![0]!["encodedLabel"]!.GetValue<string>(),
            labelFormat: LabelFormat.Zpl,
            carrierReference: masterTrackingNumber);
    }

    private static bool TryParseErrors(string content, out IReadOnlyList<CarrierError> errors)
    {
        errors = [];
        try
        {
            if (JsonNode.Parse(content)?["errors"] is not JsonArray array || array.Count == 0)
            {
                return false;
            }

            errors = array
                .Select(e => new CarrierError
                {
                    Code = e?["code"]?.GetValue<string>() ?? string.Empty,
                    Message = e?["message"]?.GetValue<string>() ?? string.Empty
                })
                .ToList();
            return true;
        }
        catch (System.Text.Json.JsonException)
        {
            return false;
        }
    }
}
