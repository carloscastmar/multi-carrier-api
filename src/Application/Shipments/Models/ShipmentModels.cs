using MultiCarrierApi.Domain.Enums;

namespace MultiCarrierApi.Application.Shipments.Models;

public record AddressDto
{
    public string Name { get; init; } = string.Empty;

    public string Street1 { get; init; } = string.Empty;

    public string? Street2 { get; init; }

    public string City { get; init; } = string.Empty;

    public string State { get; init; } = string.Empty;

    public string PostalCode { get; init; } = string.Empty;

    public string CountryCode { get; init; } = string.Empty;
}

public record WeightDto
{
    public decimal Value { get; init; }

    public WeightUnit Unit { get; init; }
}

public record DimensionsDto
{
    public decimal Length { get; init; }

    public decimal Width { get; init; }

    public decimal Height { get; init; }

    public DimensionUnit Unit { get; init; }
}

public record PackageDto
{
    public WeightDto Weight { get; init; } = null!;

    public DimensionsDto Dimensions { get; init; } = null!;

    public string? Description { get; init; }
}

public record ShipmentRequest
{
    public CarrierType Carrier { get; init; }

    public AddressDto ShipFrom { get; init; } = null!;

    public AddressDto ShipTo { get; init; } = null!;

    public IReadOnlyList<PackageDto> Packages { get; init; } = [];

    public string? ServiceLevel { get; init; }
}

public record CarrierError
{
    public string Code { get; init; } = string.Empty;

    public string Message { get; init; } = string.Empty;

    public string? Field { get; init; }
}

public record ShipmentErrorDto
{
    public string Code { get; init; } = string.Empty;

    public string Message { get; init; } = string.Empty;

    public string? Field { get; init; }
}

public record CarrierShipmentResponse
{
    public bool Succeeded { get; init; }

    public string? TrackingNumber { get; init; }

    public string? LabelData { get; init; }

    public LabelFormat LabelFormat { get; init; }

    public string? CarrierReference { get; init; }

    public string? FailureReason { get; init; }

    public IReadOnlyList<CarrierError> Errors { get; init; } = [];

    public static CarrierShipmentResponse Success(
        string trackingNumber,
        string labelData,
        LabelFormat labelFormat,
        string carrierReference) =>
        new()
        {
            Succeeded = true,
            TrackingNumber = trackingNumber,
            LabelData = labelData,
            LabelFormat = labelFormat,
            CarrierReference = carrierReference
        };

    public static CarrierShipmentResponse Failure(string failureReason, IReadOnlyList<CarrierError> errors) =>
        new()
        {
            Succeeded = false,
            FailureReason = failureReason,
            Errors = errors
        };
}

public record CreateShipmentResult
{
    public bool Succeeded { get; init; }

    public int ShipmentId { get; init; }

    public CarrierType Carrier { get; init; }

    public string? TrackingNumber { get; init; }

    public string? LabelData { get; init; }

    public LabelFormat? LabelFormat { get; init; }

    public string? CarrierReference { get; init; }

    public string? FailureReason { get; init; }

    public IReadOnlyList<ShipmentErrorDto> Errors { get; init; } = [];
}
