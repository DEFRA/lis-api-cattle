// <copyright file="RegistrationBundleRequest.cs" company="Defra">
// Copyright (c) Defra. All rights reserved.
// </copyright>

namespace Defra.Lis.Api.Models.Requests;

using System.ComponentModel;

/// <summary>
/// Represents the request payload for submitting a cattle registration bundle.
/// </summary>
public class RegistrationBundleRequest
{
    /// <summary>
    /// Gets or sets the client reference identifier for the registration bundle.
    /// </summary>
    [Description("The client reference identifier for the registration bundle.")]
    public string ClientReference { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the holding information where the animals are being registered.
    /// </summary>
    [Description("The holding information where the animals are being registered.")]
    public HoldingRequest? Holding { get; set; }

    /// <summary>
    /// Gets or sets the identifier of the user or system submitting the bundle.
    /// </summary>
    [Description("The identifier of the user or system submitting the bundle.")]
    public string? SubmittedBy { get; set; }

    /// <summary>
    /// Gets or sets the list of animal registrations included in the bundle.
    /// </summary>
    [Description("The list of animal registrations included in the bundle.")]
    public List<AnimalRegistrationRequest> Animals { get; set; } = [];
}
