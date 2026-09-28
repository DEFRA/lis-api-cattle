// <copyright file="KrdsMark.cs" company="Defra">
// Copyright (c) Defra. All rights reserved.
// </copyright>

namespace Defra.Lis.Api.Models.Krds;

/// <summary>
/// Mark details returned by the keeper-data-api V2 holdings endpoint.
/// </summary>
/// <param name="Mark">The mark identifier (e.g. herd or flock mark).</param>
/// <param name="StartDate">The start date from which the mark is valid.</param>
/// <param name="EndDate">The end date until which the mark is valid.</param>
/// <param name="Species">The list of species associated with the mark.</param>
public sealed record KrdsMark(string? Mark, DateTimeOffset? StartDate, DateTimeOffset? EndDate, IReadOnlyList<string>? Species);
