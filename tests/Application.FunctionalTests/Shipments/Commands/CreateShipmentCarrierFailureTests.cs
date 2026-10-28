using MultiCarrierApi.Application.Common.Interfaces;
using MultiCarrierApi.Application.Shipments.Commands.CreateShipment;
using MultiCarrierApi.Application.Shipments.Models;
using MultiCarrierApi.Domain.Entities;
using MultiCarrierApi.Domain.Enums;
using MultiCarrierApi.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace MultiCarrierApi.Application.FunctionalTests.Shipments.Commands;

public class CreateShipmentCarrierFailureTests : TestBase
{
    [Test]
    public async Task ShouldMarkShipmentFailedWhenCarrierThrows()
    {
        await TestApp.RunAsDefaultUserAsync();

        var adapter = new Mock<ICarrierShipmentAdapter>();
        adapter.SetupGet(a => a.Carrier).Returns(CarrierType.Ups);
        adapter
            .Setup(a => a.CreateShipmentAsync(It.IsAny<ShipmentRequest>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("carrier is down"));

        var result = await SendWithAdapterFactory(FactoryReturning(adapter.Object), CreateShipmentTests.CreateValidCommand(CarrierType.Ups));

        result.Succeeded.ShouldBeFalse();
        result.FailureReason.ShouldBe("The Ups carrier request failed.");

        var shipment = await TestApp.FindAsync<Shipment>(result.ShipmentId);
        shipment.ShouldNotBeNull();
        shipment!.Status.ShouldBe(ShipmentStatus.Failed);
        shipment.FailureReason.ShouldBe("The Ups carrier request failed.");
    }

    [Test]
    public async Task ShouldNotPersistShipmentWhenCarrierIsNotRegistered()
    {
        await TestApp.RunAsDefaultUserAsync();

        var factory = new Mock<ICarrierAdapterFactory>();
        factory
            .Setup(f => f.GetAdapter(It.IsAny<CarrierType>()))
            .Throws(new NotSupportedException("Carrier 'Ups' is not supported."));

        await Should.ThrowAsync<NotSupportedException>(() =>
            SendWithAdapterFactory(factory.Object, CreateShipmentTests.CreateValidCommand(CarrierType.Ups)));

        (await TestApp.CountAsync<Shipment>()).ShouldBe(0);
    }

    private static ICarrierAdapterFactory FactoryReturning(ICarrierShipmentAdapter adapter)
    {
        var factory = new Mock<ICarrierAdapterFactory>();
        factory.Setup(f => f.GetAdapter(adapter.Carrier)).Returns(adapter);
        return factory.Object;
    }

    private static async Task<CreateShipmentResult> SendWithAdapterFactory(
        ICarrierAdapterFactory factory,
        CreateShipmentCommand command)
    {
        using var scope = FunctionalTestSetup.ScopeFactory.CreateScope();

        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var handler = new CreateShipmentCommandHandler(
            context,
            factory,
            NullLogger<CreateShipmentCommandHandler>.Instance);

        return await handler.Handle(command, CancellationToken.None);
    }
}
