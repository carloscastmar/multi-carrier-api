namespace MultiCarrierApi.Domain.ValueObjects;

public class Package : ValueObject
{
    public Weight Weight { get; private set; } = null!;

    public Dimensions Dimensions { get; private set; } = null!;

    public string? Description { get; private set; }

    private Package()
    {
    }

    private Package(Weight weight, Dimensions dimensions, string? description)
    {
        Weight = weight;
        Dimensions = dimensions;
        Description = description;
    }

    public static Package Create(Weight weight, Dimensions dimensions, string? description = null)
    {
        return new Package(weight, dimensions, string.IsNullOrWhiteSpace(description) ? null : description.Trim());
    }

    protected override IEnumerable<object> GetEqualityComponents()
    {
        yield return Weight;
        yield return Dimensions;
        yield return Description ?? string.Empty;
    }
}
