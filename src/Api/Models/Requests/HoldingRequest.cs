// <copyright file="HoldingRequest.cs" company="Defra">
// Copyright (c) Defra. All rights reserved.
// </copyright>

namespace Defra.Lis.Api.Models.Requests;

using System.ComponentModel;

/// <summary>
/// Represents the holding information for a cattle registration request.
/// </summary>
public class HoldingRequest
{
    /// <summary>
    /// Gets or sets the county, parish, and holding (CPH) number.
    /// </summary>
    [Description("The county, parish and holding (CPH) number.")]
    public string Cph { get; set; } = string.Empty;
}
