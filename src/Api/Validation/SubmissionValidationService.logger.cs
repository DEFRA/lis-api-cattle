// <copyright file="SubmissionValidationService.logger.cs" company="Defra">
// Copyright (c) Defra. All rights reserved.
// </copyright>

namespace Defra.Lis.Api.Validation;

public partial class SubmissionValidationService
{
    [LoggerMessage(LogLevel.Warning, "Submission with ID {SubmissionId} not found for validation")]
    partial void LogSubmissionWithIdSubmissionidNotFoundForValidation(Guid submissionId);

    [LoggerMessage(LogLevel.Information, "Starting validation for submission {SubmissionId} (CPH: {Cph})")]
    partial void LogStartingValidationForSubmissionSubmissionidCphCph(Guid submissionId, string cph);

    [LoggerMessage(LogLevel.Information, "Validation completed for submission {SubmissionId}. Status: {Status}, Errors: {ErrorCount}")]
    partial void LogValidationCompletedForSubmissionSubmissionidStatusStatusErrorsErrorcount(Guid submissionId, string status, int errorCount);

    [LoggerMessage(LogLevel.Warning, "Could not fetch CADS cattle for holding {Cph}")]
    partial void LogCouldNotFetchCadsCattleForHoldingCph(string cph, Exception exception);
}
