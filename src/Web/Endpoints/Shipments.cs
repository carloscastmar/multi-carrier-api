using MultiCarrierApi.Application.Shipments.Commands.CreateShipment;
using MultiCarrierApi.Application.Shipments.Models;
using MultiCarrierApi.Application.Shipments.Queries.GetShipmentById;
using Microsoft.AspNetCore.Http.HttpResults;

namespace MultiCarrierApi.Web.Endpoints;

public class Shipments : IEndpointGroup
{
    public static void Map(RouteGroupBuilder groupBuilder)
    {
        groupBuilder.RequireAuthorization();

        groupBuilder.MapPost(CreateShipment, "");
        groupBuilder.MapGet(GetShipment, "{id}");
    }

    [EndpointSummary("Create a shipment")]
    [EndpointDescription("Creates a shipment with the selected carrier and returns tracking and label data.")]
    public static async Task<Results<Created<CreateShipmentResult>, UnprocessableEntity<CreateShipmentResult>>> CreateShipment(
        ISender sender,
        CreateShipmentCommand command)
    {
        var result = await sender.Send(command);

        if (!result.Succeeded)
        {
            return TypedResults.UnprocessableEntity(result);
        }

        return TypedResults.Created($"/api/Shipments/{result.ShipmentId}", result);
    }

    [EndpointSummary("Get a shipment by ID")]
    [EndpointDescription("Retrieves a previously created shipment by its local ID.")]
    public static async Task<Results<Ok<ShipmentDto>, NotFound>> GetShipment(ISender sender, int id)
    {
        var shipment = await sender.Send(new GetShipmentByIdQuery(id));

        if (shipment is null)
        {
            return TypedResults.NotFound();
        }

        return TypedResults.Ok(shipment);
    }
}
