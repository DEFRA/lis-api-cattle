// <copyright file="RegistrationBundlePublisher.logger.cs" company="Defra">
// Copyright (c) Defra. All rights reserved.
// </copyright>

namespace Defra.Lis.Api.Messaging;

public partial class RegistrationBundlePublisher
{
    [LoggerMessage(LogLevel.Information, "Enqueued registration bundle {SubmissionId}. MessageId: {MessageId}")]
    partial void LogEnqueuedRegistrationBundle(Guid submissionId, string messageId);

    [LoggerMessage(LogLevel.Error, "Failed to enqueue registration bundle {SubmissionId}")]
    partial void LogFailedToEnqueueRegistrationBundle(Guid submissionId, Exception exception);

    [LoggerMessage(LogLevel.Error, "Registration bundle {SubmissionId} was not queued because AWS:RegistrationBundleQueueUrl is not set")]
    partial void LogRegistrationBundleQueueIsNotConfigured(Guid submissionId);
}
