// <copyright file="UsersEndpoints.cs" company="Defra">
// Copyright (c) Defra. All rights reserved.
// </copyright>

namespace Defra.Lis.Api.Endpoints.Users;

using Defra.Lis.Api.Authentication;
using Defra.Lis.Api.Interfaces;
using Defra.Lis.Api.Models.Responses;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

/// <summary>
/// Provides endpoint mappings for user details queries.
/// </summary>
public static class UsersEndpoints
{
    /// <summary>
    /// Maps the user endpoints onto the supplied route builder.
    /// </summary>
    /// <param name="app">The endpoint route builder to which the endpoints are added.</param>
    /// <returns>The supplied endpoint route builder.</returns>
    public static IEndpointRouteBuilder MapUserEndpoints(this IEndpointRouteBuilder app)
    {
        var registrationApi = app.NewVersionedApi(OpenApiMetadata.Tag);
        var versionOne = registrationApi.MapGroup(RouteNames.ApiVersionRoot)
            .HasApiVersion(1.0)
            .RequireServiceToServiceAuthorization();
        var group = versionOne.MapGroup(RouteNames.Users)
            .WithTags(OpenApiMetadata.Tag);

        group.MapGet("/{subject}", GetUserDetails)
            .WithName(OpenApiMetadata.GetUserDetailsRoute.Name)
            .WithSummary(OpenApiMetadata.GetUserDetailsRoute.Summary)
            .WithDescription(OpenApiMetadata.GetUserDetailsRoute.Description)
            .Produces<UserDetailsResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status500InternalServerError);

        return app;
    }

    private static async Task<IResult> GetUserDetails(
        string subject,
        [FromServices] IKrdsService krdsService,
        CancellationToken cancellationToken)
    {
        var decodedSubject = Uri.UnescapeDataString(subject);
        var details = await krdsService.GetUserAccountAsync(decodedSubject, cancellationToken);
        return Results.Ok(details);
    }
}
