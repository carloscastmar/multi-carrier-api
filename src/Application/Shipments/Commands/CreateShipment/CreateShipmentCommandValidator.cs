using MultiCarrierApi.Application.Shipments.Models;
using MultiCarrierApi.Domain.Constants;

namespace MultiCarrierApi.Application.Shipments.Commands.CreateShipment;

public class CreateShipmentCommandValidator : AbstractValidator<CreateShipmentCommand>
{
    public CreateShipmentCommandValidator()
    {
        RuleFor(v => v.Carrier)
            .IsInEnum()
            .WithMessage("Carrier must be a supported value.");

        RuleFor(v => v.ShipFrom).NotNull().SetValidator(new AddressDtoValidator());
        RuleFor(v => v.ShipTo).NotNull().SetValidator(new AddressDtoValidator());
        RuleFor(v => v.Packages).NotEmpty().WithMessage("At least one package is required.");
        RuleForEach(v => v.Packages).NotNull().SetValidator(new PackageDtoValidator());
        RuleFor(v => v.ServiceLevel).MaximumLength(ShipmentFieldLengths.ServiceLevel);
    }
}

public class AddressDtoValidator : AbstractValidator<AddressDto>
{
    public AddressDtoValidator()
    {
        RuleFor(v => v.Name).NotEmpty().MaximumLength(ShipmentFieldLengths.AddressName);
        RuleFor(v => v.Street1).NotEmpty().MaximumLength(ShipmentFieldLengths.AddressStreet);
        RuleFor(v => v.Street2).MaximumLength(ShipmentFieldLengths.AddressStreet);
        RuleFor(v => v.City).NotEmpty().MaximumLength(ShipmentFieldLengths.AddressCity);
        RuleFor(v => v.State).NotEmpty().MaximumLength(ShipmentFieldLengths.AddressState);
        RuleFor(v => v.PostalCode).NotEmpty().MaximumLength(ShipmentFieldLengths.AddressPostalCode);
        RuleFor(v => v.CountryCode)
            .NotEmpty()
            .Length(ShipmentFieldLengths.AddressCountryCode)
            .WithMessage("Country code must be a 2-letter ISO code.");
        RuleFor(v => v.Phone).MaximumLength(ShipmentFieldLengths.AddressPhone);
    }
}

public class PackageDtoValidator : AbstractValidator<PackageDto>
{
    public PackageDtoValidator()
    {
        RuleFor(v => v.Weight).NotNull().SetValidator(new WeightDtoValidator());
        RuleFor(v => v.Dimensions).NotNull().SetValidator(new DimensionsDtoValidator());
        RuleFor(v => v.Description).MaximumLength(ShipmentFieldLengths.PackageDescription);
    }
}

public class WeightDtoValidator : AbstractValidator<WeightDto>
{
    public WeightDtoValidator()
    {
        RuleFor(v => v.Value).GreaterThan(0);
        RuleFor(v => v.Unit).IsInEnum();
    }
}

public class DimensionsDtoValidator : AbstractValidator<DimensionsDto>
{
    public DimensionsDtoValidator()
    {
        RuleFor(v => v.Length).GreaterThan(0);
        RuleFor(v => v.Width).GreaterThan(0);
        RuleFor(v => v.Height).GreaterThan(0);
        RuleFor(v => v.Unit).IsInEnum();
    }
}
