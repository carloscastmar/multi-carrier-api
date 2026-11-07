using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Options;
using MultiCarrierApi.Application.Common.Interfaces;
using MultiCarrierApi.Application.Shipments.Models;
using MultiCarrierApi.Domain.Enums;

namespace MultiCarrierApi.Infrastructure.Carriers.Ups;

public class UpsShipmentAdapter : ICarrierShipmentAdapter
{
    public const string HttpClientName = "Ups";

    private const string ShipPath = "/api/shipments/v2409/ship";
    private const string DefaultServiceCode = "03";

    private static readonly Dictionary<string, string> ServiceCodes = new()
    {
        ["nextdayair"] = "01",
        ["seconddayair"] = "02",
        ["ground"] = "03",
        ["3dayselect"] = "12",
        ["nextdayairsaver"] = "13",
        ["nextdayairearlyam"] = "14",
        ["seconddayairam"] = "59"
    };

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IOptionsMonitor<CarriersOptions> _options;
    private readonly UpsTokenProvider _tokenProvider;

    public UpsShipmentAdapter(
        IHttpClientFactory httpClientFactory,
        IOptionsMonitor<CarriersOptions> options,
        UpsTokenProvider tokenProvider)
    {
        _httpClientFactory = httpClientFactory;
        _options = options;
        _tokenProvider = tokenProvider;
    }

    public CarrierType Carrier => CarrierType.Ups;

    public async Task<CarrierShipmentResponse> CreateShipmentAsync(
        ShipmentRequest request,
        CancellationToken cancellationToken)
    {
        if (!TryResolveServiceCode(request.ServiceLevel, out var serviceCode))
        {
            return CarrierShipmentResponse.Failure(
                "UPS does not support the requested service level.",
                [
                    new CarrierError
                    {
                        Code = "invalid_service_level",
                        Message = $"Service level '{request.ServiceLevel}' is not supported for UPS.",
                        Field = "ServiceLevel"
                    }
                ]);
        }

        var options = _options.CurrentValue.Ups;
        var token = await _tokenProvider.GetAccessTokenAsync(cancellationToken);

        using var httpRequest = new HttpRequestMessage(
            HttpMethod.Post, $"{options.ApiUrl!.TrimEnd('/')}{ShipPath}")
        {
            Content = JsonContent.Create(BuildBody(request, serviceCode, options.AccountNumber!))
        };
        httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        httpRequest.Headers.Add("transactionSrc", "multi-carrier-api");

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
            return CarrierShipmentResponse.Failure("UPS rejected the shipment.", errors);
        }

        response.EnsureSuccessStatusCode();
        throw new HttpRequestException($"Unexpected UPS response {(int)response.StatusCode}.");
    }

    private static bool TryResolveServiceCode(string? serviceLevel, out string code)
    {
        if (string.IsNullOrWhiteSpace(serviceLevel))
        {
            code = DefaultServiceCode;
            return true;
        }

        var normalised = new string(serviceLevel.Where(char.IsLetterOrDigit).ToArray()).ToLowerInvariant();

        if (ServiceCodes.TryGetValue(normalised, out code!))
        {
            return true;
        }

        if (ServiceCodes.ContainsValue(normalised))
        {
            code = normalised;
            return true;
        }

        return false;
    }

    private static JsonObject BuildBody(ShipmentRequest request, string serviceCode, string accountNumber)
    {
        var shipper = BuildParty(request.ShipFrom);
        shipper["ShipperNumber"] = accountNumber;

        return new JsonObject
        {
            ["ShipmentRequest"] = new JsonObject
            {
                ["Request"] = new JsonObject { ["RequestOption"] = "nonvalidate" },
                ["Shipment"] = new JsonObject
                {
                    ["Shipper"] = shipper,
                    ["ShipFrom"] = BuildParty(request.ShipFrom),
                    ["ShipTo"] = BuildParty(request.ShipTo),
                    ["PaymentInformation"] = new JsonObject
                    {
                        ["ShipmentCharge"] = new JsonObject
                        {
                            ["Type"] = "01",
                            ["BillShipper"] = new JsonObject { ["AccountNumber"] = accountNumber }
                        }
                    },
                    ["Service"] = new JsonObject { ["Code"] = serviceCode },
                    ["Package"] = new JsonArray(request.Packages.Select(BuildPackage).ToArray())
                },
                ["LabelSpecification"] = new JsonObject
                {
                    ["LabelImageFormat"] = new JsonObject { ["Code"] = "ZPL" },
                    ["LabelStockSize"] = new JsonObject { ["Height"] = "6", ["Width"] = "4" }
                }
            }
        };
    }

    private static JsonObject BuildParty(AddressDto address)
    {
        var lines = new JsonArray(address.Street1);
        if (!string.IsNullOrWhiteSpace(address.Street2))
        {
            lines.Add(address.Street2);
        }

        return new JsonObject
        {
            ["Name"] = address.Name,
            ["Address"] = new JsonObject
            {
                ["AddressLine"] = lines,
                ["City"] = address.City,
                ["StateProvinceCode"] = address.State,
                ["PostalCode"] = address.PostalCode,
                ["CountryCode"] = address.CountryCode
            }
        };
    }

    private static JsonObject BuildPackage(PackageDto package) => new()
    {
        ["Description"] = package.Description,
        ["Packaging"] = new JsonObject { ["Code"] = "02" },
        ["Dimensions"] = new JsonObject
        {
            ["UnitOfMeasurement"] = new JsonObject
            {
                ["Code"] = package.Dimensions.Unit == DimensionUnit.Cm ? "CM" : "IN"
            },
            ["Length"] = Format(package.Dimensions.Length),
            ["Width"] = Format(package.Dimensions.Width),
            ["Height"] = Format(package.Dimensions.Height)
        },
        ["PackageWeight"] = new JsonObject
        {
            ["UnitOfMeasurement"] = new JsonObject
            {
                ["Code"] = package.Weight.Unit == WeightUnit.Kg ? "KGS" : "LBS"
            },
            ["Weight"] = Format(package.Weight.Value)
        }
    };

    private static string Format(decimal value) => value.ToString(CultureInfo.InvariantCulture);

    private static CarrierShipmentResponse ParseSuccess(string content)
    {
        var results = JsonNode.Parse(content)!["ShipmentResponse"]!["ShipmentResults"]!;
        var identificationNumber = results["ShipmentIdentificationNumber"]!.GetValue<string>();
        var firstPackage = results["PackageResults"] is JsonArray packages
            ? packages[0]!
            : results["PackageResults"]!;

        return CarrierShipmentResponse.Success(
            trackingNumber: firstPackage["TrackingNumber"]?.GetValue<string>() ?? identificationNumber,
            labelData: firstPackage["ShippingLabel"]!["GraphicImage"]!.GetValue<string>(),
            labelFormat: LabelFormat.Zpl,
            carrierReference: identificationNumber);
    }

    private static bool TryParseErrors(string content, out IReadOnlyList<CarrierError> errors)
    {
        errors = [];
        try
        {
            if (JsonNode.Parse(content)?["response"]?["errors"] is not JsonArray array || array.Count == 0)
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
