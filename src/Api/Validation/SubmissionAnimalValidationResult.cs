// <copyright file="SubmissionAnimalValidationResult.cs" company="Defra">
// Copyright (c) Defra. All rights reserved.
// </copyright>

namespace Defra.Lis.Api.Validation;

using System.ComponentModel;

/// <summary>
/// Represents the validation result for an animal in a submission.
/// </summary>
public class SubmissionAnimalValidationResult
{
    /// <summary>
    /// Gets or sets the unique identifier of the animal.
    /// </summary>
    [Description("The unique identifier of the animal.")]
    public Guid AnimalId { get; set; }

    /// <summary>
    /// Gets or sets the ear tag of the animal.
    /// </summary>
    [Description("The ear tag of the animal.")]
    public string EarTag { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets a value indicating whether the animal is valid.
    /// </summary>
    [Description("Indicates whether the animal is valid.")]
    public bool IsValid { get; set; }

    /// <summary>
    /// Gets or sets the validation errors for the animal.
    /// </summary>
    [Description("The validation errors for the animal.")]
    public List<ValidationErrorItem> Errors { get; set; } = [];
}
