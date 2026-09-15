// <copyright file="CadsPaginatedResult.cs" company="Defra">
// Copyright (c) Defra. All rights reserved.
// </copyright>

namespace Defra.Lis.Api.Models.Cads;

/// <summary>
/// The paged envelope returned by the CADS bovine animal list endpoint.
/// </summary>
public sealed record CadsPaginatedResult<T>(
    IReadOnlyList<T>? Results,
    int Count,
    int TotalCount,
    int Page,
    int PageSize,
    int TotalPages,
    bool HasNextPage,
    bool HasPreviousPage);
