using MultiCarrierApi.Application.Shipments.Models;
using MultiCarrierApi.Domain.Enums;

namespace MultiCarrierApi.Application.Shipments.Commands.CreateShipment;

public class CreateShipmentCommandValidator : AbstractValidator<CreateShipmentCommand>
{
    public CreateShipmentCommandValidator()
    {
        RuleFor(v => v.Carrier)
            .IsInEnum()
            .Must(c => Enum.IsDefined(typeof(CarrierType), c))
            .WithMessage("Carrier must be a supported value.");

        RuleFor(v => v.ShipFrom).NotNull().SetValidator(new AddressDtoValidator());
        RuleFor(v => v.ShipTo).NotNull().SetValidator(new AddressDtoValidator());
        RuleFor(v => v.Packages).NotEmpty().WithMessage("At least one package is required.");
        RuleForEach(v => v.Packages).SetValidator(new PackageDtoValidator());
    }
}

public class AddressDtoValidator : AbstractValidator<AddressDto>
{
    public AddressDtoValidator()
    {
        RuleFor(v => v.Name).NotEmpty();
        RuleFor(v => v.Street1).NotEmpty();
        RuleFor(v => v.City).NotEmpty();
        RuleFor(v => v.State).NotEmpty();
        RuleFor(v => v.PostalCode).NotEmpty();
        RuleFor(v => v.CountryCode)
            .NotEmpty()
            .Length(2)
            .WithMessage("Country code must be a 2-letter ISO code.");
    }
}

public class PackageDtoValidator : AbstractValidator<PackageDto>
{
    public PackageDtoValidator()
    {
        RuleFor(v => v.Weight).NotNull().SetValidator(new WeightDtoValidator());
        RuleFor(v => v.Dimensions).NotNull().SetValidator(new DimensionsDtoValidator());
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
