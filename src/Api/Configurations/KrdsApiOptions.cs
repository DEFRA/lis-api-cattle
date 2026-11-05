// <copyright file="KrdsApiOptions.cs" company="Defra">
// Copyright (c) Defra. All rights reserved.
// </copyright>

namespace Defra.Lis.Api.Configurations;

using System.ComponentModel.DataAnnotations;

/// <summary>
/// Connection settings for the keeper-data-api (KRDS) holdings API (faked by lis-fake-service until KRDS is available).
/// </summary>
public sealed class KrdsApiOptions
{
    public const string SectionName = "KrdsApi";

    [Required]
    public string BaseUrl { get; init; } = string.Empty;

    [Required]
    public string ClientId { get; init; } = string.Empty;

    [Required]
    public string ClientSecret { get; init; } = string.Empty;

    /// <summary>
    /// Gets the CDP developer API key, sent as x-api-key, for calling KRDS through the ephemeral-protected gateway
    /// from a developer machine. Leave empty in CDP, where services call each other directly.
    /// </summary>
    public string? GatewayApiKey { get; init; }
}
