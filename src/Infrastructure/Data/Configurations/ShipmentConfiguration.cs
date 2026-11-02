using MultiCarrierApi.Domain.Constants;
using MultiCarrierApi.Domain.Entities;
using MultiCarrierApi.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MultiCarrierApi.Infrastructure.Data.Configurations;

public class ShipmentConfiguration : IEntityTypeConfiguration<Shipment>
{
    public void Configure(EntityTypeBuilder<Shipment> builder)
    {
        builder.Property(s => s.TrackingNumber).HasMaxLength(ShipmentFieldLengths.TrackingNumber);
        builder.Property(s => s.CarrierReference).HasMaxLength(ShipmentFieldLengths.CarrierReference);
        builder.Property(s => s.ServiceLevel).HasMaxLength(ShipmentFieldLengths.ServiceLevel);
        builder.Property(s => s.FailureReason).HasMaxLength(ShipmentFieldLengths.FailureReason);

        builder.OwnsOne(s => s.ShipFrom, ConfigureAddress);
        builder.OwnsOne(s => s.ShipTo, ConfigureAddress);

        builder.OwnsMany(s => s.Packages, package =>
        {
            package.ToTable("ShipmentPackages");
            package.Property(p => p.Description).HasMaxLength(ShipmentFieldLengths.PackageDescription);
            package.OwnsOne(p => p.Weight);
            package.OwnsOne(p => p.Dimensions);
        });

        builder.OwnsMany(s => s.Errors, error =>
        {
            error.ToTable("ShipmentErrors");
            error.Property(e => e.Code).HasMaxLength(ShipmentFieldLengths.ErrorCode).IsRequired();
            error.Property(e => e.Message).HasMaxLength(ShipmentFieldLengths.ErrorMessage).IsRequired();
            error.Property(e => e.Field).HasMaxLength(ShipmentFieldLengths.ErrorField);
        });
    }

    private static void ConfigureAddress(OwnedNavigationBuilder<Shipment, Address> address)
    {
        address.Property(a => a.Name).HasMaxLength(ShipmentFieldLengths.AddressName).IsRequired();
        address.Property(a => a.Street1).HasMaxLength(ShipmentFieldLengths.AddressStreet).IsRequired();
        address.Property(a => a.Street2).HasMaxLength(ShipmentFieldLengths.AddressStreet);
        address.Property(a => a.City).HasMaxLength(ShipmentFieldLengths.AddressCity).IsRequired();
        address.Property(a => a.State).HasMaxLength(ShipmentFieldLengths.AddressState).IsRequired();
        address.Property(a => a.PostalCode).HasMaxLength(ShipmentFieldLengths.AddressPostalCode).IsRequired();
        address.Property(a => a.CountryCode).HasMaxLength(ShipmentFieldLengths.AddressCountryCode).IsRequired();
    }
}
