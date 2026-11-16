// <copyright file="RegistrationAcceptedResponse.cs" company="Defra">
// Copyright (c) Defra. All rights reserved.
// </copyright>

namespace Defra.Lis.Api.Models.Responses;

using System.ComponentModel;

/// <summary>
/// The acknowledgement returned once a registration bundle has been queued for processing.
/// </summary>
public class RegistrationAcceptedResponse
{
    /// <summary>The status of a bundle that has been queued but not yet stored or sent to CTS.</summary>
    public const string AcceptedStatus = "accepted";

    /// <summary>
    /// Gets or sets the bundle (submission) ID to use for later lookups.
    /// </summary>
    [Description("The bundle (submission) ID to use for later lookups.")]
    public Guid Id { get; set; }

    /// <summary>
    /// Gets or sets the client reference supplied with the bundle.
    /// </summary>
    [Description("The client reference supplied with the bundle.")]
    public string ClientReference { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the bundle status, which is always 'accepted' in this response.
    /// </summary>
    [Description("The bundle status, which is always 'accepted' in this response.")]
    public string Status { get; set; } = AcceptedStatus;
}
