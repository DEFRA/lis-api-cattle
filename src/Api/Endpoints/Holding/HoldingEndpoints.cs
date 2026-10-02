// <copyright file="HoldingEndpoints.cs" company="Defra">
// Copyright (c) Defra. All rights reserved.
// </copyright>

namespace Defra.Lis.Api.Endpoints.Holding;

using Defra.Lis.Api.Authentication;
using Defra.Lis.Api.Interfaces;
using Defra.Lis.Api.Models;
using Defra.Lis.Api.Models.Responses;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

/// <summary>
/// Provides endpoint mappings for holding details and related cattle and registration bundle queries.
/// </summary>
public static class HoldingEndpoints
{
    /// <summary>
    /// Maps the versioned holding endpoints onto the supplied route builder.
    /// </summary>
    /// <param name="app">The endpoint route builder to which the endpoints are added.</param>
    /// <returns>The supplied endpoint route builder.</returns>
    public static IEndpointRouteBuilder MapHoldingEndpoints(this IEndpointRouteBuilder app)
    {
        var cattleApi = app.NewVersionedApi(OpenApiMetadata.Tag);
        var versionOne = cattleApi.MapGroup(RouteNames.ApiVersionRoot)
            .HasApiVersion(1.0)
            .RequireServiceToServiceAuthorization();
        var group = versionOne.MapGroup(RouteNames.Holdings)
            .WithTags(OpenApiMetadata.Tag);

        group.MapGet("/{county}/{parish}/{holding}", GetHolding)
             .WithName(OpenApiMetadata.GetHoldingRoute.Name)
             .WithSummary(OpenApiMetadata.GetHoldingRoute.Summary)
             .WithDescription(OpenApiMetadata.GetHoldingRoute.Description)
             .Produces<HoldingResponse>(StatusCodes.Status200OK)
             .ProducesProblem(StatusCodes.Status400BadRequest)
             .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapGet(
                "/{county}/{parish}/{holding}/cattle",
                (string county, string parish, string holding, [AsParameters] CattleFilter filter, [FromServices] ICattleService cattleService, CancellationToken cancellationToken) =>
                 GetCattleForHolding($"{county}/{parish}/{holding}", filter, cattleService, cancellationToken))
            .WithName(OpenApiMetadata.GetCattleForHoldingMultiSegmentRoute.Name)
            .WithSummary(OpenApiMetadata.GetCattleForHoldingMultiSegmentRoute.Summary)
            .WithDescription(OpenApiMetadata.GetCattleForHoldingMultiSegmentRoute.Description)
            .Produces<IEnumerable<CattleResponse>>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapGet(
                "/{cph}/cattle",
                (string cph, [AsParameters] CattleFilter filter, [FromServices] ICattleService cattleService, CancellationToken cancellationToken) =>
                 GetCattleForHolding(cph, filter, cattleService, cancellationToken))
            .WithName(OpenApiMetadata.GetCattleForHoldingRoute.Name)
            .WithSummary(OpenApiMetadata.GetCattleForHoldingRoute.Summary)
            .WithDescription(OpenApiMetadata.GetCattleForHoldingRoute.Description)
            .Produces<IEnumerable<CattleResponse>>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapGet(
                "/{county}/{parish}/{holding}/bundles",
                (string county, string parish, string holding, [FromServices] ICattleService cattleService) =>
                GetBundlesForHolding($"{county}/{parish}/{holding}", cattleService))
             .WithName(OpenApiMetadata.GetBundlesForHoldingMultiSegmentRoute.Name)
             .WithSummary(OpenApiMetadata.GetBundlesForHoldingMultiSegmentRoute.Summary)
             .WithDescription(OpenApiMetadata.GetBundlesForHoldingMultiSegmentRoute.Description)
             .Produces<IEnumerable<BundleResponse>>(StatusCodes.Status200OK);

        group.MapGet(
                "/{cph}/bundles",
                (string cph, [FromServices] ICattleService cattleService) =>
                GetBundlesForHolding(cph, cattleService))
             .WithName(OpenApiMetadata.GetBundlesForHoldingRoute.Name)
             .WithSummary(OpenApiMetadata.GetBundlesForHoldingRoute.Summary)
             .WithDescription(OpenApiMetadata.GetBundlesForHoldingRoute.Description)
             .Produces<IEnumerable<BundleResponse>>(StatusCodes.Status200OK);

        return app;
    }

    private static async Task<IResult> GetHolding(
        string county,
        string parish,
        string holding,
        [FromServices] IKrdsService krdsService,
        CancellationToken cancellationToken)
    {
        var response = await krdsService.GetHoldingAsync(county, parish, holding, cancellationToken);
        return Results.Ok(response);
    }

    private static async Task<IResult> GetCattleForHolding(
        string cph,
        CattleFilter filter,
        [FromServices] ICattleService cattleService,
        CancellationToken cancellationToken)
    {
        var decodedCph = Uri.UnescapeDataString(cph);
        var cattle = await cattleService.GetCattleForHoldingAsync(decodedCph, filter, cancellationToken);
        return Results.Ok(cattle);
    }

    private static async Task<IResult> GetBundlesForHolding(string cph, [FromServices] ICattleService cattleService)
    {
        var decodedCph = Uri.UnescapeDataString(cph);
        var bundles = await cattleService.GetBundlesForHoldingAsync(decodedCph);
        return Results.Ok(bundles);
    }
}
