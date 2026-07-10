using MultiCarrierApi.Application.Shipments.Models;
using MultiCarrierApi.Domain.Enums;

namespace MultiCarrierApi.Application.Common.Interfaces;

public interface ICarrierShipmentAdapter
{
    CarrierType Carrier { get; }

    Task<CarrierShipmentResponse> CreateShipmentAsync(
        ShipmentRequest request,
        CancellationToken cancellationToken);
}

public interface ICarrierAdapterFactory
{
    ICarrierShipmentAdapter GetAdapter(CarrierType carrier);
}
