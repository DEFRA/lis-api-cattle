// <copyright file="CattleLocationResponse.cs" company="Defra">
// Copyright (c) Defra. All rights reserved.
// </copyright>

namespace Defra.Lis.Api.Models.Responses;

using System.ComponentModel;

/// <summary>
/// Represents location information for an animal.
/// </summary>
public class CattleLocationResponse
{
    /// <summary>
    /// Gets or sets the location identifier (e.g. CPH).
    /// </summary>
    [Description("The location identifier.")]
    public string? Identifier { get; set; }

    /// <summary>
    /// Gets or sets the optional sub-identifier of the location.
    /// </summary>
    [Description("The sub-identifier of the location.")]
    public string? SubIdentifier { get; set; }

    /// <summary>
    /// Gets or sets the full identifier of the location.
    /// </summary>
    [Description("The full identifier of the location.")]
    public string? FullIdentifier { get; set; }

    /// <summary>
    /// Gets or sets the name or description of the location.
    /// </summary>
    [Description("The name of the location.")]
    public string? Name { get; set; }

    /// <summary>
    /// Gets or sets the premises type of the location.
    /// </summary>
    [Description("The premises type of the location.")]
    public string? PremisesType { get; set; }
}
