using MultiCarrierApi.Application.Common.Interfaces;
using MultiCarrierApi.Application.Shipments.Models;
using MultiCarrierApi.Domain.Constants;
using MultiCarrierApi.Domain.Entities;
using MultiCarrierApi.Domain.Enums;
using MultiCarrierApi.Domain.ValueObjects;
using Microsoft.Extensions.Logging;

namespace MultiCarrierApi.Application.Shipments.Commands.CreateShipment;

public record CreateShipmentCommand : IRequest<CreateShipmentResult>
{
    public CarrierType Carrier { get; init; }

    public AddressDto ShipFrom { get; init; } = null!;

    public AddressDto ShipTo { get; init; } = null!;

    public IReadOnlyList<PackageDto> Packages { get; init; } = [];

    public string? ServiceLevel { get; init; }
}

public class CreateShipmentCommandHandler : IRequestHandler<CreateShipmentCommand, CreateShipmentResult>
{
    public const string CarrierRequestFailedCode = "carrier_request_failed";
    public const string RequestCancelledCode = "request_cancelled";

    private readonly IApplicationDbContext _context;
    private readonly ICarrierAdapterFactory _carrierAdapterFactory;
    private readonly ILogger<CreateShipmentCommandHandler> _logger;

    public CreateShipmentCommandHandler(
        IApplicationDbContext context,
        ICarrierAdapterFactory carrierAdapterFactory,
        ILogger<CreateShipmentCommandHandler> logger)
    {
        _context = context;
        _carrierAdapterFactory = carrierAdapterFactory;
        _logger = logger;
    }

    public async Task<CreateShipmentResult> Handle(CreateShipmentCommand request, CancellationToken cancellationToken)
    {
        var shipFrom = MapAddress(request.ShipFrom);
        var shipTo = MapAddress(request.ShipTo);
        var packages = request.Packages.Select(MapPackage).ToList();

        var shipment = Shipment.Create(
            request.Carrier,
            shipFrom,
            shipTo,
            packages,
            request.ServiceLevel);

        // Resolve the adapter before persisting so an unsupported carrier never leaves a Pending row behind.
        var adapter = _carrierAdapterFactory.GetAdapter(request.Carrier);

        _context.Shipments.Add(shipment);
        await _context.SaveChangesAsync(cancellationToken);

        using var logScope = _logger.BeginScope(new Dictionary<string, object> { ["ShipmentId"] = shipment.Id });

        CarrierShipmentResponse carrierResponse;

        try
        {
            carrierResponse = await adapter.CreateShipmentAsync(
                new ShipmentRequest
                {
                    Carrier = request.Carrier,
                    ShipFrom = request.ShipFrom,
                    ShipTo = request.ShipTo,
                    Packages = request.Packages,
                    ServiceLevel = request.ServiceLevel
                },
                cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            const string cancelledMessage = "The request was cancelled before the carrier responded.";
            shipment.MarkFailed(cancelledMessage, [ShipmentError.Create(RequestCancelledCode, cancelledMessage)]);
            await _context.SaveChangesAsync(CancellationToken.None);
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Carrier {Carrier} failed to create shipment {ShipmentId}", request.Carrier, shipment.Id);
            var failureMessage = $"The {request.Carrier} carrier request failed.";
            carrierResponse = CarrierShipmentResponse.Failure(
                failureMessage,
                [new CarrierError { Code = CarrierRequestFailedCode, Message = failureMessage }]);
        }

        if (carrierResponse.Succeeded)
        {
            shipment.MarkCreated(
                carrierResponse.TrackingNumber!,
                carrierResponse.LabelData!,
                carrierResponse.LabelFormat,
                carrierResponse.CarrierReference!);
        }
        else
        {
            var failureReason = carrierResponse.FailureReason ?? "Carrier rejected the shipment request.";
            shipment.MarkFailed(
                Truncate(failureReason, ShipmentFieldLengths.FailureReason),
                carrierResponse.Errors.Select(MapError));
        }

        await _context.SaveChangesAsync(cancellationToken);

        return new CreateShipmentResult
        {
            Succeeded = carrierResponse.Succeeded,
            ShipmentId = shipment.Id,
            Carrier = shipment.Carrier,
            TrackingNumber = shipment.TrackingNumber,
            LabelData = shipment.LabelData,
            LabelFormat = shipment.LabelFormat,
            CarrierReference = shipment.CarrierReference,
            FailureReason = shipment.FailureReason,
            Errors = shipment.Errors
                .Select(e => new ShipmentErrorDto { Code = e.Code, Message = e.Message, Field = e.Field })
                .ToList()
        };
    }

    private static ShipmentError MapError(CarrierError error) =>
        ShipmentError.Create(
            Truncate(error.Code, ShipmentFieldLengths.ErrorCode),
            Truncate(error.Message, ShipmentFieldLengths.ErrorMessage),
            error.Field is null ? null : Truncate(error.Field, ShipmentFieldLengths.ErrorField));

    private static string Truncate(string value, int maxLength) =>
        value.Length > maxLength ? value[..maxLength] : value;

    private static Address MapAddress(AddressDto dto) =>
        Address.Create(
            dto.Name,
            dto.Street1,
            dto.Street2,
            dto.City,
            dto.State,
            dto.PostalCode,
            dto.CountryCode,
            dto.Phone);

    private static Package MapPackage(PackageDto dto) =>
        Package.Create(
            Weight.Create(dto.Weight.Value, dto.Weight.Unit),
            Dimensions.Create(
                dto.Dimensions.Length,
                dto.Dimensions.Width,
                dto.Dimensions.Height,
                dto.Dimensions.Unit),
            dto.Description);
}
