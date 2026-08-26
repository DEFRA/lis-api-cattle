// <copyright file="SubmissionAnimalValidationResult.cs" company="Defra">
// Copyright (c) Defra. All rights reserved.
// </copyright>

namespace Defra.Lis.Api.Validation;

public class SubmissionAnimalValidationResult
{
    public Guid AnimalId { get; set; }

    public string EarTag { get; set; } = string.Empty;

    public bool IsValid { get; set; }

    public List<ValidationErrorItem> Errors { get; set; } = [];
}
