// <copyright file="CadsAnimalDetail.cs" company="Defra">
// Copyright (c) Defra. All rights reserved.
// </copyright>

namespace Defra.Lis.Api.Models.Cads;

/// <summary>
/// The animal carried by the CADS bovine animal-details endpoint. Richer than the list item
/// returned for a holding: it adds the registration date, parentage and restriction status.
/// </summary>
public sealed record CadsAnimalDetail(
    string? ResourceType,
    CadsIdentifier? Identifier,
    string? Species,
    string? Sex,
    DateOnly? BirthDate,
    DateOnly? RegistrationDate,
    DateOnly? DateOnCph,
    CadsBreedCode? BreedCode,
    IReadOnlyList<CadsParentage>? Parentage,
    string? State,
    string? RestrictionStatus);
