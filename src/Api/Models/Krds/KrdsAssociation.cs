// <copyright file="KrdsAssociation.cs" company="Defra">
// Copyright (c) Defra. All rights reserved.
// </copyright>

namespace Defra.Lis.Api.Models.Krds;

/// <summary>
/// Represents an association on a holding returned by the keeper-data-api V2 holdings endpoint.
/// </summary>
/// <param name="CustomerNumber">The customer number.</param>
/// <param name="Title">The title.</param>
/// <param name="FirstName">The first name.</param>
/// <param name="LastName">The last name.</param>
/// <param name="Name">The full name.</param>
/// <param name="PartyType">The party type.</param>
/// <param name="Email">The email address.</param>
/// <param name="Mobile">The mobile phone number.</param>
/// <param name="Telephone">The telephone number.</param>
/// <param name="Address">The party address.</param>
/// <param name="Roles">The collection of roles associated with this person or organization on the holding.</param>
public sealed record KrdsAssociation(
    string? CustomerNumber,
    string? Title,
    string? FirstName,
    string? LastName,
    string? Name,
    string? PartyType,
    string? Email,
    string? Mobile,
    string? Telephone,
    KrdsPartyAddress? Address,
    IReadOnlyList<KrdsRole>? Roles);
