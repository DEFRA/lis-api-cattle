// <copyright file="UserDetailsResponse.cs" company="Defra">
// Copyright (c) Defra. All rights reserved.
// </copyright>

namespace Defra.Lis.Api.Models.Responses;

using System.ComponentModel;

/// <summary>
/// Represents a user's details and the CPHs they are associated with.
/// </summary>
public class UserDetailsResponse
{
    /// <summary>
    /// Gets or sets the identity provider subject claim.
    /// </summary>
    [Description("The identity provider subject claim.")]
    public string Subject { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the email address of the user.
    /// </summary>
    [Description("The email address of the user.")]
    public string Email { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the first name of the user.
    /// </summary>
    [Description("The first name of the user.")]
    public string? FirstName { get; set; }

    /// <summary>
    /// Gets or sets the last name of the user.
    /// </summary>
    [Description("The last name of the user.")]
    public string? LastName { get; set; }

    /// <summary>
    /// Gets or sets the display name of the user.
    /// </summary>
    [Description("The display name of the user.")]
    public string? DisplayName { get; set; }

    /// <summary>
    /// Gets or sets the CPHs the user is associated with.
    /// </summary>
    [Description("The CPHs the user was associated with when they last signed in.")]
    public IReadOnlyList<UserCphResponse> Cphs { get; set; } = [];
}
