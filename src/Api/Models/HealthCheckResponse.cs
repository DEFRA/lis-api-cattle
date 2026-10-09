// <copyright file="HealthCheckResponse.cs" company="Defra">
// Copyright (c) Defra. All rights reserved.
// </copyright>

namespace Defra.Lis.Api.Models;

using System.ComponentModel;
using System.Text.Json.Serialization;

/// <summary>
/// Represents the overall health check response payload.
/// </summary>
public record HealthCheckResponse
{
    /// <summary>
    /// Initializes a new instance of the <see cref="HealthCheckResponse"/> class.
    /// </summary>
    /// <param name="status">The overall health status string.</param>
    /// <param name="totalDurationSeconds">The total duration of the health check in seconds.</param>
    /// <param name="entries">The individual component health check results.</param>
    [JsonConstructor]
    public HealthCheckResponse(
        string status,
        double totalDurationSeconds,
        IReadOnlyDictionary<string, ComponentHealthResult> entries)
    {
        Status = status;
        TotalDurationSeconds = totalDurationSeconds;
        Entries = entries;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="HealthCheckResponse"/> class.
    /// </summary>
    /// <param name="status">The overall health status string.</param>
    /// <param name="totalDuration">The total duration of the health check as a TimeSpan.</param>
    /// <param name="entries">The individual component health check results.</param>
    public HealthCheckResponse(
        string status,
        TimeSpan totalDuration,
        IReadOnlyDictionary<string, ComponentHealthResult> entries)
        : this(status, totalDuration.TotalSeconds, entries)
    {
    }

    /// <summary>
    /// Gets the overall health status (e.g. Healthy, Degraded, Unhealthy).
    /// </summary>
    [Description("The overall health status of the application (e.g. Healthy, Degraded, Unhealthy).")]
    public string Status { get; init; }

    /// <summary>
    /// Gets the total duration of the health checks in seconds.
    /// </summary>
    [Description("The total duration taken to execute all health checks, in seconds.")]
    public double TotalDurationSeconds { get; init; }

    /// <summary>
    /// Gets the dictionary of individual component health check results.
    /// </summary>
    [Description("The dictionary of individual component health check results keyed by component name.")]
    public IReadOnlyDictionary<string, ComponentHealthResult> Entries { get; init; }
}
