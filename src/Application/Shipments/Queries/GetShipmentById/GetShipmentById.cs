using MultiCarrierApi.Application.Common.Interfaces;
using MultiCarrierApi.Domain.Constants;
using MultiCarrierApi.Domain.Entities;

namespace MultiCarrierApi.Application.Shipments.Queries.GetShipmentById;

public record GetShipmentByIdQuery(int Id) : IRequest<ShipmentDto?>;

public class GetShipmentByIdQueryHandler : IRequestHandler<GetShipmentByIdQuery, ShipmentDto?>
{
    private readonly IApplicationDbContext _context;
    private readonly IMapper _mapper;
    private readonly IUser _user;

    public GetShipmentByIdQueryHandler(IApplicationDbContext context, IMapper mapper, IUser user)
    {
        _context = context;
        _mapper = mapper;
        _user = user;
    }

    public async Task<ShipmentDto?> Handle(GetShipmentByIdQuery request, CancellationToken cancellationToken)
    {
        var query = _context.Shipments.Where(s => s.Id == request.Id);

        // Shipments are only visible to the user who created them, unless the caller is an administrator.
        if (_user.Roles?.Contains(Roles.Administrator) != true)
        {
            var userId = _user.Id;
            query = query.Where(s => userId != null && s.CreatedBy == userId);
        }

        return await query
            .ProjectTo<ShipmentDto>(_mapper.ConfigurationProvider)
            .FirstOrDefaultAsync(cancellationToken);
    }
}
