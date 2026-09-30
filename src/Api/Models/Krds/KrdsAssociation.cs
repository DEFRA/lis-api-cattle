// <copyright file="KrdsAssociation.cs" company="Defra">
// Copyright (c) Defra. All rights reserved.
// </copyright>

namespace Defra.Lis.Api.Models.Krds;

/// <summary>
/// Represents an association and its associated roles on a holding returned by the keeper-data-api V2 holdings endpoint.
/// </summary>
/// <param name="Name">The name of the associated person or organization.</param>
/// <param name="Roles">The collection of roles associated with this person or organization on the holding.</param>
public sealed record KrdsAssociation(string? Name, IReadOnlyList<KrdsRole>? Roles);
