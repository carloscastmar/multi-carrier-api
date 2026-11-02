namespace MultiCarrierApi.Domain.Constants;

public static class ShipmentFieldLengths
{
    public const int ServiceLevel = 50;
    public const int TrackingNumber = 100;
    public const int CarrierReference = 100;
    public const int FailureReason = 500;

    public const int AddressName = 200;
    public const int AddressStreet = 200;
    public const int AddressCity = 100;
    public const int AddressState = 100;
    public const int AddressPostalCode = 20;
    public const int AddressCountryCode = 2;

    public const int PackageDescription = 200;

    public const int ErrorCode = 100;
    public const int ErrorMessage = 500;
    public const int ErrorField = 100;
}
