using MultiCarrierApi.Domain.Enums;

namespace MultiCarrierApi.Domain.ValueObjects;

public class Dimensions : ValueObject
{
    public decimal Length { get; private set; }

    public decimal Width { get; private set; }

    public decimal Height { get; private set; }

    public DimensionUnit Unit { get; private set; }

    private Dimensions()
    {
    }

    private Dimensions(decimal length, decimal width, decimal height, DimensionUnit unit)
    {
        Length = length;
        Width = width;
        Height = height;
        Unit = unit;
    }

    public static Dimensions Create(decimal length, decimal width, decimal height, DimensionUnit unit)
    {
        if (length <= 0 || width <= 0 || height <= 0)
        {
            throw new ArgumentException("Dimensions must be greater than zero.");
        }

        return new Dimensions(length, width, height, unit);
    }

    protected override IEnumerable<object> GetEqualityComponents()
    {
        yield return Length;
        yield return Width;
        yield return Height;
        yield return Unit;
    }
}
