using MultiCarrierApi.Application.Shipments.Commands.CreateShipment;
using MultiCarrierApi.Application.Shipments.Models;
using MultiCarrierApi.Domain.Enums;
using MultiCarrierApi.Infrastructure.Carriers;

namespace MultiCarrierApi.Application.UnitTests.Carriers;

public class CarrierAdapterFactoryTests
{
    [Test]
    public void ShouldResolveRegisteredAdapters()
    {
        IReadOnlyList<ICarrierShipmentAdapter> adapters =
        [
            new MockUpsAdapter(),
            new MockFedExAdapter()
        ];

        var factory = new CarrierAdapterFactory(adapters);

        factory.GetAdapter(CarrierType.Ups).Carrier.ShouldBe(CarrierType.Ups);
        factory.GetAdapter(CarrierType.FedEx).Carrier.ShouldBe(CarrierType.FedEx);
    }

    [Test]
    public void ShouldThrowForUnsupportedCarrier()
    {
        var factory = new CarrierAdapterFactory([new MockUpsAdapter()]);

        Should.Throw<NotSupportedException>(() => factory.GetAdapter(CarrierType.FedEx));
    }
}

public class MockCarrierAdapterTests
{
    [Test]
    public async Task ShouldReturnStructuredErrorForInvalidPostalCode()
    {
        IReadOnlyList<ICarrierShipmentAdapter> adapters = [new MockUpsAdapter(), new MockFedExAdapter()];

        foreach (var adapter in adapters)
        {
            var response = await adapter.CreateShipmentAsync(
                new ShipmentRequest
                {
                    Carrier = adapter.Carrier,
                    ShipFrom = new AddressDto { PostalCode = "30301" },
                    ShipTo = new AddressDto { PostalCode = MockCarrierErrors.InvalidPostalCode }
                },
                CancellationToken.None);

            response.Succeeded.ShouldBeFalse();
            response.Errors.Count.ShouldBe(1);
            response.Errors[0].Code.ShouldBe(MockCarrierErrors.InvalidPostalCodeErrorCode);
            response.Errors[0].Field.ShouldBe("ShipTo.PostalCode");
        }
    }
}

public class CreateShipmentCommandValidatorTests
{
    private readonly CreateShipmentCommandValidator _validator = new();

    [Test]
    public void ShouldValidateValidCommand()
    {
        var result = _validator.Validate(CreateValidCommand());
        result.IsValid.ShouldBeTrue();
    }

    [Test]
    public void ShouldRejectInvalidCountryCode()
    {
        var command = CreateValidCommand();
        command = command with
        {
            ShipTo = command.ShipTo with { CountryCode = "USA" }
        };

        var result = _validator.Validate(command);
        result.IsValid.ShouldBeFalse();
    }

    [Test]
    public void ShouldRejectValuesLongerThanTheirDatabaseColumns()
    {
        var command = CreateValidCommand();
        command = command with
        {
            ShipFrom = command.ShipFrom with { Name = new string('a', 201) },
            ServiceLevel = new string('a', 51)
        };

        var result = _validator.Validate(command);

        result.IsValid.ShouldBeFalse();
        result.Errors.Select(e => e.PropertyName).ShouldBe(["ShipFrom.Name", "ServiceLevel"], ignoreOrder: true);
    }

    [Test]
    public void ShouldRejectNullPackage()
    {
        var command = CreateValidCommand() with { Packages = [null!] };

        var result = _validator.Validate(command);

        result.IsValid.ShouldBeFalse();
    }

    private static CreateShipmentCommand CreateValidCommand() =>
        new()
        {
            Carrier = CarrierType.Ups,
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
                    Weight = new WeightDto
                    {
                        Value = 1,
                        Unit = WeightUnit.Lb
                    },
                    Dimensions = new DimensionsDto
                    {
                        Length = 1,
                        Width = 1,
                        Height = 1,
                        Unit = DimensionUnit.In
                    }
                }
            ]
        };
}
