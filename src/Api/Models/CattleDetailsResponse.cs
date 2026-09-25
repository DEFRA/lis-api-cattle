// <copyright file="CattleDetailsResponse.cs" company="Defra">
// Copyright (c) Defra. All rights reserved.
// </copyright>

namespace Defra.Lis.Api.Models;

/// <summary>
/// Everything known about a single animal, as returned by the cattle details endpoint. The
/// parentage CADS holds as a list is flattened here into the dam and sire fields the consuming
/// services present.
/// </summary>
public class CattleDetailsResponse
{
    public string EarTag { get; set; } = string.Empty;

    public string? Species { get; set; }

    public string? Sex { get; set; }

    public DateOnly? DateBirth { get; set; }

    public DateOnly? DateRegistered { get; set; }

    public DateOnly? DateOnCph { get; set; }

    public string? Breed { get; set; }

    public string? BreedCode { get; set; }

    public string? BreedName { get; set; }

    /// <summary>
    /// Gets or sets the animal's state, "Alive" or "Dead".
    /// </summary>
    public string? State { get; set; }

    /// <summary>
    /// Gets or sets the animal's restriction status, for example "None" or "Restricted".
    /// </summary>
    public string? RestrictionStatus { get; set; }

    /// <summary>
    /// Gets or sets the kind of dam recorded, "surrogate" when a surrogate dam is present,
    /// otherwise "genetic" when a genetic dam is, otherwise null.
    /// </summary>
    public string? DamType { get; set; }

    public string? GeneticDamEarTag { get; set; }

    public string? SurrogateDamEarTag { get; set; }

    public string? SireEarTag { get; set; }

    /// <summary>
    /// Gets or sets the sire's name. CADS does not currently carry one, so this is always null.
    /// </summary>
    public string? SireName { get; set; }
}
