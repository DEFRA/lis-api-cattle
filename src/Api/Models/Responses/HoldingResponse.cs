// <copyright file="HoldingResponse.cs" company="Defra">
// Copyright (c) Defra. All rights reserved.
// </copyright>

namespace Defra.Lis.Api.Models.Responses;

using System.ComponentModel;

/// <summary>
/// Represents the response model containing holding details.
/// </summary>
public class HoldingResponse
{
    /// <summary>
    /// Gets or sets the county, parish and holding (CPH) number.
    /// </summary>
    [Description("The county, parish and holding (CPH) number.")]
    public string Cph { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the name of the holding.
    /// </summary>
    [Description("The name of the holding.")]
    public string? Name { get; set; }

    /// <summary>
    /// Gets or sets the type of the holding.
    /// </summary>
    [Description("The type of the holding.")]
    public string? HoldingType { get; set; }

    /// <summary>
    /// Gets or sets the address of the holding.
    /// </summary>
    [Description("The address of the holding.")]
    public IReadOnlyList<string> Address { get; set; } = [];

    /// <summary>
    /// Gets or sets the name of the keeper.
    /// </summary>
    [Description("The name of the keeper.")]
    public string? KeeperName { get; set; }

    /// <summary>
    /// Gets or sets the list of herd marks for the holding.
    /// </summary>
    [Description("The list of herd marks for the holding.")]
    public IReadOnlyList<string> HerdMarks { get; set; } = [];

    /// <summary>
    /// Gets or sets the list of allowed species for the holding.
    /// </summary>
    [Description("The list of allowed species for the holding.")]
    public IReadOnlyList<string> AllowedSpecies { get; set; } = [];
}
