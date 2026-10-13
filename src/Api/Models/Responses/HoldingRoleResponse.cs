// <copyright file="HoldingRoleResponse.cs" company="Defra">
// Copyright (c) Defra. All rights reserved.
// </copyright>

namespace Defra.Lis.Api.Models.Responses;

using System.ComponentModel;

/// <summary>
/// Represents a role associated with an entity on a holding.
/// </summary>
public class HoldingRoleResponse
{
    /// <summary>
    /// Gets or sets the role code.
    /// </summary>
    [Description("The role code.")]
    public string? Code { get; set; }

    /// <summary>
    /// Gets or sets the species associated with the role.
    /// </summary>
    [Description("The species associated with the role.")]
    public IReadOnlyList<string> Species { get; set; } = [];
}
