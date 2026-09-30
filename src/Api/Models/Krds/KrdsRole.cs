// <copyright file="KrdsRole.cs" company="Defra">
// Copyright (c) Defra. All rights reserved.
// </copyright>

namespace Defra.Lis.Api.Models.Krds;

/// <summary>
/// Role details returned by the keeper-data-api V2 holdings endpoint.
/// </summary>
/// <param name="Code">The role code.</param>
/// <param name="Species">The list of species associated with the role.</param>
public sealed record KrdsRole(string? Code, IReadOnlyList<string>? Species);
