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

    [Range(1, 1000)]
    public int PageSize { get; init; } = 100;
}
