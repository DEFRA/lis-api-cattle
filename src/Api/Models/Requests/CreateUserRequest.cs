// <copyright file="CreateUserRequest.cs" company="Defra">
// Copyright (c) Defra. All rights reserved.
// </copyright>

namespace Defra.Lis.Api.Models.Requests;

using System.ComponentModel;
using System.Text.Json.Serialization;

/// <summary>
/// The identity provider claims used to create (or refresh) a user account in the keeper data service.
/// Property names follow the OIDC claim names, as the keeper data service expects.
/// </summary>
public class CreateUserRequest
{
    /// <summary>
    /// Gets or sets the identity provider subject claim.
    /// </summary>
    [JsonPropertyName("sub")]
    [Description("The identity provider subject claim.")]
    public string? Sub { get; set; }

    /// <summary>
    /// Gets or sets the email address claim.
    /// </summary>
    [JsonPropertyName("email")]
    [Description("The email address claim.")]
    public string? Email { get; set; }

    /// <summary>
    /// Gets or sets the given name claim.
    /// </summary>
    [JsonPropertyName("given_name")]
    [Description("The given name claim.")]
    public string? GivenName { get; set; }

    /// <summary>
    /// Gets or sets the family name claim.
    /// </summary>
    [JsonPropertyName("family_name")]
    [Description("The family name claim.")]
    public string? FamilyName { get; set; }
}
