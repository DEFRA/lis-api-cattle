// <copyright file="HoldingSearchRequest.cs" company="Defra">
// Copyright (c) Defra. All rights reserved.
// </copyright>

namespace Defra.Lis.Api.Models.Requests;

/// <summary>
/// Query parameters for searching holding details.
/// </summary>
/// <param name="Search">The optional search term to filter holdings by.</param>
/// <param name="Page">The optional 1-based page number.</param>
/// <param name="PageSize">The optional page size.</param>
/// <param name="Sort">The optional sort direction (asc or desc).</param>
/// <param name="Order">The optional field to sort by.</param>
public sealed record HoldingSearchRequest(
    string? Search = null,
    int? Page = null,
    int? PageSize = null,
    string? Sort = null,
    string? Order = null);
