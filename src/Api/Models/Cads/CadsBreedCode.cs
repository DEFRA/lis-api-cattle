// <copyright file="CadsBreedCode.cs" company="Defra">
// Copyright (c) Defra. All rights reserved.
// </copyright>

namespace Defra.Lis.Api.Models.Cads;

/// <summary>
/// Represents breed code information for an animal returned by the CADS API.
/// </summary>
/// <param name="Schema">The schema describing the breed code.</param>
/// <param name="BreedName">The name of the breed.</param>
/// <param name="Identifier">The identifier or abbreviation code for the breed.</param>
public sealed record CadsBreedCode(string? Schema, string? BreedName, string? Identifier);
