using MultiCarrierApi.Application.Common.Interfaces;
using MultiCarrierApi.Domain.Enums;

namespace MultiCarrierApi.Infrastructure.Carriers;

public class CarrierAdapterFactory : ICarrierAdapterFactory
{
    private readonly IReadOnlyDictionary<CarrierType, ICarrierShipmentAdapter> _adapters;

    public CarrierAdapterFactory(IEnumerable<ICarrierShipmentAdapter> adapters)
    {
        _adapters = adapters.ToDictionary(adapter => adapter.Carrier);
    }

    public ICarrierShipmentAdapter GetAdapter(CarrierType carrier)
    {
        if (!_adapters.TryGetValue(carrier, out var adapter))
        {
            throw new NotSupportedException($"Carrier '{carrier}' is not supported.");
        }

        return adapter;
    }
}
