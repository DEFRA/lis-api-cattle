// <copyright file="ComponentHealthResult.cs" company="Defra">
// Copyright (c) Defra. All rights reserved.
// </copyright>

namespace Defra.Lis.Api.Models;

using System.ComponentModel;
using System.Text.Json.Serialization;

/// <summary>
/// Represents the health check result for an individual dependency component.
/// </summary>
public record ComponentHealthResult
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ComponentHealthResult"/> class.
    /// </summary>
    /// <param name="status">The health status of the component.</param>
    /// <param name="description">An optional description or failure detail.</param>
    /// <param name="durationSeconds">The execution duration in seconds.</param>
    /// <param name="data">Optional metadata associated with the health check.</param>
    [JsonConstructor]
    public ComponentHealthResult(
        string status,
        string? description = null,
        double durationSeconds = 0,
        IReadOnlyDictionary<string, object>? data = null)
    {
        this.Status = status;
        this.Description = description;
        this.DurationSeconds = durationSeconds;
        this.Data = data;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ComponentHealthResult"/> class.
    /// </summary>
    /// <param name="status">The health status of the component.</param>
    /// <param name="description">An optional description or failure detail.</param>
    /// <param name="duration">The execution duration as a TimeSpan.</param>
    /// <param name="data">Optional metadata associated with the health check.</param>
    public ComponentHealthResult(
        string status,
        string? description,
        TimeSpan duration,
        IReadOnlyDictionary<string, object>? data = null)
        : this(status, description, duration.TotalSeconds, data)
    {
    }

    /// <summary>
    /// Gets the component health status (e.g. Healthy, Degraded, Unhealthy).
    /// </summary>
    [Description("The component health status (e.g. Healthy, Degraded, Unhealthy).")]
    public string Status { get; init; }

    /// <summary>
    /// Gets the optional description or error message for this component.
    /// </summary>
    [Description("The description or details regarding the component check outcome.")]
    public string? Description { get; init; }

    /// <summary>
    /// Gets the duration of the component health check in seconds.
    /// </summary>
    [Description("The duration taken to check this component, in seconds.")]
    public double DurationSeconds { get; init; }

    /// <summary>
    /// Gets additional key-value metadata returned by the component health check.
    /// </summary>
    [Description("Additional metadata properties associated with the component health check.")]
    public IReadOnlyDictionary<string, object>? Data { get; init; }
}
