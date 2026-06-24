using multi_carrier_api.Application.Common.Behaviours;
using multi_carrier_api.Application.Common.Interfaces;
using multi_carrier_api.Application.Shipments.Commands.CreateShipment;
using multi_carrier_api.Application.Shipments.Models;
using multi_carrier_api.Domain.Enums;
using Microsoft.Extensions.Logging;
using Moq;
using NUnit.Framework;

namespace multi_carrier_api.Application.UnitTests.Common.Behaviours;

public class RequestLoggerTests
{
    private Mock<ILogger<CreateShipmentCommand>> _logger = null!;
    private Mock<IUser> _user = null!;
    private Mock<IIdentityService> _identityService = null!;

    [SetUp]
    public void Setup()
    {
        _logger = new Mock<ILogger<CreateShipmentCommand>>();
        _user = new Mock<IUser>();
        _identityService = new Mock<IIdentityService>();
    }

    [Test]
    public async Task ShouldCallGetUserNameAsyncOnceIfAuthenticated()
    {
        _user.Setup(x => x.Id).Returns(Guid.NewGuid().ToString());

        var requestLogger = new LoggingBehaviour<CreateShipmentCommand>(_logger.Object, _user.Object, _identityService.Object);

        await requestLogger.Process(CreateSampleCommand(), new CancellationToken());

        _identityService.Verify(i => i.GetUserNameAsync(It.IsAny<string>()), Times.Once);
    }

    [Test]
    public async Task ShouldNotCallGetUserNameAsyncOnceIfUnauthenticated()
    {
        var requestLogger = new LoggingBehaviour<CreateShipmentCommand>(_logger.Object, _user.Object, _identityService.Object);

        await requestLogger.Process(CreateSampleCommand(), new CancellationToken());

        _identityService.Verify(i => i.GetUserNameAsync(It.IsAny<string>()), Times.Never);
    }

    private static CreateShipmentCommand CreateSampleCommand() =>
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
                    Weight = new WeightDto { Value = 1, Unit = WeightUnit.Lb },
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
