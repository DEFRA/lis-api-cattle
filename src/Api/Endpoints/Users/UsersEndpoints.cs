// <copyright file="UsersEndpoints.cs" company="Defra">
// Copyright (c) Defra. All rights reserved.
// </copyright>

namespace Defra.Lis.Api.Endpoints.Users;

using System.Diagnostics.CodeAnalysis;
using Defra.Lis.Api.Authentication;
using Defra.Lis.Api.Interfaces;
using Defra.Lis.Api.Models.Requests;
using Defra.Lis.Api.Models.Responses;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

/// <summary>
/// Provides endpoint mappings for user details queries and user account creation.
/// </summary>
public partial class UsersEndpoints
{
    [ExcludeFromCodeCoverage]
    protected UsersEndpoints()
    {
    }

    /// <summary>
    /// Maps the user endpoints onto the supplied route builder.
    /// </summary>
    /// <param name="app">The endpoint route builder to which the endpoints are added.</param>
    /// <returns>The supplied endpoint route builder.</returns>
    public static IEndpointRouteBuilder MapUserEndpoints(IEndpointRouteBuilder app)
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

        group.MapPost("/", CreateUser)
            .WithName(OpenApiMetadata.CreateUserRoute.Name)
            .WithSummary(OpenApiMetadata.CreateUserRoute.Summary)
            .WithDescription(OpenApiMetadata.CreateUserRoute.Description)
            .Accepts<CreateUserRequest>("application/json")
            .Produces<UserDetailsResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status500InternalServerError)
            .ProducesProblem(StatusCodes.Status503ServiceUnavailable);

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

    private static async Task<IResult> CreateUser(
        [FromBody] CreateUserRequest? request,
        [FromServices] IKrdsService krdsService,
        [FromServices] ILogger<UsersEndpoints> logger,
        CancellationToken cancellationToken)
    {
        if (request is null)
        {
            throw new ArgumentException("A request body is required.", nameof(request));
        }

        // KRDS answers 201 for a new account and 200 for a refreshed one, but the REST strategy does not expose the
        // status, so both are returned as 200 (LREG-660).
        try
        {
            logger.LogInformation("Pre get holding");
            var tmp = await krdsService.GetHoldingAsync("10", "123", "1234", cancellationToken);
            LogTmpTmp(logger, tmp);
            logger.LogInformation("Post get holding");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting holding");
        }

        logger.LogInformation("Pre update user");
        var details = await krdsService.EnsureUserAccountAsync(request, cancellationToken);
        logger.LogInformation("Post update user");
        return Results.Ok(details);
    }

    [LoggerMessage(LogLevel.Information, "{{Message: {Tmp}}}")]
    static partial void LogTmpTmp(ILogger<UsersEndpoints> logger, HoldingDetailedResponse tmp);
}
