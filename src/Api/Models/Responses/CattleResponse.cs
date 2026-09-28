// <copyright file="CattleResponse.cs" company="Defra">
// Copyright (c) Defra. All rights reserved.
// </copyright>

namespace Defra.Lis.Api.Models.Responses;

using System.ComponentModel;

/// <summary>
/// Represents the response model for cattle on a holding.
/// </summary>
public class CattleResponse
{
    /// <summary>
    /// Gets or sets the ear tag of the animal.
    /// </summary>
    [Description("The ear tag of the animal.")]
    public string EarTag { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the date of birth of the animal.
    /// </summary>
    [Description("The date of birth of the animal.")]
    public DateOnly? DateBirth { get; set; }

    /// <summary>
    /// Gets or sets the date the animal arrived on the holding.
    /// </summary>
    [Description("The date the animal arrived on the holding.")]
    public DateOnly? DateOnCph { get; set; }

    /// <summary>
    /// Gets or sets the sex of the animal.
    /// </summary>
    [Description("The sex of the animal.")]
    public string? Sex { get; set; }

    /// <summary>
    /// Gets or sets the breed of the animal.
    /// </summary>
    [Description("The breed of the animal.")]
    public string? Breed { get; set; }

    /// <summary>
    /// Gets or sets the breed code of the animal.
    /// </summary>
    [Description("The breed code of the animal.")]
    public string? BreedCode { get; set; }

    /// <summary>
    /// Gets or sets the breed name of the animal.
    /// </summary>
    [Description("The breed name of the animal.")]
    public string? BreedName { get; set; }

    /// <summary>
    /// Gets or sets the status of the animal.
    /// </summary>
    [Description("The status of the animal.")]
    public string Status { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the list of errors for the animal.
    /// </summary>
    [Description("The list of errors for the animal.")]
    public List<CattleErrorResponse> Errors { get; set; } = new();
}
