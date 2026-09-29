// <copyright file="UserCphResponse.cs" company="Defra">
// Copyright (c) Defra. All rights reserved.
// </copyright>

namespace Defra.Lis.Api.Models.Responses;

using System.ComponentModel;

/// <summary>
/// Represents a CPH a user is associated with, and the role they hold on it.
/// </summary>
public class UserCphResponse
{
    /// <summary>
    /// Gets or sets the county, parish and holding (CPH) number.
    /// </summary>
    [Description("The county, parish and holding (CPH) number.")]
    public string Cph { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the keeper data identifier of the holding the CPH belongs to.
    /// </summary>
    [Description("The keeper data identifier of the holding the CPH belongs to.")]
    public string? HoldingId { get; set; }

    /// <summary>
    /// Gets or sets the name of the holding.
    /// </summary>
    [Description("The name of the holding, where the source carries one.")]
    public string? HoldingName { get; set; }

    /// <summary>
    /// Gets or sets the role the user holds on the holding.
    /// </summary>
    [Description("The role the user holds on the holding.")]
    public string Role { get; set; } = string.Empty;
}
