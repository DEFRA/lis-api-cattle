// <copyright file="KrdsCphAssociation.cs" company="Defra">
// Copyright (c) Defra. All rights reserved.
// </copyright>

namespace Defra.Lis.Api.Models.Krds;

/// <summary>
/// An association between a user account and a CPH (<c>CphAssociationDto</c> in the keeper-data-api V2 contract).
/// </summary>
public sealed record KrdsCphAssociation(
    string? CphNumber,
    string? Role,
    string? HoldingId,
    string? HoldingName);
