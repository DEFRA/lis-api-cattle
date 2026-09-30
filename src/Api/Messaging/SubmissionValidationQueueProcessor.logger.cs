// <copyright file="SubmissionValidationQueueProcessor.logger.cs" company="Defra">
// Copyright (c) Defra. All rights reserved.
// </copyright>

namespace Defra.Lis.Api.Messaging;

public partial class SubmissionValidationQueueProcessor
{
    [LoggerMessage(LogLevel.Debug, "Submission validation queue URL is not configured. Skipping queue processing.")]
    partial void LogSubmissionValidationQueueUrlIsNotConfiguredSkippingQueueProcessing();

    [LoggerMessage(LogLevel.Error, "Failed to receive messages from SQS queue {QueueUrl}")]
    partial void LogFailedToReceiveMessagesFromSqsQueueQueueurl(string queueUrl, Exception exception);

    [LoggerMessage(LogLevel.Information, "Received {Count} validation messages from SQS queue")]
    partial void LogReceivedCountValidationMessagesFromSqsQueue(int count);

    [LoggerMessage(LogLevel.Information, "Processing validation for submission {SubmissionId} from SQS")]
    partial void LogProcessingValidationForSubmissionSubmissionidFromSqs(Guid submissionId);

    [LoggerMessage(LogLevel.Warning, "SQS message {MessageId} did not contain valid submission data")]
    partial void LogSqsMessageMessageidDidNotContainValidSubmissionData(string messageId);

    [LoggerMessage(LogLevel.Error, "Error processing SQS validation message {MessageId}")]
    partial void LogErrorProcessingSqsValidationMessageMessageid(string messageId, Exception exception);
}
