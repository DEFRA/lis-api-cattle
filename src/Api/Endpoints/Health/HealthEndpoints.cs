// <copyright file="HealthEndpoints.cs" company="Defra">
// Copyright (c) Defra. All rights reserved.
// </copyright>

namespace Defra.Lis.Api.Endpoints.Health;

using System.Net.Mime;
using System.Text.Json;
using Defra.Lis.Api.Correlation;
using Defra.Lis.Api.Interfaces;
using Defra.Lis.Api.Models;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Diagnostics.HealthChecks;

/// <summary>
/// Provides endpoint mappings for health check endpoints.
/// </summary>
public static class HealthEndpoints
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = false,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    /// <summary>
    /// Maps health check endpoints onto the supplied route builder.
    /// </summary>
    /// <param name="app">The endpoint route builder to which the endpoints are added.</param>
    /// <returns>The supplied endpoint route builder.</returns>
    public static IEndpointRouteBuilder MapHealthEndpoints(this IEndpointRouteBuilder app)
    {
        var healthOptions = new HealthCheckOptions
        {
            ResponseWriter = async (context, report) =>
            {
                context.Response.ContentType = MediaTypeNames.Application.Json;

                var entries = report.Entries.ToDictionary(
                    entry => entry.Key,
                    entry => new ComponentHealthResult(
                        entry.Value.Status.ToString(),
                        entry.Value.Description,
                        entry.Value.Duration,
                        entry.Value.Data));

                var response = new HealthCheckResponse(
                    report.Status.ToString(),
                    report.TotalDuration,
                    entries);

                var json = JsonSerializer.Serialize(response, JsonOptions);
                await context.Response.WriteAsync(json);
            },
        };

        app.MapHealthChecks("/health", healthOptions)
           .WithName(OpenApiMetadata.GetHealthRoute.Name)
           .WithTags(OpenApiMetadata.Tag)
           .WithSummary(OpenApiMetadata.GetHealthRoute.Summary)
           .WithDescription(OpenApiMetadata.GetHealthRoute.Description)
           .WithMetadata(new ProducesResponseTypeAttribute(typeof(HealthCheckResponse), StatusCodes.Status200OK))
           .WithMetadata(new ProducesResponseTypeAttribute(typeof(HealthCheckResponse), StatusCodes.Status503ServiceUnavailable))
           .WithoutCorrelationIdCheck();

        app.MapGet("/health/detailed", async (IHealthService healthService, CancellationToken cancellationToken) =>
        {
            var result = await healthService.CheckHealthAsync(cancellationToken);
            return result.Status == nameof(HealthStatus.Healthy)
                ? Results.Ok(result)
                : Results.Json(result, statusCode: StatusCodes.Status503ServiceUnavailable);
        })
        .WithName(OpenApiMetadata.GetDetailedHealthRoute.Name)
        .WithTags(OpenApiMetadata.Tag)
        .WithSummary(OpenApiMetadata.GetDetailedHealthRoute.Summary)
        .WithDescription(OpenApiMetadata.GetDetailedHealthRoute.Description)
        .Produces<HealthCheckResponse>(StatusCodes.Status200OK)
        .Produces<HealthCheckResponse>(StatusCodes.Status503ServiceUnavailable)
        .WithoutCorrelationIdCheck();

        return app;
    }
}
