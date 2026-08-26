// <copyright file="SubmissionValidationResult.cs" company="Defra">
// Copyright (c) Defra. All rights reserved.
// </copyright>

namespace Defra.Lis.Api.Validation;

public class SubmissionValidationResult
{
    public Guid SubmissionId { get; set; }

    public bool IsValid { get; set; }

    public string Status { get; set; } = string.Empty;

    public int ErrorCount { get; set; }

    public List<SubmissionAnimalValidationResult> AnimalResults { get; set; } = [];
}
