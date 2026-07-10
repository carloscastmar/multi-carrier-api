using MultiCarrierApi.Application.Common.Interfaces;
using MultiCarrierApi.Domain.Entities;

namespace MultiCarrierApi.Application.Shipments.Queries.GetShipmentById;

public record GetShipmentByIdQuery(int Id) : IRequest<ShipmentDto?>;

public class GetShipmentByIdQueryHandler : IRequestHandler<GetShipmentByIdQuery, ShipmentDto?>
{
    private readonly IApplicationDbContext _context;
    private readonly IMapper _mapper;

    public GetShipmentByIdQueryHandler(IApplicationDbContext context, IMapper mapper)
    {
        _context = context;
        _mapper = mapper;
    }

    public async Task<ShipmentDto?> Handle(GetShipmentByIdQuery request, CancellationToken cancellationToken)
    {
        return await _context.Shipments
            .Where(s => s.Id == request.Id)
            .ProjectTo<ShipmentDto>(_mapper.ConfigurationProvider)
            .FirstOrDefaultAsync(cancellationToken);
    }
}
