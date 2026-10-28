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
            shipment.MarkFailed("The request was cancelled before the carrier responded.");
            await _context.SaveChangesAsync(CancellationToken.None);
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Carrier {Carrier} failed to create shipment {ShipmentId}", request.Carrier, shipment.Id);
            carrierResponse = CarrierShipmentResponse.Failure($"The {request.Carrier} carrier request failed.");
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
            shipment.MarkFailed(failureReason.Length > ShipmentFieldLengths.FailureReason
                ? failureReason[..ShipmentFieldLengths.FailureReason]
                : failureReason);
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
            FailureReason = shipment.FailureReason
        };
    }

    private static Address MapAddress(AddressDto dto) =>
        Address.Create(
            dto.Name,
            dto.Street1,
            dto.Street2,
            dto.City,
            dto.State,
            dto.PostalCode,
            dto.CountryCode);

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
