// <copyright file="PaginatedResponse.cs" company="Defra">
// Copyright (c) Defra. All rights reserved.
// </copyright>

namespace Defra.Lis.Api.Models.Responses;

using System.ComponentModel;

/// <summary>
/// Represents a paginated response containing a subset of items and pagination metadata.
/// </summary>
/// <typeparam name="T">The type of item contained in the response.</typeparam>
public class PaginatedResponse<T>
{
    /// <summary>
    /// Gets the number of items on the current page.
    /// </summary>
    [Description("Number of items on the current page.")]
    public int Count { get; init; }

    /// <summary>
    /// Gets the total number of items across all pages.
    /// </summary>
    [Description("Total number of items across all pages.")]
    public int TotalCount { get; init; }

    /// <summary>
    /// Gets the items on the current page.
    /// </summary>
    [Description("The items on the current page.")]
    public IReadOnlyList<T> Items { get; init; } = [];

    /// <summary>
    /// Gets the current page number (1-based).
    /// </summary>
    [Description("The current page number (1-based).")]
    public int Page { get; init; }

    /// <summary>
    /// Gets the number of items per page.
    /// </summary>
    [Description("The number of items per page.")]
    public int PageSize { get; init; }

    /// <summary>
    /// Gets the total number of pages.
    /// </summary>
    [Description("The total number of pages.")]
    public int TotalPages { get; init; }

    /// <summary>
    /// Gets a value indicating whether there is a next page available.
    /// </summary>
    [Description("Whether there is a next page available.")]
    public bool HasNextPage { get; init; }

    /// <summary>
    /// Gets a value indicating whether there is a previous page available.
    /// </summary>
    [Description("Whether there is a previous page available.")]
    public bool HasPreviousPage { get; init; }
}
