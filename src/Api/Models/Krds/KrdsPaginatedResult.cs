// <copyright file="KrdsPaginatedResult.cs" company="Defra">
// Copyright (c) Defra. All rights reserved.
// </copyright>

namespace Defra.Lis.Api.Models.Krds;

/// <summary>
/// A paginated result returned by the keeper-data-api V2 holdings endpoint.
/// </summary>
/// <typeparam name="T">The type of item contained in the result page.</typeparam>
/// <param name="Values">The items returned for the current page.</param>
/// <param name="Count">The number of items returned on the current page.</param>
/// <param name="TotalCount">The total number of matching items across all pages.</param>
/// <param name="Page">The one-based number of the current page.</param>
/// <param name="PageSize">The maximum number of items requested per page.</param>
/// <param name="TotalPages">The total number of pages available.</param>
/// <param name="HasNextPage">A value indicating whether another page is available after the current page.</param>
/// <param name="HasPreviousPage">A value indicating whether a page is available before the current page.</param>
/// <param name="NextCursor">An optional cursor for next page navigation.</param>
public sealed record KrdsPaginatedResult<T>(
    IReadOnlyList<T>? Values,
    int Count,
    int TotalCount,
    int Page,
    int PageSize,
    int TotalPages,
    bool HasNextPage,
    bool HasPreviousPage,
    string? NextCursor = null);
