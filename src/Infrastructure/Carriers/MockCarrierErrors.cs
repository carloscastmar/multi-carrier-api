using MultiCarrierApi.Application.Shipments.Models;

namespace MultiCarrierApi.Infrastructure.Carriers;

public static class MockCarrierErrors
{
    public const string InvalidPostalCode = "00000";

    public const string InvalidPostalCodeErrorCode = "invalid_postal_code";

    public static CarrierShipmentResponse InvalidShipToPostalCode(string carrier) =>
        CarrierShipmentResponse.Failure(
            $"{carrier} rejected the ship-to postal code.",
            [
                new CarrierError
                {
                    Code = InvalidPostalCodeErrorCode,
                    Message = "The ship-to postal code is not valid.",
                    Field = "ShipTo.PostalCode"
                }
            ]);
}
