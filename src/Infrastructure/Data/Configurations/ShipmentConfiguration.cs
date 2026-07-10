using MultiCarrierApi.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MultiCarrierApi.Infrastructure.Data.Configurations;

public class ShipmentConfiguration : IEntityTypeConfiguration<Shipment>
{
    public void Configure(EntityTypeBuilder<Shipment> builder)
    {
        builder.Property(s => s.TrackingNumber).HasMaxLength(100);
        builder.Property(s => s.CarrierReference).HasMaxLength(100);
        builder.Property(s => s.ServiceLevel).HasMaxLength(50);
        builder.Property(s => s.FailureReason).HasMaxLength(500);

        builder.OwnsOne(s => s.ShipFrom, address =>
        {
            address.Property(a => a.Name).HasMaxLength(200).IsRequired();
            address.Property(a => a.Street1).HasMaxLength(200).IsRequired();
            address.Property(a => a.Street2).HasMaxLength(200);
            address.Property(a => a.City).HasMaxLength(100).IsRequired();
            address.Property(a => a.State).HasMaxLength(100).IsRequired();
            address.Property(a => a.PostalCode).HasMaxLength(20).IsRequired();
            address.Property(a => a.CountryCode).HasMaxLength(2).IsRequired();
        });

        builder.OwnsOne(s => s.ShipTo, address =>
        {
            address.Property(a => a.Name).HasMaxLength(200).IsRequired();
            address.Property(a => a.Street1).HasMaxLength(200).IsRequired();
            address.Property(a => a.Street2).HasMaxLength(200);
            address.Property(a => a.City).HasMaxLength(100).IsRequired();
            address.Property(a => a.State).HasMaxLength(100).IsRequired();
            address.Property(a => a.PostalCode).HasMaxLength(20).IsRequired();
            address.Property(a => a.CountryCode).HasMaxLength(2).IsRequired();
        });

        builder.OwnsMany(s => s.Packages, package =>
        {
            package.Property(p => p.Description).HasMaxLength(200);
            package.OwnsOne(p => p.Weight);
            package.OwnsOne(p => p.Dimensions);
        });
    }
}
