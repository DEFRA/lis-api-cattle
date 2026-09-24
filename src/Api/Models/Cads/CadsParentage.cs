// <copyright file="CadsParentage.cs" company="Defra">
// Copyright (c) Defra. All rights reserved.
// </copyright>

namespace Defra.Lis.Api.Models.Cads;

/// <summary>
/// One parent recorded against an animal in CADS. The relationship names the role the parent
/// plays; <see cref="GeneticDam"/>, <see cref="SurrogateDam"/> and <see cref="Sire"/> are the
/// values the bovine animal-details endpoint uses.
/// </summary>
public sealed record CadsParentage(string? Relationship, CadsIdentifier? AnimalIdentifier)
{
    public const string GeneticDam = "GeneticDam";

    public const string SurrogateDam = "SurrogateDam";

    public const string Sire = "Sire";

    public bool Is(string relationship) =>
        string.Equals(Relationship, relationship, StringComparison.OrdinalIgnoreCase);
}
