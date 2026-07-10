using MultiCarrierApi.Application.Common.Interfaces;
using MultiCarrierApi.Application.Shipments.Models;
using MultiCarrierApi.Domain.Enums;

namespace MultiCarrierApi.Infrastructure.Carriers.Ups;

public class UpsShipmentAdapter : ICarrierShipmentAdapter
{
    public CarrierType Carrier => CarrierType.Ups;

    public Task<CarrierShipmentResponse> CreateShipmentAsync(
        ShipmentRequest request,
        CancellationToken cancellationToken)
    {
        throw new NotImplementedException(
            "Real UPS integration is not implemented yet. Set Carriers:Ups:UseMock to true.");
    }
}
