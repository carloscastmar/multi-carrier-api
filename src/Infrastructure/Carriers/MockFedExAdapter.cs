using MultiCarrierApi.Application.Common.Interfaces;
using MultiCarrierApi.Application.Shipments.Models;
using MultiCarrierApi.Domain.Enums;

namespace MultiCarrierApi.Infrastructure.Carriers;

public class MockFedExAdapter : ICarrierShipmentAdapter
{
    public CarrierType Carrier => CarrierType.FedEx;

    public async Task<CarrierShipmentResponse> CreateShipmentAsync(
        ShipmentRequest request,
        CancellationToken cancellationToken)
    {
        await Task.Delay(100, cancellationToken);

        if (request.ShipTo.PostalCode == MockCarrierErrors.InvalidPostalCode)
        {
            return MockCarrierErrors.InvalidShipToPostalCode("FedEx");
        }

        return CarrierShipmentResponse.Success(
            trackingNumber: "748901234567890",
            labelData: Convert.ToBase64String("mock-fedex-label-pdf"u8.ToArray()),
            labelFormat: LabelFormat.Pdf,
            carrierReference: "mock-fedex-ref-001");
    }
}
