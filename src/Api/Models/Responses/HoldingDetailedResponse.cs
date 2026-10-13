// <copyright file="HoldingDetailedResponse.cs" company="Defra">
// Copyright (c) Defra. All rights reserved.
// </copyright>

namespace Defra.Lis.Api.Models.Responses;

using System.ComponentModel;

/// <summary>
/// Represents the response model containing detailed holding information and associations.
/// </summary>
public class HoldingDetailedResponse : HoldingResponse
{
    /// <summary>
    /// Gets or sets the list of herd marks for the holding.
    /// </summary>
    [Description("The list of herd marks for the holding.")]
    public IReadOnlyList<HoldingMarkResponse> HerdMarks { get; set; } = [];

    /// <summary>
    /// Gets or sets the list of allowed species for the holding.
    /// </summary>
    [Description("The list of allowed species for the holding.")]
    public IReadOnlyList<string> AllowedSpecies { get; set; } = [];

    /// <summary>
    /// Gets or sets the list of associations for the holding.
    /// </summary>
    [Description("The list of associations for the holding.")]
    public IReadOnlyList<HoldingAssociationResponse> Associations { get; set; } = [];
}
