using MultiCarrierApi.Domain.Entities;
using MultiCarrierApi.Domain.Enums;
using MultiCarrierApi.Domain.ValueObjects;
using MultiCarrierApi.Infrastructure.Data;
using Microsoft.Extensions.DependencyInjection;

namespace MultiCarrierApi.Infrastructure.IntegrationTests.Data;

public class ShipmentPersistenceTests
{
    [Test]
    public async Task ShouldPersistAndReloadShipmentWithOwnedTypes()
    {
        await using var scope = IntegrationTestSetup.ScopeFactory.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var shipment = Shipment.Create(
            CarrierType.Ups,
            Address.Create("Sender", "123 Main", null, "Atlanta", "GA", "30301", "US"),
            Address.Create("Recipient", "456 Oak", null, "Dallas", "TX", "75201", "US"),
            [
                Package.Create(
                    Weight.Create(2.5m, WeightUnit.Lb),
                    Dimensions.Create(10, 8, 6, DimensionUnit.In),
                    "Books")
            ],
            "Ground");

        shipment.MarkCreated("1Z123", "label", LabelFormat.Pdf, "ref-001");

        context.Shipments.Add(shipment);
        await context.SaveChangesAsync();

        var reloaded = await context.Shipments
            .AsNoTracking()
            .FirstAsync(s => s.Id == shipment.Id);

        reloaded.TrackingNumber.ShouldBe("1Z123");
        reloaded.ShipFrom.City.ShouldBe("Atlanta");
        reloaded.Packages.Count.ShouldBe(1);
        reloaded.Packages[0].Description.ShouldBe("Books");
    }
}
