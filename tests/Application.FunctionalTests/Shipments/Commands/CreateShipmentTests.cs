using MultiCarrierApi.Application.Common.Exceptions;
using MultiCarrierApi.Application.Shipments.Commands.CreateShipment;
using MultiCarrierApi.Application.Shipments.Models;
using MultiCarrierApi.Domain.Entities;
using MultiCarrierApi.Domain.Enums;

namespace MultiCarrierApi.Application.FunctionalTests.Shipments.Commands;

public class CreateShipmentTests : TestBase
{
    [Test]
    public async Task ShouldRequireMinimumFields()
    {
        var command = new CreateShipmentCommand();
        await Should.ThrowAsync<ValidationException>(() => TestApp.SendAsync(command));
    }

    [Test]
    public async Task ShouldCreateUpsShipment()
    {
        await TestApp.RunAsDefaultUserAsync();

        var command = CreateValidCommand(CarrierType.Ups);
        var result = await TestApp.SendAsync(command);

        result.Succeeded.ShouldBeTrue();
        result.TrackingNumber.ShouldStartWith("1Z");
        result.CarrierReference.ShouldBe("mock-ups-ref-001");

        var shipment = await TestApp.FindAsync<Shipment>(result.ShipmentId);
        shipment.ShouldNotBeNull();
        shipment!.Status.ShouldBe(ShipmentStatus.Created);
    }

    [Test]
    public async Task ShouldCreateFedExShipment()
    {
        await TestApp.RunAsDefaultUserAsync();

        var command = CreateValidCommand(CarrierType.FedEx);
        var result = await TestApp.SendAsync(command);

        result.Succeeded.ShouldBeTrue();
        result.TrackingNumber.ShouldStartWith("7489");
        result.CarrierReference.ShouldBe("mock-fedex-ref-001");
    }

    [Test]
    public async Task ShouldRejectEmptyPackages()
    {
        var command = CreateValidCommand(CarrierType.Ups) with { Packages = [] };
        await Should.ThrowAsync<ValidationException>(() => TestApp.SendAsync(command));
    }

    private static CreateShipmentCommand CreateValidCommand(CarrierType carrier) =>
        new()
        {
            Carrier = carrier,
            ShipFrom = new AddressDto
            {
                Name = "Sender",
                Street1 = "123 Main St",
                City = "Atlanta",
                State = "GA",
                PostalCode = "30301",
                CountryCode = "US"
            },
            ShipTo = new AddressDto
            {
                Name = "Recipient",
                Street1 = "456 Oak Ave",
                City = "Dallas",
                State = "TX",
                PostalCode = "75201",
                CountryCode = "US"
            },
            Packages =
            [
                new PackageDto
                {
                    Weight = new WeightDto { Value = 2.5m, Unit = WeightUnit.Lb },
                    Dimensions = new DimensionsDto
                    {
                        Length = 10,
                        Width = 8,
                        Height = 6,
                        Unit = DimensionUnit.In
                    }
                }
            ],
            ServiceLevel = "Ground"
        };
}
