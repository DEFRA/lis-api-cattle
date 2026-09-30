// <copyright file="SubmissionValidationResult.cs" company="Defra">
// Copyright (c) Defra. All rights reserved.
// </copyright>

namespace Defra.Lis.Api.Validation;

using System.ComponentModel;

/// <summary>
/// Represents the validation result for a submission and its animals.
/// </summary>
public class SubmissionValidationResult
{
    /// <summary>
    /// Gets or sets the unique identifier of the submission.
    /// </summary>
    [Description("The unique identifier of the submission.")]
    public Guid SubmissionId { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the submission is valid.
    /// </summary>
    [Description("Indicates whether the submission is valid.")]
    public bool IsValid { get; set; }

    /// <summary>
    /// Gets or sets the validation status of the submission.
    /// </summary>
    [Description("The validation status of the submission.")]
    public string Status { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the total number of validation errors in the submission.
    /// </summary>
    [Description("The total number of validation errors in the submission.")]
    public int ErrorCount { get; set; }

    /// <summary>
    /// Gets or sets the validation results for the animals in the submission.
    /// </summary>
    [Description("The validation results for the animals in the submission.")]
    public List<SubmissionAnimalValidationResult> AnimalResults { get; set; } = [];
}
