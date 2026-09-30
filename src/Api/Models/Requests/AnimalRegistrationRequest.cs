// <copyright file="AnimalRegistrationRequest.cs" company="Defra">
// Copyright (c) Defra. All rights reserved.
// </copyright>

namespace Defra.Lis.Api.Models.Requests;

using System.ComponentModel;

/// <summary>
/// Represents the request payload for registering an individual animal within a cattle registration bundle.
/// </summary>
public class AnimalRegistrationRequest
{
    /// <summary>
    /// Gets or sets the unique ear tag identifier of the animal.
    /// </summary>
    [Description("The unique ear tag identifier of the animal.")]
    public string EarTag { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the date of birth of the animal.
    /// </summary>
    [Description("The date of birth of the animal.")]
    public DateOnly? DateOfBirth { get; set; }

    /// <summary>
    /// Gets or sets the sex of the animal.
    /// </summary>
    [Description("The sex of the animal.")]
    public string? Sex { get; set; }

    /// <summary>
    /// Gets or sets the breed code or name of the animal.
    /// </summary>
    [Description("The breed code or name of the animal.")]
    public string? Breed { get; set; }

    /// <summary>
    /// Gets or sets the dam details for the animal registration.
    /// </summary>
    [Description("The dam details for the animal registration.")]
    public DamRegistrationRequest? Dam { get; set; }

    /// <summary>
    /// Gets or sets the sire details for the animal registration.
    /// </summary>
    [Description("The sire details for the animal registration.")]
    public SireRegistrationRequest? Sire { get; set; }
}
