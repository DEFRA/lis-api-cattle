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
}
