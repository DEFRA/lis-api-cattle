// <copyright file="CadsSource.cs" company="Defra">
// Copyright (c) Defra. All rights reserved.
// </copyright>

namespace Defra.Lis.Api.Models.Cads;

/// <summary>
/// Provenance of a CADS record: the system it originated from and the schema it was sent under.
/// </summary>
public sealed record CadsSource(string? System, string? Schema, string? SchemaVersion);
