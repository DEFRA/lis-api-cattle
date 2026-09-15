// <copyright file="CadsIdentifier.cs" company="Defra">
// Copyright (c) Defra. All rights reserved.
// </copyright>

namespace Defra.Lis.Api.Models.Cads;

public sealed record CadsIdentifier(string? Schema, string? Identifier);
