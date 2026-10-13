// <copyright file="KrdsEnsureUserAccountRequest.cs" company="Defra">
// Copyright (c) Defra. All rights reserved.
// </copyright>

namespace Defra.Lis.Api.Models.Krds;

using System.Text.Json.Serialization;

/// <summary>
/// The body of the keeper-data-api V2 ensure user account request (<c>EnsureUserAccountRequest</c>), which uses the
/// OIDC claim names.
/// </summary>
public sealed record KrdsEnsureUserAccountRequest(
    [property: JsonPropertyName("sub")] string Sub,
    [property: JsonPropertyName("email")] string Email,
    [property: JsonPropertyName("given_name")] string GivenName,
    [property: JsonPropertyName("family_name")] string FamilyName);
