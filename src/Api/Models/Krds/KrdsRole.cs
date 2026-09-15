// <copyright file="KrdsRole.cs" company="Defra">
// Copyright (c) Defra. All rights reserved.
// </copyright>

namespace Defra.Lis.Api.Models.Krds;

public sealed record KrdsRole(string? Code, IReadOnlyList<string>? Species);
