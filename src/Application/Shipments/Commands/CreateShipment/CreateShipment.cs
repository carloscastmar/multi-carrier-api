using MultiCarrierApi.Application.Common.Interfaces;
using MultiCarrierApi.Application.Shipments.Models;
using MultiCarrierApi.Domain.Entities;
using MultiCarrierApi.Domain.Enums;
using MultiCarrierApi.Domain.ValueObjects;

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

    public CreateShipmentCommandHandler(
        IApplicationDbContext context,
        ICarrierAdapterFactory carrierAdapterFactory)
    {
        _context = context;
        _carrierAdapterFactory = carrierAdapterFactory;
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

        _context.Shipments.Add(shipment);
        await _context.SaveChangesAsync(cancellationToken);

        var adapter = _carrierAdapterFactory.GetAdapter(request.Carrier);
        var carrierResponse = await adapter.CreateShipmentAsync(
            new ShipmentRequest
            {
                Carrier = request.Carrier,
                ShipFrom = request.ShipFrom,
                ShipTo = request.ShipTo,
                Packages = request.Packages,
                ServiceLevel = request.ServiceLevel
            },
            cancellationToken);

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
            shipment.MarkFailed(carrierResponse.FailureReason ?? "Carrier rejected the shipment request.");
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
