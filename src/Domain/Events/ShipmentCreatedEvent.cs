using MultiCarrierApi.Domain.Entities;

namespace MultiCarrierApi.Domain.Events;

public class ShipmentCreatedEvent : BaseEvent
{
    public ShipmentCreatedEvent(Shipment shipment)
    {
        Shipment = shipment;
    }

    public Shipment Shipment { get; }
}
