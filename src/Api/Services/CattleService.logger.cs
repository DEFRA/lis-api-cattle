// <copyright file="CattleService.logger.cs" company="Defra">
// Copyright (c) Defra. All rights reserved.
// </copyright>

namespace Defra.Lis.Api.Services;

public partial class CattleService
{
    [LoggerMessage(LogLevel.Warning, "Failed to publish validation message for submission {SubmissionId}")]
    partial void LogFailedToPublishValidationMessageForSubmissionSubmissionid(Guid submissionId, Exception exception);
}
