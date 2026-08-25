using Lis.Cattle.Interfaces;
using Lis.Cattle.Models;
using Lis.Cattle.Validation;
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

        group.MapPost("/{id:guid}/validate", ValidateRegistrationBundle)
             .WithName("ValidateRegistrationBundle")
             .Produces<SubmissionValidationResult>(StatusCodes.Status200OK)
             .ProducesProblem(StatusCodes.Status404NotFound);

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

    private static async Task<IResult> ValidateRegistrationBundle(
        [FromRoute] Guid id,
        [FromServices] ISubmissionValidationService validationService,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await validationService.ValidateSubmissionByIdAsync(id, cancellationToken);
            return Results.Ok(result);
        }
        catch (KeyNotFoundException ex)
        {
            return Results.NotFound(new ProblemDetails
            {
                Title = "Submission not found",
                Detail = ex.Message,
                Status = StatusCodes.Status404NotFound
            });
        }
    }
}