// <copyright file="CadsApiOptions.cs" company="Defra">
// Copyright (c) Defra. All rights reserved.
// </copyright>

namespace Defra.Lis.Api.Configurations;

using System.ComponentModel.DataAnnotations;

/// <summary>
/// Connection settings for the CADS bovine animal API (faked by lis-fake-service until CADS is available).
/// </summary>
public sealed class CadsApiOptions
{
    public const string SectionName = "CadsApi";

    [Required]
    public string BaseUrl { get; init; } = string.Empty;

    [Required]
    public string ClientId { get; init; } = string.Empty;

    [Required]
    public string ClientSecret { get; init; } = string.Empty;

    /// <summary>
    /// Gets the CDP developer API key, sent as x-api-key, for calling CADS through the ephemeral-protected gateway
    /// from a developer machine. Leave empty in CDP, where services call each other directly.
    /// </summary>
    public string? GatewayApiKey { get; init; }

    [Range(1, 1000)]
    public int PageSize { get; init; } = 100;

    /// <summary>
    /// Gets how long a call to CADS may wait for the response headers before it times out and the API answers 503
    /// (LREG-509). The cattle API gives up before the BE4FE, which gives it 4 s.
    /// </summary>
    [Range(1, 300)]
    public int TimeoutSeconds { get; init; } = 3;

    /// <summary>
    /// Gets how many calls must fail without a response, with none succeeding, within
    /// <see cref="CircuitBreakerSamplingSeconds"/> before the circuit opens.
    /// </summary>
    [Range(2, 1000)]
    public int CircuitBreakerFailureThreshold { get; init; } = 5;

    /// <summary>
    /// Gets the window, in seconds, over which failures are counted towards opening the circuit.
    /// </summary>
    [Range(1, 3600)]
    public int CircuitBreakerSamplingSeconds { get; init; } = 30;

    /// <summary>
    /// Gets how long, in seconds, calls fail fast once the circuit opens, before one probe call is let through.
    /// </summary>
    [Range(1, 3600)]
    public int CircuitBreakerBreakSeconds { get; init; } = 30;
}
