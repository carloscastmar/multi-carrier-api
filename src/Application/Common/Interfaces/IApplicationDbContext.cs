using MultiCarrierApi.Domain.Entities;

namespace MultiCarrierApi.Application.Common.Interfaces;

public interface IApplicationDbContext
{
    DbSet<Shipment> Shipments { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken);
}
