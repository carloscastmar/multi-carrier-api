using MultiCarrierApi.Domain.Entities;
using MultiCarrierApi.Domain.Enums;

namespace MultiCarrierApi.Application.Shipments.Queries.GetShipmentById;

public class ShipmentDto
{
    public int Id { get; init; }

    public CarrierType Carrier { get; init; }

    public ShipmentStatus Status { get; init; }

    public string? ServiceLevel { get; init; }

    public string? TrackingNumber { get; init; }

    public string? CarrierReference { get; init; }

    public string? LabelData { get; init; }

    public LabelFormat? LabelFormat { get; init; }

    public string? FailureReason { get; init; }

    private class Mapping : Profile
    {
        public Mapping()
        {
            CreateMap<Shipment, ShipmentDto>();
        }
    }
}
