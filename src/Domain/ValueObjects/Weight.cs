using MultiCarrierApi.Domain.Enums;

namespace MultiCarrierApi.Domain.ValueObjects;

public class Weight : ValueObject
{
    public decimal Value { get; private set; }

    public WeightUnit Unit { get; private set; }

    private Weight()
    {
    }

    private Weight(decimal value, WeightUnit unit)
    {
        Value = value;
        Unit = unit;
    }

    public static Weight Create(decimal value, WeightUnit unit)
    {
        if (value <= 0)
        {
            throw new ArgumentException("Weight must be greater than zero.", nameof(value));
        }

        return new Weight(value, unit);
    }

    protected override IEnumerable<object> GetEqualityComponents()
    {
        yield return Value;
        yield return Unit;
    }
}
