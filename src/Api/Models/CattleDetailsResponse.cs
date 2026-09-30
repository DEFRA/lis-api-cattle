// <copyright file="CattleDetailsResponse.cs" company="Defra">
// Copyright (c) Defra. All rights reserved.
// </copyright>

namespace Defra.Lis.Api.Models;

using System.ComponentModel;

/// <summary>
/// Everything known about a single animal, as returned by the cattle details endpoint. The
/// parentage CADS holds as a list is flattened here into the dam and sire fields the consuming
/// services present.
/// </summary>
public class CattleDetailsResponse
{
    /// <summary>
    /// Gets or sets the ear tag of the animal.
    /// </summary>
    [Description("The ear tag of the animal.")]
    public string EarTag { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the species of the animal.
    /// </summary>
    [Description("The species of the animal.")]
    public string? Species { get; set; }

    /// <summary>
    /// Gets or sets the sex of the animal.
    /// </summary>
    [Description("The sex of the animal.")]
    public string? Sex { get; set; }

    /// <summary>
    /// Gets or sets the date of birth of the animal.
    /// </summary>
    [Description("The date of birth of the animal.")]
    public DateOnly? DateBirth { get; set; }

    /// <summary>
    /// Gets or sets the registration date of the animal.
    /// </summary>
    [Description("The registration date of the animal.")]
    public DateOnly? DateRegistered { get; set; }

    /// <summary>
    /// Gets or sets the date the animal arrived on the holding.
    /// </summary>
    [Description("The date the animal arrived on the holding.")]
    public DateOnly? DateOnCph { get; set; }

    /// <summary>
    /// Gets or sets the breed of the animal.
    /// </summary>
    [Description("The breed of the animal.")]
    public string? Breed { get; set; }

    /// <summary>
    /// Gets or sets the breed code of the animal.
    /// </summary>
    [Description("The breed code of the animal.")]
    public string? BreedCode { get; set; }

    /// <summary>
    /// Gets or sets the breed name of the animal.
    /// </summary>
    [Description("The breed name of the animal.")]
    public string? BreedName { get; set; }

    /// <summary>
    /// Gets or sets the animal's state, "Alive" or "Dead".
    /// </summary>
    [Description("The animal's state, for example Alive or Dead.")]
    public string? State { get; set; }

    /// <summary>
    /// Gets or sets the animal's restriction status, for example "None" or "Restricted".
    /// </summary>
    [Description("The animal's restriction status, for example None or Restricted.")]
    public string? RestrictionStatus { get; set; }

    /// <summary>
    /// Gets or sets the kind of dam recorded, "surrogate" when a surrogate dam is present,
    /// otherwise "genetic" when a genetic dam is, otherwise null.
    /// </summary>
    [Description("The kind of dam recorded: surrogate or genetic.")]
    public string? DamType { get; set; }

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

    /// <summary>
    /// Gets or sets the ear tag of the sire.
    /// </summary>
    [Description("The ear tag of the sire.")]
    public string? SireEarTag { get; set; }

    /// <summary>
    /// Gets or sets the sire's name. CADS does not currently carry one, so this is always null.
    /// </summary>
    [Description("The name of the sire; currently always null because CADS does not carry it.")]
    public string? SireName { get; set; }
}
