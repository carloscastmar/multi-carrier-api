namespace MultiCarrierApi.Domain.ValueObjects;

public class Address : ValueObject
{
    public string Name { get; private set; } = string.Empty;

    public string Street1 { get; private set; } = string.Empty;

    public string? Street2 { get; private set; }

    public string City { get; private set; } = string.Empty;

    public string State { get; private set; } = string.Empty;

    public string PostalCode { get; private set; } = string.Empty;

    public string CountryCode { get; private set; } = string.Empty;

    private Address()
    {
    }

    private Address(
        string name,
        string street1,
        string? street2,
        string city,
        string state,
        string postalCode,
        string countryCode)
    {
        Name = name;
        Street1 = street1;
        Street2 = street2;
        City = city;
        State = state;
        PostalCode = postalCode;
        CountryCode = countryCode;
    }

    public static Address Create(
        string name,
        string street1,
        string? street2,
        string city,
        string state,
        string postalCode,
        string countryCode)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Name is required.", nameof(name));
        }

        if (string.IsNullOrWhiteSpace(street1))
        {
            throw new ArgumentException("Street1 is required.", nameof(street1));
        }

        if (string.IsNullOrWhiteSpace(city))
        {
            throw new ArgumentException("City is required.", nameof(city));
        }

        if (string.IsNullOrWhiteSpace(state))
        {
            throw new ArgumentException("State is required.", nameof(state));
        }

        if (string.IsNullOrWhiteSpace(postalCode))
        {
            throw new ArgumentException("Postal code is required.", nameof(postalCode));
        }

        if (string.IsNullOrWhiteSpace(countryCode) || countryCode.Length != 2)
        {
            throw new ArgumentException("Country code must be a 2-letter ISO code.", nameof(countryCode));
        }

        return new Address(
            name.Trim(),
            street1.Trim(),
            string.IsNullOrWhiteSpace(street2) ? null : street2.Trim(),
            city.Trim(),
            state.Trim(),
            postalCode.Trim(),
            countryCode.Trim().ToUpperInvariant());
    }

    protected override IEnumerable<object> GetEqualityComponents()
    {
        yield return Name;
        yield return Street1;
        yield return Street2 ?? string.Empty;
        yield return City;
        yield return State;
        yield return PostalCode;
        yield return CountryCode;
    }
}