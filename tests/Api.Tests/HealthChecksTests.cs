// <copyright file="HealthChecksTests.cs" company="Defra">
// Copyright (c) Defra. All rights reserved.
// </copyright>

namespace Defra.Lis.Api.Tests;

using Defra.Lis.Api.Models;

public class HealthChecksTests
{
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
