// <copyright file="CtsAnimalStatusResponse.cs" company="Defra">
// Copyright (c) Defra. All rights reserved.
// </copyright>

namespace Defra.Lis.Api.Models.Responses;

using System.ComponentModel;
using Defra.Lis.Entities;

/// <summary>
/// Represents the CTS status response for an animal.
/// </summary>
public class CtsAnimalStatusResponse
{
    /// <summary>
    /// Gets or sets the ear tag of the animal.
    /// </summary>
    [Description("The ear tag of the animal.")]
    public string EarTag { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the status of the animal.
    /// </summary>
    [Description("The status of the animal.")]
    public string Status { get; set; } = string.Empty;

    /// <summary>
    /// Gets a value indicating whether the status is clean.
    /// </summary>
    [Description("Indicates whether the status is clean.")]
    public bool IsClean => string.Equals(Status, "clean", StringComparison.OrdinalIgnoreCase) ||
                           string.Equals(Status, Statuses.Complete, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Gets a value indicating whether the status is error.
    /// </summary>
    [Description("Indicates whether the status is error.")]
    public bool IsError => string.Equals(Status, Statuses.Error, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Gets or sets the list of errors for the animal.
    /// </summary>
    [Description("The list of errors for the animal.")]
    public List<CtsErrorResponse> Errors { get; set; } = [];
}
