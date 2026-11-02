using MultiCarrierApi.Domain.Entities;

namespace MultiCarrierApi.Domain.Events;

public class ShipmentFailedEvent : BaseEvent
{
    public ShipmentFailedEvent(Shipment shipment)
    {
        Shipment = shipment;
    }

    public Shipment Shipment { get; }
}
