// <copyright file="DamRegistrationRequest.cs" company="Defra">
// Copyright (c) Defra. All rights reserved.
// </copyright>

namespace Defra.Lis.Api.Models.Requests;

using System.ComponentModel;

/// <summary>
/// Represents dam details for a cattle registration request.
/// </summary>
public class DamRegistrationRequest
{
    /// <summary>
    /// Gets or sets the dam type (e.g., genetic or surrogate).
    /// </summary>
    [Description("The dam type (e.g., genetic or surrogate).")]
    public string? Type { get; set; }

    /// <summary>
    /// Gets or sets the ear tag of the genetic dam.
    /// </summary>
    [Description("The ear tag of the genetic dam.")]
    public string? GeneticDamEarTag { get; set; }

    /// <summary>
    /// Gets or sets the ear tag of the surrogate dam.
    /// </summary>
    [Description("The ear tag of the surrogate dam.")]
    public string? SurrogateDamEarTag { get; set; }
}
