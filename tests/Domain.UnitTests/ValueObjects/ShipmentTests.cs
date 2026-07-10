using MultiCarrierApi.Domain.Enums;
using MultiCarrierApi.Domain.ValueObjects;
using MultiCarrierApi.Domain.Entities;

namespace MultiCarrierApi.Domain.UnitTests.ValueObjects;

public class AddressTests
{
    [Test]
    public void ShouldCreateValidAddress()
    {
        var address = Address.Create(
            "Acme Corp",
            "123 Main St",
            null,
            "Atlanta",
            "GA",
            "30301",
            "us");

        address.Name.ShouldBe("Acme Corp");
        address.CountryCode.ShouldBe("US");
    }

    [Test]
    public void ShouldRequireTwoLetterCountryCode()
    {
        Should.Throw<ArgumentException>(() =>
            Address.Create("Acme", "123 Main", null, "Atlanta", "GA", "30301", "USA"));
    }
}

public class ShipmentTests
{
    [Test]
    public void ShouldRequireAtLeastOnePackage()
    {
        var shipFrom = CreateAddress("From");
        var shipTo = CreateAddress("To");

        Should.Throw<ArgumentException>(() =>
            Shipment.Create(CarrierType.Ups, shipFrom, shipTo, [], null));
    }

    [Test]
    public void ShouldMarkCreatedAndRaiseDomainEvent()
    {
        var shipment = CreateShipment();

        shipment.MarkCreated("1Z123", "label-data", LabelFormat.Pdf, "ref-001");

        shipment.Status.ShouldBe(ShipmentStatus.Created);
        shipment.TrackingNumber.ShouldBe("1Z123");
        shipment.DomainEvents.ShouldContain(e => e is Domain.Events.ShipmentCreatedEvent);
    }

    private static Domain.Entities.Shipment CreateShipment()
    {
        return Domain.Entities.Shipment.Create(
            CarrierType.Ups,
            CreateAddress("From"),
            CreateAddress("To"),
            [CreatePackage()],
            "Ground");
    }

    private static Address CreateAddress(string name) =>
        Address.Create(name, "123 Main St", null, "Atlanta", "GA", "30301", "US");

    private static Package CreatePackage() =>
        Package.Create(
            Weight.Create(2.5m, WeightUnit.Lb),
            Dimensions.Create(10, 8, 6, DimensionUnit.In));
}
