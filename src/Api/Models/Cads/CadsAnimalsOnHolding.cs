// <copyright file="CadsAnimalsOnHolding.cs" company="Defra">
// Copyright (c) Defra. All rights reserved.
// </copyright>

namespace Defra.Lis.Api.Models.Cads;

/// <summary>
/// Represents a page of animals currently on a holding, as returned by the CADS bovine animals-on-holding endpoint.
/// </summary>
/// <param name="Animals">The animals on the holding for the current page, or <see langword="null"/> when the response contains no animal collection.</param>
/// <param name="TotalRecords">The total number of animals on the holding across all pages.</param>
/// <param name="PageSize">The page size applied by CADS, which may be smaller than the size requested.</param>
public sealed record CadsAnimalsOnHolding(
    IReadOnlyList<CadsAnimal>? Animals,
    long TotalRecords,
    int PageSize);
