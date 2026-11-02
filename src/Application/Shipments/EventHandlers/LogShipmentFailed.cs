using MultiCarrierApi.Domain.Events;
using Microsoft.Extensions.Logging;

namespace MultiCarrierApi.Application.Shipments.EventHandlers;

public class LogShipmentFailed : INotificationHandler<ShipmentFailedEvent>
{
    private readonly ILogger<LogShipmentFailed> _logger;

    public LogShipmentFailed(ILogger<LogShipmentFailed> logger)
    {
        _logger = logger;
    }

    public Task Handle(ShipmentFailedEvent notification, CancellationToken cancellationToken)
    {
        _logger.LogWarning(
            "Shipment {ShipmentId} failed with error codes {ErrorCodes}",
            notification.Shipment.Id,
            notification.Shipment.Errors.Select(e => e.Code).ToList());

        return Task.CompletedTask;
    }
}
