// <copyright file="KrdsAddress.cs" company="Defra">
// Copyright (c) Defra. All rights reserved.
// </copyright>

namespace Defra.Lis.Api.Models.Krds;

/// <summary>
/// Address details returned by the keeper-data-api V2 holdings endpoint.
/// </summary>
/// <param name="AddressLine1">The first line of the address.</param>
/// <param name="AddressLine2">The second line of the address.</param>
/// <param name="PostTown">The post-town.</param>
/// <param name="Locality">The locality.</param>
/// <param name="Postcode">The postcode.</param>
/// <param name="Country">The country.</param>
public sealed record KrdsAddress(
    string? AddressLine1,
    string? AddressLine2,
    string? PostTown,
    string? Locality,
    string? Postcode,
    string? Country);
