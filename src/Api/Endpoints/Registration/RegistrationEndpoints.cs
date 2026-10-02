// <copyright file="RegistrationEndpoints.cs" company="Defra">
// Copyright (c) Defra. All rights reserved.
// </copyright>

namespace Defra.Lis.Api.Endpoints.Registration;

using Defra.Lis.Api.Authentication;
using Defra.Lis.Api.Interfaces;
using Defra.Lis.Api.Models.Requests;
using Defra.Lis.Api.Models.Responses;
using Defra.Lis.Api.Validation;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

/// <summary>
/// Provides endpoint mappings for cattle registration bundle operations.
/// </summary>
public static class RegistrationEndpoints
{
    /// <summary>
    /// Maps the versioned cattle registration bundle endpoints onto the supplied route builder.
    /// </summary>
    /// <param name="app">The endpoint route builder to which the endpoints are added.</param>
    /// <returns>The supplied endpoint route builder.</returns>
    public static IEndpointRouteBuilder MapRegistrationEndpoints(this IEndpointRouteBuilder app)
    {
        var registrationApi = app.NewVersionedApi(OpenApiMetadata.Tag);
        var versionOne = registrationApi.MapGroup(RouteNames.ApiVersionRoot)
            .HasApiVersion(1.0)
            .RequireServiceToServiceAuthorization();
        var group = versionOne.MapGroup(RouteNames.Registrations)
            .WithTags(OpenApiMetadata.Tag);

        group.MapPost("/", CreateRegistrationBundle)
            .WithName(OpenApiMetadata.CreateRegistrationBundleRoute.Name)
            .WithSummary(OpenApiMetadata.CreateRegistrationBundleRoute.Summary)
            .WithDescription(OpenApiMetadata.CreateRegistrationBundleRoute.Description)
            .Produces<BundleResponse>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status400BadRequest);

        group.MapPost("/{id:guid}/validate", ValidateRegistrationBundle)
            .WithName(OpenApiMetadata.ValidateRegistrationBundleRoute.Name)
            .WithSummary(OpenApiMetadata.ValidateRegistrationBundleRoute.Summary)
            .WithDescription(OpenApiMetadata.ValidateRegistrationBundleRoute.Description)
            .Produces<SubmissionValidationResult>()
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
            return Results.Created($"/v1/holdings/{result.CountyParishHolding}/bundles", result);
        }
        catch (ArgumentException ex)
        {
            return Results.BadRequest(new ProblemDetails
            {
                Title = "Invalid registration bundle request",
                Detail = ex.Message,
                Status = StatusCodes.Status400BadRequest,
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
                Status = StatusCodes.Status404NotFound,
            });
        }
    }
}
