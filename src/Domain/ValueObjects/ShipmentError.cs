namespace MultiCarrierApi.Domain.ValueObjects;

public class ShipmentError : ValueObject
{
    public string Code { get; private set; } = string.Empty;

    public string Message { get; private set; } = string.Empty;

    public string? Field { get; private set; }

    private ShipmentError()
    {
    }

    private ShipmentError(string code, string message, string? field)
    {
        Code = code;
        Message = message;
        Field = field;
    }

    public static ShipmentError Create(string code, string message, string? field = null)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            throw new ArgumentException("Error code is required.", nameof(code));
        }

        if (string.IsNullOrWhiteSpace(message))
        {
            throw new ArgumentException("Error message is required.", nameof(message));
        }

        return new ShipmentError(code.Trim(), message.Trim(), string.IsNullOrWhiteSpace(field) ? null : field.Trim());
    }

    protected override IEnumerable<object> GetEqualityComponents()
    {
        yield return Code;
        yield return Message;
        yield return Field ?? string.Empty;
    }
}
