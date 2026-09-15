// <copyright file="KrdsAssociation.cs" company="Defra">
// Copyright (c) Defra. All rights reserved.
// </copyright>

namespace Defra.Lis.Api.Models.Krds;

public sealed record KrdsAssociation(string? Name, IReadOnlyList<KrdsRole>? Roles);
