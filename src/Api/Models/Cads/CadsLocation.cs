// <copyright file="CadsLocation.cs" company="Defra">
// Copyright (c) Defra. All rights reserved.
// </copyright>

namespace Defra.Lis.Api.Models.Cads;

/// <summary>
/// Location information associated with an animal returned by the CADS API.
/// </summary>
/// <param name="Identifier">The identifier of the location (e.g. CPH).</param>
/// <param name="SubIdentifier">The optional sub-identifier of the location.</param>
/// <param name="FullIdentifier">The full identifier of the location (e.g. AH-08/065/0077).</param>
/// <param name="Name">The name of the location or address.</param>
/// <param name="PremisesType">The premises type of the location (e.g. AH).</param>
public sealed record CadsLocation(
    string? Identifier = null,
    string? SubIdentifier = null,
    string? FullIdentifier = null,
    string? Name = null,
    string? PremisesType = null);
