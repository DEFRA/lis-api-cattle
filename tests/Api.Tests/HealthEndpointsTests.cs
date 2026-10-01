// <copyright file="HealthEndpointsTests.cs" company="Defra">
// Copyright (c) Defra. All rights reserved.
// </copyright>

namespace Defra.Lis.Api.Tests;

using System.Net;
using System.Net.Http.Json;
using Defra.Lis.Api.Endpoints.Health;
using Defra.Lis.Api.Interfaces;
using Defra.Lis.Api.Models;
using Defra.Lis.Api.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Moq;

public class HealthEndpointsTests
{
    [Fact]
    public void MapHealthEndpoints_MapsRoutesSuccessfully()
    {
        var builder = WebApplication.CreateEmptyBuilder(new WebApplicationOptions());
        builder.WebHost.UseTestServer();
        builder.Services.AddRouting();
        builder.Services.AddHealthChecks();
        var app = builder.Build();

        var returned = app.MapHealthEndpoints();

        Assert.NotNull(returned);
    }

    [Fact]
    public async Task MapHealthEndpoints_DescribesRoutesForOpenApi()
    {
        var mockHealthService = new Mock<IHealthService>();
        var builder = WebApplication.CreateEmptyBuilder(new WebApplicationOptions());
        builder.WebHost.UseTestServer();
        builder.Services.AddRouting();
        builder.Services.AddHealthChecks();
        builder.Services.AddSingleton(mockHealthService.Object);
        var app = builder.Build();
        app.MapHealthEndpoints();
        await app.StartAsync(TestContext.Current.CancellationToken);

        var endpoints = app.Services.GetRequiredService<Microsoft.AspNetCore.Routing.EndpointDataSource>().Endpoints
            .OfType<Microsoft.AspNetCore.Routing.RouteEndpoint>()
            .ToList();

        var healthEndpoint = endpoints.Single(e => e.RoutePattern.RawText == "/health");
        Assert.Equal("/health", healthEndpoint.RoutePattern.RawText);
        Assert.Equal(OpenApiMetadata.GetHealthRoute.Summary, healthEndpoint.Metadata.GetMetadata<Microsoft.AspNetCore.Http.Metadata.IEndpointSummaryMetadata>()?.Summary);
        Assert.Equal(OpenApiMetadata.GetHealthRoute.Description, healthEndpoint.Metadata.GetMetadata<Microsoft.AspNetCore.Http.Metadata.IEndpointDescriptionMetadata>()?.Description);
        Assert.Contains(OpenApiMetadata.Tag, healthEndpoint.Metadata.GetMetadata<Microsoft.AspNetCore.Http.Metadata.ITagsMetadata>()!.Tags);

        var detailedEndpoint = endpoints.Single(e => e.RoutePattern.RawText == "/health/detailed");
        Assert.Equal("/health/detailed", detailedEndpoint.RoutePattern.RawText);
        Assert.Equal(OpenApiMetadata.GetDetailedHealthRoute.Summary, detailedEndpoint.Metadata.GetMetadata<Microsoft.AspNetCore.Http.Metadata.IEndpointSummaryMetadata>()?.Summary);
        Assert.Equal(OpenApiMetadata.GetDetailedHealthRoute.Description, detailedEndpoint.Metadata.GetMetadata<Microsoft.AspNetCore.Http.Metadata.IEndpointDescriptionMetadata>()?.Description);
        Assert.Contains(OpenApiMetadata.Tag, detailedEndpoint.Metadata.GetMetadata<Microsoft.AspNetCore.Http.Metadata.ITagsMetadata>()!.Tags);
    }

    [Fact]
    public async Task GetHealth_WhenAllChecksPass_Returns200WithJsonResponse()
    {
        var mockHealthService = new Mock<IHealthService>();
        mockHealthService.Setup(s => s.CheckDatabaseHealthAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ComponentHealthResult(HealthStatus.Healthy.ToString(), "Database is up."));
        mockHealthService.Setup(s => s.CheckQueueHealthAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ComponentHealthResult(HealthStatus.Healthy.ToString(), "Queue is up."));
        mockHealthService.Setup(s => s.CheckQuartzHealthAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ComponentHealthResult(HealthStatus.Healthy.ToString(), "Quartz is up."));

        var builder = WebApplication.CreateEmptyBuilder(new WebApplicationOptions());
        builder.WebHost.UseTestServer();
        builder.Services.AddRouting();
        builder.Services.AddSingleton(mockHealthService.Object);
        builder.Services.AddHealthChecks()
            .AddCheck<DatabaseHealthCheck>("database")
            .AddCheck<QueueHealthCheck>("queue")
            .AddCheck<QuartzHealthCheck>("quartz");

        var app = builder.Build();
        app.MapHealthEndpoints();
        await app.StartAsync(TestContext.Current.CancellationToken);

        var client = app.GetTestClient();
        var response = await client.GetAsync("/health", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<HealthCheckResponse>(TestContext.Current.CancellationToken);
        Assert.NotNull(result);
        Assert.Equal(HealthStatus.Healthy.ToString(), result.Status);
        Assert.Equal(3, result.Entries.Count);
        Assert.Equal(HealthStatus.Healthy.ToString(), result.Entries["database"].Status);
        Assert.Equal(HealthStatus.Healthy.ToString(), result.Entries["queue"].Status);
        Assert.Equal(HealthStatus.Healthy.ToString(), result.Entries["quartz"].Status);
    }

    [Fact]
    public async Task GetHealth_WhenCheckFails_Returns503WithJsonResponse()
    {
        var mockHealthService = new Mock<IHealthService>();
        mockHealthService.Setup(s => s.CheckDatabaseHealthAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ComponentHealthResult(HealthStatus.Healthy.ToString(), "Database is up."));
        mockHealthService.Setup(s => s.CheckQueueHealthAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ComponentHealthResult(HealthStatus.Unhealthy.ToString(), "Queue is down."));

        var builder = WebApplication.CreateEmptyBuilder(new WebApplicationOptions());
        builder.WebHost.UseTestServer();
        builder.Services.AddRouting();
        builder.Services.AddSingleton(mockHealthService.Object);
        builder.Services.AddHealthChecks()
            .AddCheck<DatabaseHealthCheck>("database")
            .AddCheck<QueueHealthCheck>("queue");

        var app = builder.Build();
        app.MapHealthEndpoints();
        await app.StartAsync(TestContext.Current.CancellationToken);

        var client = app.GetTestClient();
        var response = await client.GetAsync("/health", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<HealthCheckResponse>(TestContext.Current.CancellationToken);
        Assert.NotNull(result);
        Assert.Equal(HealthStatus.Unhealthy.ToString(), result.Status);
        Assert.Equal(HealthStatus.Healthy.ToString(), result.Entries["database"].Status);
        Assert.Equal(HealthStatus.Unhealthy.ToString(), result.Entries["queue"].Status);
    }

    [Fact]
    public async Task GetDetailedHealth_WhenHealthy_Returns200()
    {
        var mockHealthService = new Mock<IHealthService>();
        var healthResponse = new HealthCheckResponse(
            HealthStatus.Healthy.ToString(),
            TimeSpan.FromMilliseconds(15),
            new Dictionary<string, ComponentHealthResult>
            {
                ["database"] = new(HealthStatus.Healthy.ToString(), "DB Healthy"),
            });

        mockHealthService.Setup(s => s.CheckHealthAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(healthResponse);

        var builder = WebApplication.CreateEmptyBuilder(new WebApplicationOptions());
        builder.WebHost.UseTestServer();
        builder.Services.AddRouting();
        builder.Services.AddHealthChecks();
        builder.Services.AddSingleton(mockHealthService.Object);

        var app = builder.Build();
        app.MapHealthEndpoints();
        await app.StartAsync(TestContext.Current.CancellationToken);

        var client = app.GetTestClient();
        var response = await client.GetAsync("/health/detailed", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<HealthCheckResponse>(TestContext.Current.CancellationToken);
        Assert.NotNull(result);
        Assert.Equal(HealthStatus.Healthy.ToString(), result.Status);
    }

    [Fact]
    public async Task GetDetailedHealth_WhenUnhealthy_Returns503()
    {
        var mockHealthService = new Mock<IHealthService>();
        var healthResponse = new HealthCheckResponse(
            HealthStatus.Unhealthy.ToString(),
            TimeSpan.FromMilliseconds(15),
            new Dictionary<string, ComponentHealthResult>
            {
                ["database"] = new(HealthStatus.Unhealthy.ToString(), "DB Unhealthy"),
            });

        mockHealthService.Setup(s => s.CheckHealthAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(healthResponse);

        var builder = WebApplication.CreateEmptyBuilder(new WebApplicationOptions());
        builder.WebHost.UseTestServer();
        builder.Services.AddRouting();
        builder.Services.AddHealthChecks();
        builder.Services.AddSingleton(mockHealthService.Object);

        var app = builder.Build();
        app.MapHealthEndpoints();
        await app.StartAsync(TestContext.Current.CancellationToken);

        var client = app.GetTestClient();
        var response = await client.GetAsync("/health/detailed", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<HealthCheckResponse>(TestContext.Current.CancellationToken);
        Assert.NotNull(result);
        Assert.Equal(HealthStatus.Unhealthy.ToString(), result.Status);
    }
}
