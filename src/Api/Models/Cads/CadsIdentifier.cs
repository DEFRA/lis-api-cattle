// <copyright file="CadsIdentifier.cs" company="Defra">
// Copyright (c) Defra. All rights reserved.
// </copyright>

namespace Defra.Lis.Api.Models.Cads;

/// <summary>
/// Represents identifier information for an animal returned by the CADS API.
/// </summary>
/// <param name="Schema">The schema describing the identifier.</param>
/// <param name="Identifier">The value of the identifier.</param>
public sealed record CadsIdentifier(string? Schema, string? Identifier);
