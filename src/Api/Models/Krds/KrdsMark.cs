// <copyright file="KrdsMark.cs" company="Defra">
// Copyright (c) Defra. All rights reserved.
// </copyright>

namespace Defra.Lis.Api.Models.Krds;

public sealed record KrdsMark(string? Mark, DateTimeOffset? StartDate, DateTimeOffset? EndDate, IReadOnlyList<string>? Species);
