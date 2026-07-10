namespace MultiCarrierApi.Infrastructure.Carriers;

public class CarriersOptions
{
    public const string SectionName = "Carriers";

    public CarrierOptions Ups { get; set; } = new();

    public CarrierOptions FedEx { get; set; } = new();
}

public class CarrierOptions
{
    public bool UseMock { get; set; } = true;

    public string? ApiUrl { get; set; }

    public string? ClientId { get; set; }

    public string? ClientSecret { get; set; }
}
