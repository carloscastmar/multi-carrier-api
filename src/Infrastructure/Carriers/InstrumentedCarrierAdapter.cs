using System.Diagnostics;
using MultiCarrierApi.Application.Common.Interfaces;
using MultiCarrierApi.Application.Shipments.Models;
using MultiCarrierApi.Domain.Enums;
using Microsoft.Extensions.Logging;

namespace MultiCarrierApi.Infrastructure.Carriers;

public class InstrumentedCarrierAdapter : ICarrierShipmentAdapter
{
    public const string ActivitySourceName = "MultiCarrierApi.Carriers";

    private static readonly ActivitySource ActivitySource = new(ActivitySourceName);

    private readonly ICarrierShipmentAdapter _inner;
    private readonly ILogger<InstrumentedCarrierAdapter> _logger;

    public InstrumentedCarrierAdapter(ICarrierShipmentAdapter inner, ILogger<InstrumentedCarrierAdapter> logger)
    {
        _inner = inner;
        _logger = logger;
    }

    public CarrierType Carrier => _inner.Carrier;

    public async Task<CarrierShipmentResponse> CreateShipmentAsync(
        ShipmentRequest request,
        CancellationToken cancellationToken)
    {
        using var activity = ActivitySource.StartActivity("Carrier CreateShipment");
        activity?.SetTag("carrier", Carrier.ToString());

        using var scope = _logger.BeginScope(new Dictionary<string, object> { ["Carrier"] = Carrier });

        _logger.LogInformation("Calling carrier {Carrier} to create a shipment", Carrier);
        var started = Stopwatch.GetTimestamp();

        try
        {
            var response = await _inner.CreateShipmentAsync(request, cancellationToken);
            var elapsedMs = Stopwatch.GetElapsedTime(started).TotalMilliseconds;

            if (response.Succeeded)
            {
                activity?.SetTag("carrier.outcome", "success");
                _logger.LogInformation(
                    "Carrier {Carrier} created shipment in {ElapsedMs:F0} ms (reference {CarrierReference})",
                    Carrier, elapsedMs, response.CarrierReference);
            }
            else
            {
                var errorCodes = response.Errors.Select(e => e.Code).ToList();
                activity?.SetTag("carrier.outcome", "rejected");
                activity?.SetStatus(ActivityStatusCode.Error, response.FailureReason);
                _logger.LogWarning(
                    "Carrier {Carrier} rejected shipment in {ElapsedMs:F0} ms with error codes {ErrorCodes}",
                    Carrier, elapsedMs, errorCodes);
            }

            return response;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            activity?.SetTag("carrier.outcome", "cancelled");
            _logger.LogWarning(
                "Carrier {Carrier} call cancelled after {ElapsedMs:F0} ms",
                Carrier, Stopwatch.GetElapsedTime(started).TotalMilliseconds);
            throw;
        }
        catch (Exception ex)
        {
            activity?.SetTag("carrier.outcome", "error");
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            _logger.LogError(
                ex, "Carrier {Carrier} call failed after {ElapsedMs:F0} ms",
                Carrier, Stopwatch.GetElapsedTime(started).TotalMilliseconds);
            throw;
        }
    }
}
