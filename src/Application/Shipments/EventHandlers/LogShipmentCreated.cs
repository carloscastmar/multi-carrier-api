using MultiCarrierApi.Domain.Events;
using Microsoft.Extensions.Logging;

namespace MultiCarrierApi.Application.Shipments.EventHandlers;

public class LogShipmentCreated : INotificationHandler<ShipmentCreatedEvent>
{
    private readonly ILogger<LogShipmentCreated> _logger;

    public LogShipmentCreated(ILogger<LogShipmentCreated> logger)
    {
        _logger = logger;
    }

    public Task Handle(ShipmentCreatedEvent notification, CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "Shipment {ShipmentId} created with tracking number {TrackingNumber}",
            notification.Shipment.Id,
            notification.Shipment.TrackingNumber);

        return Task.CompletedTask;
    }
}
