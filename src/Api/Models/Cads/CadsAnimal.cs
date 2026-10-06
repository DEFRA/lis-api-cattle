// <copyright file="CadsAnimal.cs" company="Defra">
// Copyright (c) Defra. All rights reserved.
// </copyright>

namespace Defra.Lis.Api.Models.Cads;

/// <summary>
/// A list item from the CADS bovine animals-on-holding endpoint.
/// </summary>
/// <param name="Identifier">The identifier of the animal.</param>
/// <param name="BirthDate">The birth date of the animal.</param>
/// <param name="DateOnCph">The date the animal arrived on the holding (CPH).</param>
/// <param name="DateOffCph">The date the animal left the holding (CPH).</param>
/// <param name="Species">The species of the animal.</param>
/// <param name="Sex">The sex of the animal.</param>
/// <param name="BreedCode">The breed code information of the animal.</param>
/// <param name="Status">The status of the animal.</param>
public sealed record CadsAnimal(
    CadsIdentifier? Identifier,
    DateOnly? BirthDate,
    DateOnly? DateOnCph,
    DateOnly? DateOffCph,
    string? Species,
    string? Sex,
    CadsBreedCode? BreedCode,
    string? Status);
