using multi_carrier_api.Domain.Entities;

namespace multi_carrier_api.Application.Common.Interfaces;

public interface IApplicationDbContext
{
    DbSet<Shipment> Shipments { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken);
}
