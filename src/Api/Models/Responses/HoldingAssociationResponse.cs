// <copyright file="HoldingAssociationResponse.cs" company="Defra">
// Copyright (c) Defra. All rights reserved.
// </copyright>

namespace Defra.Lis.Api.Models.Responses;

using System.ComponentModel;

/// <summary>
/// Represents an association on a holding.
/// </summary>
public class HoldingAssociationResponse
{
    /// <summary>
    /// Gets or sets the customer number of the associated party.
    /// </summary>
    [Description("The customer number of the associated party.")]
    public string? CustomerNumber { get; set; }

    /// <summary>
    /// Gets or sets the title of the associated person.
    /// </summary>
    [Description("The title of the associated person.")]
    public string? Title { get; set; }

    /// <summary>
    /// Gets or sets the first name of the associated person.
    /// </summary>
    [Description("The first name of the associated person.")]
    public string? FirstName { get; set; }

    /// <summary>
    /// Gets or sets the last name of the associated person.
    /// </summary>
    [Description("The last name of the associated person.")]
    public string? LastName { get; set; }

    /// <summary>
    /// Gets or sets the name of the associated person or organization.
    /// </summary>
    [Description("The name of the associated person or organization.")]
    public string? Name { get; set; }

    /// <summary>
    /// Gets or sets the party type.
    /// </summary>
    [Description("The party type.")]
    public string? PartyType { get; set; }

    /// <summary>
    /// Gets or sets the email address of the associated party.
    /// </summary>
    [Description("The email address of the associated party.")]
    public string? Email { get; set; }

    /// <summary>
    /// Gets or sets the mobile phone number of the associated party.
    /// </summary>
    [Description("The mobile phone number of the associated party.")]
    public string? Mobile { get; set; }

    /// <summary>
    /// Gets or sets the telephone number of the associated party.
    /// </summary>
    [Description("The telephone number of the associated party.")]
    public string? Telephone { get; set; }

    /// <summary>
    /// Gets or sets the address of the associated party.
    /// </summary>
    [Description("The address of the associated party.")]
    public IReadOnlyList<string> Address { get; set; } = [];

    /// <summary>
    /// Gets or sets the roles associated with the person or organization on the holding.
    /// </summary>
    [Description("The roles associated with the person or organization on the holding.")]
    public IReadOnlyList<HoldingRoleResponse> Roles { get; set; } = [];
}
