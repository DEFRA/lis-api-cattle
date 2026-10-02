// <copyright file="HealthEndpointsTests.cs" company="Defra">
// Copyright (c) Defra. All rights reserved.
// </copyright>

namespace Defra.Lis.Api.Tests;

using System.Net;
using System.Net.Http.Json;
using Defra.Lis.Api.Endpoints.Health;
using Defra.Lis.Api.Interfaces;
using Defra.Lis.Api.Models;
using Defra.Lis.Api.Services.Health;
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
        // Arrange
        var builder = WebApplication.CreateEmptyBuilder(new WebApplicationOptions());
        builder.WebHost.UseTestServer();
        builder.Services.AddRouting();
        builder.Services.AddHealthChecks();
        var app = builder.Build();

        // Act
        var returned = app.MapHealthEndpoints();

        // Assert
        Assert.NotNull(returned);
    }

    [Fact]
    public async Task MapHealthEndpoints_DescribesRoutesForOpenApi()
    {
        // Arrange
        var mockHealthService = new Mock<IHealthService>();
        var builder = WebApplication.CreateEmptyBuilder(new WebApplicationOptions());
        builder.WebHost.UseTestServer();
        builder.Services.AddRouting();
        builder.Services.AddHealthChecks();
        builder.Services.AddSingleton(mockHealthService.Object);
        var app = builder.Build();
        app.MapHealthEndpoints();
        await app.StartAsync(TestContext.Current.CancellationToken);

        // Act
        var endpoints = app.Services.GetRequiredService<Microsoft.AspNetCore.Routing.EndpointDataSource>().Endpoints
            .OfType<Microsoft.AspNetCore.Routing.RouteEndpoint>()
            .ToList();

        // Assert
        var healthEndpoint = endpoints.Single(e => e.RoutePattern.RawText == "/health");
        Assert.Equal("/health", healthEndpoint.RoutePattern.RawText);
        Assert.Equal(
            OpenApiMetadata.GetHealthRoute.Summary,
            healthEndpoint.Metadata.GetMetadata<Microsoft.AspNetCore.Http.Metadata.IEndpointSummaryMetadata>()?.Summary);
        Assert.Equal(
            OpenApiMetadata.GetHealthRoute.Description,
            healthEndpoint.Metadata.GetMetadata<Microsoft.AspNetCore.Http.Metadata.IEndpointDescriptionMetadata>()?.Description);
        Assert.Contains(
            OpenApiMetadata.Tag,
            healthEndpoint.Metadata.GetMetadata<Microsoft.AspNetCore.Http.Metadata.ITagsMetadata>()!.Tags);

        var detailedEndpoint = endpoints.Single(e => e.RoutePattern.RawText == "/health/detailed");
        Assert.Equal("/health/detailed", detailedEndpoint.RoutePattern.RawText);
        Assert.Equal(
            OpenApiMetadata.GetDetailedHealthRoute.Summary,
            detailedEndpoint.Metadata.GetMetadata<Microsoft.AspNetCore.Http.Metadata.IEndpointSummaryMetadata>()?.Summary);
        Assert.Equal(
            OpenApiMetadata.GetDetailedHealthRoute.Description,
            detailedEndpoint.Metadata.GetMetadata<Microsoft.AspNetCore.Http.Metadata.IEndpointDescriptionMetadata>()?.Description);
        Assert.Contains(
            OpenApiMetadata.Tag,
            detailedEndpoint.Metadata.GetMetadata<Microsoft.AspNetCore.Http.Metadata.ITagsMetadata>()!.Tags);
    }

    [Fact]
    public async Task GetHealth_WhenAllChecksPass_Returns200WithJsonResponse()
    {
        // Arrange
        var mockHealthService = new Mock<IHealthService>();
        mockHealthService.Setup(s => s.CheckDatabaseHealthAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ComponentHealthResult(nameof(HealthStatus.Healthy), "Database is up."));
        mockHealthService.Setup(s => s.CheckQueueHealthAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ComponentHealthResult(nameof(HealthStatus.Healthy), "Queue is up."));
        mockHealthService.Setup(s => s.CheckQuartzHealthAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ComponentHealthResult(nameof(HealthStatus.Healthy), "Quartz is up."));

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

        // Act
        var response = await client.GetAsync("/health", TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result =
            await response.Content.ReadFromJsonAsync<HealthCheckResponse>(TestContext.Current.CancellationToken);
        Assert.NotNull(result);
        Assert.Equal(nameof(HealthStatus.Healthy), result.Status);
        Assert.Equal(3, result.Entries.Count);
        Assert.Equal(nameof(HealthStatus.Healthy), result.Entries["database"].Status);
        Assert.Equal(nameof(HealthStatus.Healthy), result.Entries["queue"].Status);
        Assert.Equal(nameof(HealthStatus.Healthy), result.Entries["quartz"].Status);
    }

    [Fact]
    public async Task GetHealth_WhenCheckFails_Returns503WithJsonResponse()
    {
        // Arrange
        var mockHealthService = new Mock<IHealthService>();
        mockHealthService.Setup(s => s.CheckDatabaseHealthAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ComponentHealthResult(nameof(HealthStatus.Healthy), "Database is up."));
        mockHealthService.Setup(s => s.CheckQueueHealthAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ComponentHealthResult(nameof(HealthStatus.Unhealthy), "Queue is down."));

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

        // Act
        var response = await client.GetAsync("/health", TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        var result =
            await response.Content.ReadFromJsonAsync<HealthCheckResponse>(TestContext.Current.CancellationToken);
        Assert.NotNull(result);
        Assert.Equal(nameof(HealthStatus.Unhealthy), result.Status);
        Assert.Equal(nameof(HealthStatus.Healthy), result.Entries["database"].Status);
        Assert.Equal(nameof(HealthStatus.Unhealthy), result.Entries["queue"].Status);
    }

    [Fact]
    public async Task GetDetailedHealth_WhenHealthy_Returns200()
    {
        // Arrange
        var mockHealthService = new Mock<IHealthService>();
        var healthResponse = new HealthCheckResponse(
            nameof(HealthStatus.Healthy),
            TimeSpan.FromMilliseconds(15),
            new Dictionary<string, ComponentHealthResult>
            {
                ["database"] = new(nameof(HealthStatus.Healthy), "DB Healthy"),
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

        // Act
        var response = await client.GetAsync("/health/detailed", TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result =
            await response.Content.ReadFromJsonAsync<HealthCheckResponse>(TestContext.Current.CancellationToken);
        Assert.NotNull(result);
        Assert.Equal(nameof(HealthStatus.Healthy), result.Status);
    }

    [Fact]
    public async Task GetDetailedHealth_WhenUnhealthy_Returns503()
    {
        // Arrange
        var mockHealthService = new Mock<IHealthService>();
        var healthResponse = new HealthCheckResponse(
            nameof(HealthStatus.Unhealthy),
            TimeSpan.FromMilliseconds(15),
            new Dictionary<string, ComponentHealthResult>
            {
                ["database"] = new(nameof(HealthStatus.Unhealthy), "DB Unhealthy"),
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

        // Act
        var response = await client.GetAsync("/health/detailed", TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        var result =
            await response.Content.ReadFromJsonAsync<HealthCheckResponse>(TestContext.Current.CancellationToken);
        Assert.NotNull(result);
        Assert.Equal(nameof(HealthStatus.Unhealthy), result.Status);
    }

    [Fact]
    public async Task GetDetailedHealth_WhenDegraded_Returns503()
    {
        // Arrange
        var mockHealthService = new Mock<IHealthService>();
        var healthResponse = new HealthCheckResponse(
            nameof(HealthStatus.Degraded),
            TimeSpan.FromMilliseconds(20),
            new Dictionary<string, ComponentHealthResult>
            {
                ["quartz"] = new(nameof(HealthStatus.Degraded), "Quartz Degraded"),
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

        // Act
        var response = await client.GetAsync("/health/detailed", TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        var result =
            await response.Content.ReadFromJsonAsync<HealthCheckResponse>(TestContext.Current.CancellationToken);
        Assert.NotNull(result);
        Assert.Equal(HealthStatus.Degraded.ToString(), result.Status);
    }

    [Fact]
    public async Task GetHealth_WhenCheckIsDegraded_Returns200WithDegradedJsonResponse()
    {
        // Arrange
        var mockHealthService = new Mock<IHealthService>();
        mockHealthService.Setup(s => s.CheckDatabaseHealthAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ComponentHealthResult(nameof(HealthStatus.Healthy), "Database is up."));
        mockHealthService.Setup(s => s.CheckQueueHealthAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ComponentHealthResult(nameof(HealthStatus.Degraded), "Queue is degraded."));

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

        // Act
        var response = await client.GetAsync("/health", TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result =
            await response.Content.ReadFromJsonAsync<HealthCheckResponse>(TestContext.Current.CancellationToken);
        Assert.NotNull(result);
        Assert.Equal(nameof(HealthStatus.Degraded), result.Status);
        Assert.Equal(nameof(HealthStatus.Healthy), result.Entries["database"].Status);
        Assert.Equal(nameof(HealthStatus.Degraded), result.Entries["queue"].Status);
    }
}
