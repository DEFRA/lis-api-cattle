// <copyright file="BundleAnimalResponse.cs" company="Defra">
// Copyright (c) Defra. All rights reserved.
// </copyright>

namespace Defra.Lis.Api.Models.Responses;

using System.ComponentModel;

/// <summary>
/// Represents the response model for an individual animal within a cattle registration bundle.
/// </summary>
public class BundleAnimalResponse
{
    /// <summary>
    /// Gets or sets the unique identifier of the animal.
    /// </summary>
    [Description("The unique identifier of the animal.")]
    public Guid Id { get; set; }

    /// <summary>
    /// Gets or sets the unique identifier of the submission.
    /// </summary>
    [Description("The unique identifier of the submission.")]
    public Guid SubmissionId { get; set; }

    /// <summary>
    /// Gets or sets the status of the animal.
    /// </summary>
    [Description("The status of the animal.")]
    public string Status { get; set; } = string.Empty;

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
    /// Gets or sets the dam type.
    /// </summary>
    [Description("The dam type.")]
    public string? DamType { get; set; }

    /// <summary>
    /// Gets or sets the genetic dam ear tag.
    /// </summary>
    [Description("The genetic dam ear tag.")]
    public string? DamGeneticEarTag { get; set; }

    /// <summary>
    /// Gets or sets the surrogate dam ear tag.
    /// </summary>
    [Description("The surrogate dam ear tag.")]
    public string? DamSurrogateEarTag { get; set; }

    /// <summary>
    /// Gets or sets the sire ear tag.
    /// </summary>
    [Description("The sire ear tag.")]
    public string? SireEarTag { get; set; }

    /// <summary>
    /// Gets or sets the sire name.
    /// </summary>
    [Description("The sire name.")]
    public string? SireName { get; set; }

    /// <summary>
    /// Gets or sets the list of errors for the animal.
    /// </summary>
    [Description("The list of errors for the animal.")]
    public List<BundleAnimalErrorResponse> Errors { get; set; } = [];
}
