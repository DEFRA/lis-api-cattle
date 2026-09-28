// <copyright file="CattleEndpoints.cs" company="Defra">
// Copyright (c) Defra. All rights reserved.
// </copyright>

namespace Defra.Lis.Api.Endpoints.Cattle;

using Defra.Lis.Api.Interfaces;
using Defra.Lis.Api.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

/// <summary>
/// Provides endpoint mappings for holding cattle and registration bundle queries.
/// </summary>
public static class CattleEndpoints
{
    /// <summary>
    /// Maps the versioned cattle endpoints onto the supplied route builder.
    /// </summary>
    /// <param name="app">The endpoint route builder to which the endpoints are added.</param>
    /// <returns>The supplied endpoint route builder.</returns>
    public static IEndpointRouteBuilder MapCattleEndpoints(this IEndpointRouteBuilder app)
    {
        var cattleApi = app.NewVersionedApi(OpenApiMetadata.Tag);
        var versionOne = cattleApi.MapGroup(RouteNames.ApiVersionRoot)
            .HasApiVersion(1.0);
        var group = versionOne.MapGroup(RouteNames.Cattle)
            .WithTags(OpenApiMetadata.Tag);

        group.MapGet("/{earTag}", GetCattleDetails)
            .WithName("GetCattleDetails")
            .Produces<CattleDetailsResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status404NotFound);

        return app;
    }

    private static async Task<IResult> GetCattleDetails(
        string earTag,
        [FromServices] ICattleService cattleService,
        CancellationToken cancellationToken)
    {
        var decodedEarTag = Uri.UnescapeDataString(earTag);
        var details = await cattleService.GetCattleDetailsAsync(decodedEarTag, cancellationToken);
        return Results.Ok(details);
    }
}
