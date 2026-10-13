// <copyright file="HoldingResponse.cs" company="Defra">
// Copyright (c) Defra. All rights reserved.
// </copyright>

namespace Defra.Lis.Api.Models.Responses;

using System.ComponentModel;

/// <summary>
/// Represents the holding details returned in holding search results.
/// </summary>
public class HoldingResponse
{
    /// <summary>
    /// Gets the county, parish and holding (CPH) number.
    /// </summary>
    [Description("The county, parish and holding (CPH) number.")]
    public string Cph { get; init; } = string.Empty;

    /// <summary>
    /// Gets the name of the holding.
    /// </summary>
    [Description("The name of the holding.")]
    public string? Name { get; init; }

    /// <summary>
    /// Gets the type of the holding.
    /// </summary>
    [Description("The type of the holding.")]
    public string? HoldingType { get; init; }

    /// <summary>
    /// Gets the address of the holding.
    /// </summary>
    [Description("The address of the holding.")]
    public IReadOnlyList<string> Address { get; init; } = [];

    /// <summary>
    /// Gets the name of the keeper.
    /// </summary>
    [Description("The name of the keeper.")]
    public string? KeeperName { get; init; }
}
