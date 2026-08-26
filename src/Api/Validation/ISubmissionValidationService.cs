// <copyright file="ISubmissionValidationService.cs" company="Defra">
// Copyright (c) Defra. All rights reserved.
// </copyright>

namespace Defra.Lis.Api.Validation;

using Defra.Lis.Entities;

public interface ISubmissionValidationService
{
    Task<SubmissionValidationResult> ValidateSubmissionAsync(Submission submission, CancellationToken cancellationToken = default);

    Task<SubmissionValidationResult> ValidateSubmissionByIdAsync(Guid submissionId, CancellationToken cancellationToken = default);
}
