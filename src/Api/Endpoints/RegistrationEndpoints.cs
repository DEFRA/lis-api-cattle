using Lis.Cattle.Interfaces;
using Lis.Cattle.Models;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace Lis.Cattle.Endpoints;

public static class RegistrationEndpoints
{
    public static IEndpointRouteBuilder MapRegistrationEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/registrations")
                       .WithTags("Registrations");

        group.MapPost("/", CreateRegistrationBundle)
             .WithName("CreateRegistrationBundle")
             .Produces<BundleResponse>(StatusCodes.Status201Created)
             .ProducesProblem(StatusCodes.Status400BadRequest);

        return app;
    }

    private static async Task<IResult> CreateRegistrationBundle(
        [FromBody] RegistrationBundleRequest request,
        [FromServices] ICattleService cattleService,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await cattleService.CreateRegistrationBundleAsync(request, cancellationToken);
            return Results.Created($"/holdings/{result.CountyParishHolding}/bundles", result);
        }
        catch (ArgumentException ex)
        {
            return Results.BadRequest(new ProblemDetails
            {
                Title = "Invalid registration bundle request",
                Detail = ex.Message,
                Status = StatusCodes.Status400BadRequest
            });
        }
    }
}