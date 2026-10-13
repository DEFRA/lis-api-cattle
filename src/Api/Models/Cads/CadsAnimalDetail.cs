// <copyright file="CadsAnimalDetail.cs" company="Defra">
// Copyright (c) Defra. All rights reserved.
// </copyright>

namespace Defra.Lis.Api.Models.Cads;

/// <summary>
/// The animal carried by the CADS bovine animal-details endpoint. Richer than the list item
/// returned for a holding: it adds the registration date, locations, parentage and restriction status.
/// </summary>
/// <param name="ResourceType">The resource type of the animal detail.</param>
/// <param name="Identifier">The animal identifier.</param>
/// <param name="Species">The species of the animal.</param>
/// <param name="Sex">The sex of the animal.</param>
/// <param name="BirthDate">The birth date of the animal.</param>
/// <param name="RegistrationDate">The date the animal was registered.</param>
/// <param name="DateOnCph">The date the animal arrived on the current holding.</param>
/// <param name="CurrentLocation">The current location of the animal.</param>
/// <param name="RegisteredLocation">The registered location (birth/first holding) of the animal.</param>
/// <param name="DeathLocation">The death location of the animal, if deceased.</param>
/// <param name="BreedCode">The breed code information.</param>
/// <param name="Parentage">The parentage records (dam/sire).</param>
/// <param name="State">The alive/dead state of the animal.</param>
/// <param name="RestrictionStatus">The restriction status of the animal.</param>
public sealed record CadsAnimalDetail(
    string? ResourceType = null,
    CadsIdentifier? Identifier = null,
    string? Species = null,
    string? Sex = null,
    DateOnly? BirthDate = null,
    DateOnly? RegistrationDate = null,
    DateOnly? DateOnCph = null,
    CadsLocation? CurrentLocation = null,
    CadsLocation? RegisteredLocation = null,
    CadsLocation? DeathLocation = null,
    CadsBreedCode? BreedCode = null,
    IReadOnlyList<CadsParentage>? Parentage = null,
    string? State = null,
    string? RestrictionStatus = null);
