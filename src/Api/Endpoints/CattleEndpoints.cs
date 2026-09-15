// <copyright file="CattleEndpoints.cs" company="Defra">
// Copyright (c) Defra. All rights reserved.
// </copyright>

namespace Defra.Lis.Api.Endpoints;

using Defra.Lis.Api.Interfaces;
using Defra.Lis.Api.Models;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

public static class CattleEndpoints
{
    public static IEndpointRouteBuilder MapCattleEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/holdings")
                       .WithTags("Cattle");

        group.MapGet("/{county}/{parish}/{holding}", GetHolding)
             .WithName("GetHolding")
             .Produces<HoldingResponse>(StatusCodes.Status200OK)
             .ProducesProblem(StatusCodes.Status400BadRequest)
             .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapGet("/{cph1}/{cph2}/{cph3}/cattle", (string cph1, string cph2, string cph3, [AsParameters] CattleFilter filter, [FromServices] ICattleService cattleService, CancellationToken cancellationToken) =>
                 GetCattleForHolding($"{cph1}/{cph2}/{cph3}", filter, cattleService, cancellationToken))
             .WithName("GetCattleForHoldingMultiSegment")
             .Produces<IEnumerable<CattleResponse>>(StatusCodes.Status200OK)
             .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapGet("/{cph}/cattle", (string cph, [AsParameters] CattleFilter filter, [FromServices] ICattleService cattleService, CancellationToken cancellationToken) =>
                 GetCattleForHolding(cph, filter, cattleService, cancellationToken))
             .WithName("GetCattleForHolding")
             .Produces<IEnumerable<CattleResponse>>(StatusCodes.Status200OK)
             .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapGet("/{cph1}/{cph2}/{cph3}/bundles", (string cph1, string cph2, string cph3, [FromServices] ICattleService cattleService) =>
                 GetBundlesForHolding($"{cph1}/{cph2}/{cph3}", cattleService))
             .WithName("GetBundlesForHoldingMultiSegment")
             .Produces<IEnumerable<BundleResponse>>(StatusCodes.Status200OK);

        group.MapGet("/{cph}/bundles", (string cph, [FromServices] ICattleService cattleService) =>
                 GetBundlesForHolding(cph, cattleService))
             .WithName("GetBundlesForHolding")
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
