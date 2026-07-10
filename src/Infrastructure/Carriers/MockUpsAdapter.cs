using MultiCarrierApi.Application.Common.Interfaces;
using MultiCarrierApi.Application.Shipments.Models;
using MultiCarrierApi.Domain.Enums;

namespace MultiCarrierApi.Infrastructure.Carriers;

public class MockUpsAdapter : ICarrierShipmentAdapter
{
    public CarrierType Carrier => CarrierType.Ups;

    public async Task<CarrierShipmentResponse> CreateShipmentAsync(
        ShipmentRequest request,
        CancellationToken cancellationToken)
    {
        await Task.Delay(100, cancellationToken);

        return CarrierShipmentResponse.Success(
            trackingNumber: "1Z999AA10123456784",
            labelData: Convert.ToBase64String("mock-ups-label-pdf"u8.ToArray()),
            labelFormat: LabelFormat.Pdf,
            carrierReference: "mock-ups-ref-001");
    }
}
