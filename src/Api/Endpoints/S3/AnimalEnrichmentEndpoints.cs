// <copyright file="AnimalEnrichmentEndpoints.cs" company="Defra">
// Copyright (c) Defra. All rights reserved.
// </copyright>

namespace Defra.Lis.Api.Endpoints.S3;

using System.Text.Json;
using Defra.Lis.Api.Authentication;
using Defra.Lis.Api.Interfaces;
using Defra.Lis.Api.Models.Cads;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

/// <summary>
/// Provides endpoint mappings for the temporary CADS animal enrichment files.
/// </summary>
public static class AnimalEnrichmentEndpoints
{
    /// <summary>Maps the versioned animal enrichment endpoints.</summary>
    /// <returns>The supplied endpoint route builder.</returns>
    public static IEndpointRouteBuilder MapAnimalEnrichmentEndpoints(this IEndpointRouteBuilder app)
    {
        var versionOne = app.NewVersionedApi(OpenApiMetadata.Tag)
            .MapGroup(RouteNames.ApiVersionRoot)
            .HasApiVersion(1.0)
            .RequireServiceToServiceAuthorization();
        var group = versionOne.MapGroup(RouteNames.AnimalEnrichment).WithTags(OpenApiMetadata.Tag);

        group.MapGet("/", CphList)
            .WithName("ListAnimalEnrichmentCphs")
            .WithSummary("List holdings with animal enrichment files")
            .Produces<IReadOnlyList<string>>();

        group.MapGet("/{county}/{parish}/{holding}", EarTagList)
            .WithName("ListAnimalEnrichmentEarTags")
            .WithSummary("List animal enrichment ear tags for a holding")
            .Produces<IReadOnlyList<string>>();

        group.MapGet("/{county}/{parish}/{holding}/{key}", GetDetail)
            .WithName("GetAnimalEnrichment")
            .WithSummary("Get an animal enrichment file")
            .Produces<CadsAnimal>()
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPut("/{county}/{parish}/{holding}/{key}", PutFile)
            .WithName("UpsertAnimalEnrichment")
            .WithSummary("Add or replace an animal enrichment file")
            .Accepts<JsonElement>("application/json")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status503ServiceUnavailable);

        group.MapDelete("/{county}/{parish}/{holding}/{key}", DeleteFile)
            .WithName("DeleteAnimalEnrichment")
            .WithSummary("Delete an animal enrichment file")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status503ServiceUnavailable);

        return app;
    }

    private static async Task<IResult> CphList(
        [FromServices] ICadsDataPatchStore store,
        CancellationToken cancellationToken) =>
        Results.Ok(await store.GetCphListAsync(cancellationToken));

    private static async Task<IResult> EarTagList(
        string county,
        string parish,
        string holding,
        [FromServices] ICadsDataPatchStore store,
        CancellationToken cancellationToken) =>
        Results.Ok(await store.GetCphEarTagsAsync($"{county}/{parish}/{holding}", cancellationToken));

    private static async Task<IResult> GetDetail(
        string county,
        string parish,
        string holding,
        string key,
        [FromServices] ICadsDataPatchStore store,
        CancellationToken cancellationToken)
    {
        var animal = await store.GetAnimalAsync($"{county}/{parish}/{holding}", key, cancellationToken);
        return animal is null
            ? Results.Problem(statusCode: StatusCodes.Status404NotFound, detail: "The animal enrichment file was not found.")
            : Results.Ok(animal);
    }

    private static async Task<IResult> PutFile(
        string county,
        string parish,
        string holding,
        string key,
        [FromBody] JsonElement animal,
        [FromServices] ICadsDataPatchStore store,
        CancellationToken cancellationToken)
    {
        var saved = await store.UpsertAnimalAsync($"{county}/{parish}/{holding}", key, animal.GetRawText(), cancellationToken);
        return saved ? Results.NoContent() : PatchDisabled();
    }

    private static async Task<IResult> DeleteFile(
        string county,
        string parish,
        string holding,
        string key,
        [FromServices] ICadsDataPatchStore store,
        CancellationToken cancellationToken)
    {
        var deleted = await store.DeleteAnimalAsync($"{county}/{parish}/{holding}", key, cancellationToken);
        return deleted ? Results.NoContent() : PatchDisabled();
    }

    private static IResult PatchDisabled() => Results.Problem(
        statusCode: StatusCodes.Status503ServiceUnavailable,
        detail: "The CADS data patch bucket is not configured.");
}
