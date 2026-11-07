using System.Diagnostics;
using MultiCarrierApi.Application.Shipments.Models;
using MultiCarrierApi.Domain.Enums;
using MultiCarrierApi.Infrastructure.Carriers;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace MultiCarrierApi.Application.UnitTests.Carriers;

public class InstrumentedCarrierAdapterTests
{
    private static readonly ShipmentRequest Request = new()
    {
        Carrier = CarrierType.Ups,
        ShipFrom = new AddressDto { PostalCode = "30301" },
        ShipTo = new AddressDto { PostalCode = "10001" }
    };

    private static (InstrumentedCarrierAdapter Adapter, Mock<ICarrierShipmentAdapter> Inner) Create()
    {
        var inner = new Mock<ICarrierShipmentAdapter>();
        inner.SetupGet(a => a.Carrier).Returns(CarrierType.Ups);
        return (new InstrumentedCarrierAdapter(inner.Object, NullLogger<InstrumentedCarrierAdapter>.Instance), inner);
    }

    [Test]
    public async Task ShouldReturnInnerResponseAndExposeCarrier()
    {
        var (adapter, inner) = Create();
        var expected = CarrierShipmentResponse.Failure("rejected", []);
        inner.Setup(a => a.CreateShipmentAsync(Request, It.IsAny<CancellationToken>())).ReturnsAsync(expected);

        var response = await adapter.CreateShipmentAsync(Request, CancellationToken.None);

        adapter.Carrier.ShouldBe(CarrierType.Ups);
        response.ShouldBeSameAs(expected);
    }

    [Test]
    public void ShouldRethrowInnerException()
    {
        var (adapter, inner) = Create();
        inner.Setup(a => a.CreateShipmentAsync(Request, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("boom"));

        Should.Throw<InvalidOperationException>(() => adapter.CreateShipmentAsync(Request, CancellationToken.None));
    }

    [Test]
    public async Task ShouldRecordActivityWithCarrierAndOutcome()
    {
        var activities = new List<Activity>();
        using var listener = new ActivityListener
        {
            ShouldListenTo = source => source.Name == InstrumentedCarrierAdapter.ActivitySourceName,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData,
            ActivityStopped = activities.Add
        };
        ActivitySource.AddActivityListener(listener);

        var (adapter, inner) = Create();
        inner.Setup(a => a.CreateShipmentAsync(Request, It.IsAny<CancellationToken>()))
            .ReturnsAsync(CarrierShipmentResponse.Failure("rejected", []));

        await adapter.CreateShipmentAsync(Request, CancellationToken.None);

        var activity = activities.ShouldHaveSingleItem();
        activity.GetTagItem("carrier").ShouldBe("Ups");
        activity.GetTagItem("carrier.outcome").ShouldBe("rejected");
    }
}
