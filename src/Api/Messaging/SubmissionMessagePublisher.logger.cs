// <copyright file="SubmissionMessagePublisher.logger.cs" company="Defra">
// Copyright (c) Defra. All rights reserved.
// </copyright>

namespace Defra.Lis.Api.Messaging;

public partial class SubmissionMessagePublisher
{
    [LoggerMessage(LogLevel.Information, "Enqueued submission {SubmissionId} for validation to SQS queue {QueueUrl}. MessageId: {MessageId}")]
    partial void LogEnqueuedSubmissionSubmissionidForValidationToSqsQueueQueueurlMessageidMessageid(Guid submissionId, string queueUrl, string messageId);

    [LoggerMessage(LogLevel.Error, "Failed to send validation message to SQS for submission {SubmissionId}")]
    partial void LogFailedToSendValidationMessageToSqsForSubmissionSubmissionid(Guid submissionId, Exception exception);

    [LoggerMessage(LogLevel.Information, "Published submission {SubmissionId} validation event to SNS topic {TopicArn}. MessageId: {MessageId}")]
    partial void LogPublishedSubmissionSubmissionidValidationEventToSnsTopicTopicarnMessageidMessageid(Guid submissionId, string topicArn, string messageId);

    [LoggerMessage(LogLevel.Warning, "Failed to publish validation event to SNS topic for submission {SubmissionId}")]
    partial void LogFailedToPublishValidationEventToSnsTopicForSubmissionSubmissionid(Guid submissionId, Exception exception);
}
