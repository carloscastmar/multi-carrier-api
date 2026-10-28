using MultiCarrierApi.Application.FunctionalTests.Shipments.Commands;
using MultiCarrierApi.Application.Shipments.Queries.GetShipmentById;
using MultiCarrierApi.Domain.Enums;

namespace MultiCarrierApi.Application.FunctionalTests.Shipments.Queries;

public class GetShipmentByIdTests : TestBase
{
    [Test]
    public async Task ShouldReturnShipmentToItsOwner()
    {
        await TestApp.RunAsDefaultUserAsync();
        var created = await TestApp.SendAsync(CreateShipmentTests.CreateValidCommand(CarrierType.Ups));

        var shipment = await TestApp.SendAsync(new GetShipmentByIdQuery(created.ShipmentId));

        shipment.ShouldNotBeNull();
        shipment!.Id.ShouldBe(created.ShipmentId);
        shipment.Status.ShouldBe(ShipmentStatus.Created);
    }

    [Test]
    public async Task ShouldNotReturnAnotherUsersShipment()
    {
        await TestApp.RunAsDefaultUserAsync();
        var created = await TestApp.SendAsync(CreateShipmentTests.CreateValidCommand(CarrierType.Ups));

        await TestApp.RunAsUserAsync("other@local", "Testing1234!", []);

        var shipment = await TestApp.SendAsync(new GetShipmentByIdQuery(created.ShipmentId));

        shipment.ShouldBeNull();
    }

    [Test]
    public async Task ShouldReturnAnyShipmentToAdministrator()
    {
        await TestApp.RunAsDefaultUserAsync();
        var created = await TestApp.SendAsync(CreateShipmentTests.CreateValidCommand(CarrierType.Ups));

        await TestApp.RunAsAdministratorAsync();

        var shipment = await TestApp.SendAsync(new GetShipmentByIdQuery(created.ShipmentId));

        shipment.ShouldNotBeNull();
    }
}
