// <copyright file="RegistrationBundleQueueProcessor.logger.cs" company="Defra">
// Copyright (c) Defra. All rights reserved.
// </copyright>

namespace Defra.Lis.Api.Messaging;

public partial class RegistrationBundleQueueProcessor
{
    [LoggerMessage(LogLevel.Warning, "AWS:RegistrationBundleQueueUrl is not set, so no registration bundles are read")]
    partial void LogRegistrationBundleQueueUrlIsNotConfigured();

    [LoggerMessage(LogLevel.Error, "Failed to receive registration bundles from SQS")]
    partial void LogFailedToReceiveRegistrationBundles(Exception exception);

    [LoggerMessage(LogLevel.Information, "Processed registration bundle {SubmissionId}: {Outcome}")]
    partial void LogRegistrationBundleProcessed(Guid submissionId, RegistrationBundleOutcome outcome);

    [LoggerMessage(LogLevel.Error, "SQS message {MessageId} is not a readable registration bundle (receive count {ReceiveCount}). It is left to go to the dead-letter queue")]
    partial void LogUnreadableRegistrationBundleMessage(string messageId, int receiveCount);

    [LoggerMessage(LogLevel.Warning, "Registration bundle {SubmissionId} (SQS message {MessageId}) failed on receive {ReceiveCount} and will be retried")]
    partial void LogRegistrationBundleWillBeRetried(Guid? submissionId, string messageId, int receiveCount, Exception exception);

    [LoggerMessage(LogLevel.Error, "Registration bundle {SubmissionId} (SQS message {MessageId}) failed on receive {ReceiveCount}, its last attempt, and goes to the dead-letter queue")]
    partial void LogRegistrationBundleGoingToDeadLetterQueue(Guid? submissionId, string messageId, int receiveCount, Exception exception);
}
