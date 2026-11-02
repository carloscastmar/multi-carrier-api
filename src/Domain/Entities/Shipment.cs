using MultiCarrierApi.Domain.Enums;
using MultiCarrierApi.Domain.Events;
using MultiCarrierApi.Domain.ValueObjects;

namespace MultiCarrierApi.Domain.Entities;

public class Shipment : BaseAuditableEntity
{
    public CarrierType Carrier { get; private set; }

    public ShipmentStatus Status { get; private set; }

    public string? ServiceLevel { get; private set; }

    public string? TrackingNumber { get; private set; }

    public string? CarrierReference { get; private set; }

    public string? LabelData { get; private set; }

    public LabelFormat? LabelFormat { get; private set; }

    public string? FailureReason { get; private set; }

    public Address ShipFrom { get; private set; } = null!;

    public Address ShipTo { get; private set; } = null!;

    public IList<Package> Packages { get; private set; } = [];

    public IList<ShipmentError> Errors { get; private set; } = [];

    private Shipment()
    {
    }

    public static Shipment Create(
        CarrierType carrier,
        Address shipFrom,
        Address shipTo,
        IEnumerable<Package> packages,
        string? serviceLevel)
    {
        var packageList = packages.ToList();

        if (packageList.Count == 0)
        {
            throw new ArgumentException("At least one package is required.", nameof(packages));
        }

        return new Shipment
        {
            Carrier = carrier,
            Status = ShipmentStatus.Pending,
            ShipFrom = shipFrom,
            ShipTo = shipTo,
            Packages = packageList,
            ServiceLevel = string.IsNullOrWhiteSpace(serviceLevel) ? null : serviceLevel.Trim()
        };
    }

    public void MarkCreated(
        string trackingNumber,
        string labelData,
        LabelFormat labelFormat,
        string carrierReference)
    {
        Status = ShipmentStatus.Created;
        TrackingNumber = trackingNumber;
        LabelData = labelData;
        LabelFormat = labelFormat;
        CarrierReference = carrierReference;
        FailureReason = null;
        Errors.Clear();

        AddDomainEvent(new ShipmentCreatedEvent(this));
    }

    public void MarkFailed(string failureReason, IEnumerable<ShipmentError> errors)
    {
        Status = ShipmentStatus.Failed;
        FailureReason = failureReason;
        Errors.Clear();

        foreach (var error in errors)
        {
            Errors.Add(error);
        }

        AddDomainEvent(new ShipmentFailedEvent(this));
    }
}
