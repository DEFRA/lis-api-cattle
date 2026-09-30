// <copyright file="CadsPaginatedResult.cs" company="Defra">
// Copyright (c) Defra. All rights reserved.
// </copyright>

namespace Defra.Lis.Api.Models.Cads;

/// <summary>
/// Represents a page of results returned by a paginated CADS endpoint.
/// </summary>
/// <typeparam name="T">The type of item contained in the result page.</typeparam>
/// <param name="Results">The items returned for the current page, or <see langword="null"/> when the response contains no result collection.</param>
/// <param name="Count">The number of items returned on the current page.</param>
/// <param name="TotalCount">The total number of matching items across all pages.</param>
/// <param name="Page">The one-based number of the current page.</param>
/// <param name="PageSize">The maximum number of items requested per page.</param>
/// <param name="TotalPages">The total number of pages available.</param>
/// <param name="HasNextPage">A value indicating whether another page is available after the current page.</param>
/// <param name="HasPreviousPage">A value indicating whether a page is available before the current page.</param>
public sealed record CadsPaginatedResult<T>(
    IReadOnlyList<T>? Results,
    int Count,
    int TotalCount,
    int Page,
    int PageSize,
    int TotalPages,
    bool HasNextPage,
    bool HasPreviousPage);
