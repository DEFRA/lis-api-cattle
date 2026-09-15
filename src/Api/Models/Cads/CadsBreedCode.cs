// <copyright file="CadsBreedCode.cs" company="Defra">
// Copyright (c) Defra. All rights reserved.
// </copyright>

namespace Defra.Lis.Api.Models.Cads;

public sealed record CadsBreedCode(string? Schema, string? BreedName, string? Identifier);
