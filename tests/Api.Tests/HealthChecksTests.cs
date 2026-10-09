// <copyright file="HealthChecksTests.cs" company="Defra">
// Copyright (c) Defra. All rights reserved.
// </copyright>

namespace Defra.Lis.Api.Tests;

using Defra.Lis.Api.Interfaces;
using Defra.Lis.Api.Models;
using Defra.Lis.Api.Services.Health;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Moq;

public class HealthChecksTests
{
    private readonly Mock<IHealthService> mockHealthService = new();

    [Fact]
    public void DatabaseHealthCheck_Constructor_ThrowsArgumentNullException_WhenHealthServiceIsNull()
    {
        // Arrange
        IHealthService healthService = null!;

        // Act
        var act = () => new DatabaseHealthCheck(healthService);

        // Assert
        Assert.Throws<ArgumentNullException>(act);
    }

    [Theory]
    [InlineData("Healthy", HealthStatus.Healthy)]
    [InlineData("Degraded", HealthStatus.Degraded)]
    [InlineData("Unhealthy", HealthStatus.Unhealthy)]
    [InlineData("UnknownStatus", HealthStatus.Unhealthy)]
    public async Task DatabaseHealthCheck_CheckHealthAsync_MapsStatusCorrectly(
        string serviceStatus,
        HealthStatus expectedStatus)
    {
        // Arrange
        var componentResult = new ComponentHealthResult(
            serviceStatus,
            "Database test description",
            TimeSpan.FromMilliseconds(50),
            new Dictionary<string, object> { ["key"] = "val" });

        mockHealthService.Setup(s => s.CheckDatabaseHealthAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(componentResult);

        var check = new DatabaseHealthCheck(mockHealthService.Object);
        var context = new HealthCheckContext();

        // Act
        var result = await check.CheckHealthAsync(context, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(expectedStatus, result.Status);
        Assert.Equal("Database test description", result.Description);
        Assert.NotNull(result.Data);
        Assert.Equal("val", result.Data["key"]);
    }

    [Fact]
    public void QueueHealthCheck_Constructor_ThrowsArgumentNullException_WhenHealthServiceIsNull()
    {
        // Arrange
        IHealthService healthService = null!;

        // Act
        var act = () => new QueueHealthCheck(healthService);

        // Assert
        Assert.Throws<ArgumentNullException>(act);
    }

    [Theory]
    [InlineData("Healthy", HealthStatus.Healthy)]
    [InlineData("Degraded", HealthStatus.Degraded)]
    [InlineData("Unhealthy", HealthStatus.Unhealthy)]
    [InlineData("CustomStatus", HealthStatus.Unhealthy)]
    public async Task QueueHealthCheck_CheckHealthAsync_MapsStatusCorrectly(
        string serviceStatus,
        HealthStatus expectedStatus)
    {
        // Arrange
        var componentResult = new ComponentHealthResult(
            serviceStatus,
            "Queue test description",
            TimeSpan.FromMilliseconds(25),
            new Dictionary<string, object> { ["queueUrl"] = "http://localhost:4566/0000/test" });

        mockHealthService.Setup(s => s.CheckQueueHealthAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(componentResult);

        var check = new QueueHealthCheck(mockHealthService.Object);
        var context = new HealthCheckContext();

        // Act
        var result = await check.CheckHealthAsync(context, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(expectedStatus, result.Status);
        Assert.Equal("Queue test description", result.Description);
        Assert.NotNull(result.Data);
        Assert.Equal("http://localhost:4566/0000/test", result.Data["queueUrl"]);
    }

    [Fact]
    public void QuartzHealthCheck_Constructor_ThrowsArgumentNullException_WhenHealthServiceIsNull()
    {
        // Arrange
        IHealthService healthService = null!;

        // Act
        var act = () => new QuartzHealthCheck(healthService);

        // Assert
        Assert.Throws<ArgumentNullException>(act);
    }

    [Theory]
    [InlineData("Healthy", HealthStatus.Healthy)]
    [InlineData("Degraded", HealthStatus.Degraded)]
    [InlineData("Unhealthy", HealthStatus.Unhealthy)]
    [InlineData("Failed", HealthStatus.Unhealthy)]
    public async Task QuartzHealthCheck_CheckHealthAsync_MapsStatusCorrectly(
        string serviceStatus,
        HealthStatus expectedStatus)
    {
        // Arrange
        var componentResult = new ComponentHealthResult(
            serviceStatus,
            "Quartz test description",
            TimeSpan.FromMilliseconds(10),
            new Dictionary<string, object> { ["jobExists"] = true });

        mockHealthService.Setup(s => s.CheckQuartzHealthAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(componentResult);

        var check = new QuartzHealthCheck(mockHealthService.Object);
        var context = new HealthCheckContext();

        // Act
        var result = await check.CheckHealthAsync(context, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(expectedStatus, result.Status);
        Assert.Equal("Quartz test description", result.Description);
        Assert.NotNull(result.Data);
    }

    [Fact]
    public void ComponentHealthResult_ConstructorsAndProperties_InitializeCorrectly()
    {
        // Arrange
        var data = new Dictionary<string, object> { ["testKey"] = "testValue" };

        // Act
        var resultWithTimeSpan = new ComponentHealthResult("Healthy", "All good", TimeSpan.FromSeconds(1.5), data);
        var resultWithDouble = new ComponentHealthResult("Degraded", "Slow", 2.5, data);
        var defaultResult = new ComponentHealthResult("Unhealthy");

        // Assert
        Assert.Equal("Healthy", resultWithTimeSpan.Status);
        Assert.Equal("All good", resultWithTimeSpan.Description);
        Assert.Equal(1.5, resultWithTimeSpan.DurationSeconds);
        Assert.Equal(data, resultWithTimeSpan.Data);

        Assert.Equal("Degraded", resultWithDouble.Status);
        Assert.Equal("Slow", resultWithDouble.Description);
        Assert.Equal(2.5, resultWithDouble.DurationSeconds);
        Assert.Equal(data, resultWithDouble.Data);

        Assert.Equal("Unhealthy", defaultResult.Status);
        Assert.Null(defaultResult.Description);
        Assert.Equal(0, defaultResult.DurationSeconds);
        Assert.Null(defaultResult.Data);
    }

    [Fact]
    public void HealthCheckResponse_ConstructorsAndProperties_InitializeCorrectly()
    {
        // Arrange
        var entries = new Dictionary<string, ComponentHealthResult>
        {
            ["database"] = new("Healthy", "DB OK", 0.05),
            ["queue"] = new("Healthy", "Queue OK", 0.02),
        };

        // Act
        var responseWithTimeSpan = new HealthCheckResponse("Healthy", TimeSpan.FromSeconds(0.07), entries);
        var responseWithDouble = new HealthCheckResponse("Unhealthy", 1.25, entries);

        // Assert
        Assert.Equal("Healthy", responseWithTimeSpan.Status);
        Assert.Equal(0.07, responseWithTimeSpan.TotalDurationSeconds);
        Assert.Equal(2, responseWithTimeSpan.Entries.Count);

        Assert.Equal("Unhealthy", responseWithDouble.Status);
        Assert.Equal(1.25, responseWithDouble.TotalDurationSeconds);
        Assert.Equal(entries, responseWithDouble.Entries);
    }
}
