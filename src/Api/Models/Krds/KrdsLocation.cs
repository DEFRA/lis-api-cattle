// <copyright file="KrdsLocation.cs" company="Defra">
// Copyright (c) Defra. All rights reserved.
// </copyright>

namespace Defra.Lis.Api.Models.Krds;

/// <summary>
/// Location details returned by the keeper-data-api V2 holdings endpoint.
/// </summary>
/// <param name="Address">The address of the location.</param>
public sealed record KrdsLocation(KrdsAddress? Address);
