using System.Text.Json;
using FluentValidation.Results;
using MultiCarrierApi.Application.Common.Exceptions;
using MultiCarrierApi.Web.Infrastructure;
using Microsoft.AspNetCore.Http;

namespace MultiCarrierApi.Application.FunctionalTests.Infrastructure;

public class ProblemDetailsExceptionHandlerTests
{
    [Test]
    public async Task ShouldReturnEveryValidationErrorGroupedByProperty()
    {
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();
        var exception = new ValidationException(
        [
            new ValidationFailure("ShipTo.Name", "Name is required."),
            new ValidationFailure("ShipTo.City", "City is required."),
            new ValidationFailure("ShipTo.City", "City is too long.")
        ]);

        var handled = await new ProblemDetailsExceptionHandler().TryHandleAsync(context, exception, CancellationToken.None);

        handled.ShouldBeTrue();
        context.Response.StatusCode.ShouldBe(StatusCodes.Status400BadRequest);

        context.Response.Body.Position = 0;
        using var json = await JsonDocument.ParseAsync(context.Response.Body);
        var errors = json.RootElement.GetProperty("errors");
        errors.GetProperty("ShipTo.Name").EnumerateArray().Select(e => e.GetString())
            .ShouldBe(["Name is required."]);
        errors.GetProperty("ShipTo.City").EnumerateArray().Select(e => e.GetString())
            .ShouldBe(["City is required.", "City is too long."]);
    }
}
