using MultiCarrierApi.Application.Shipments.Models;
using MultiCarrierApi.Domain.Entities;
using MultiCarrierApi.Domain.Enums;
using MultiCarrierApi.Domain.ValueObjects;

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

    public IReadOnlyList<ShipmentErrorDto> Errors { get; init; } = [];

    private class Mapping : Profile
    {
        public Mapping()
        {
            CreateMap<Shipment, ShipmentDto>();
            CreateMap<ShipmentError, ShipmentErrorDto>();
        }
    }
}
