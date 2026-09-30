// <copyright file="SireRegistrationRequest.cs" company="Defra">
// Copyright (c) Defra. All rights reserved.
// </copyright>

namespace Defra.Lis.Api.Models.Requests;

using System.ComponentModel;

/// <summary>
/// Represents sire details for a cattle registration request.
/// </summary>
public class SireRegistrationRequest
{
    /// <summary>
    /// Gets or sets the ear tag of the sire.
    /// </summary>
    [Description("The ear tag of the sire.")]
    public string? EarTag { get; set; }

    /// <summary>
    /// Gets or sets the name of the sire.
    /// </summary>
    [Description("The name of the sire.")]
    public string? Name { get; set; }
}
