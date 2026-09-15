// <copyright file="CadsAnimal.cs" company="Defra">
// Copyright (c) Defra. All rights reserved.
// </copyright>

namespace Defra.Lis.Api.Models.Cads;

/// <summary>
/// A list item from the CADS bovine animals-on-holding endpoint.
/// </summary>
public sealed record CadsAnimal(
    CadsIdentifier? Identifier,
    DateOnly? BirthDate,
    DateOnly? DateOnCph,
    DateOnly? DateOffCph,
    string? Species,
    string? Sex,
    CadsBreedCode? BreedCode,
    string? Status)
{
    public const string AliveStatus = "Alive";

    public bool IsAlive => string.Equals(Status, AliveStatus, StringComparison.OrdinalIgnoreCase);
}
