// <copyright file="KrdsHolding.cs" company="Defra">
// Copyright (c) Defra. All rights reserved.
// </copyright>

namespace Defra.Lis.Api.Models.Krds;

/// <summary>
/// The holding detail returned by the keeper-data-api V2 holdings endpoint.
/// Only the fields this API needs are modelled; keeper contact details are deliberately not read.
/// </summary>
public sealed record KrdsHolding(
    string? Identifier,
    string? HoldingType,
    string? Name,
    KrdsLocation? Location,
    IReadOnlyList<KrdsAssociation>? Associations,
    IReadOnlyList<string>? AllowedSpecies,
    IReadOnlyList<KrdsMark>? Marks);
