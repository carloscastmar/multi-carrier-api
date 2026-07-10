using MultiCarrierApi.Application.Common.Interfaces;
using MultiCarrierApi.Application.Shipments.Models;
using MultiCarrierApi.Domain.Enums;

namespace MultiCarrierApi.Infrastructure.Carriers.FedEx;

public class FedExShipmentAdapter : ICarrierShipmentAdapter
{
    public CarrierType Carrier => CarrierType.FedEx;

    public Task<CarrierShipmentResponse> CreateShipmentAsync(
        ShipmentRequest request,
        CancellationToken cancellationToken)
    {
        throw new NotImplementedException(
            "Real FedEx integration is not implemented yet. Set Carriers:FedEx:UseMock to true.");
    }
}
