// <copyright file="CadsAnimalDetailResponse.cs" company="Defra">
// Copyright (c) Defra. All rights reserved.
// </copyright>

namespace Defra.Lis.Api.Models.Cads;

/// <summary>
/// The envelope returned by the CADS bovine animal-details endpoint, which wraps the animal in
/// provenance metadata describing the system the record originated from.
/// </summary>
public sealed record CadsAnimalDetailResponse(
    string? ResourceType,
    string? Identifier,
    DateTimeOffset? EventDateTime,
    CadsSource? Source,
    CadsAnimalDetail? AnimalDetail);
