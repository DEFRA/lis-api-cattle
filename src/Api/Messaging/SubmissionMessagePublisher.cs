// <copyright file="SubmissionMessagePublisher.cs" company="Defra">
// Copyright (c) Defra. All rights reserved.
// </copyright>

namespace Defra.Lis.Api.Messaging;

using System.Text.Json;
using Amazon.SimpleNotificationService;
using Amazon.SimpleNotificationService.Model;
using Amazon.SQS;
using Amazon.SQS.Model;
using Defra.Lis.Api.Configurations;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

public class SubmissionMessagePublisher(
    IAmazonSQS sqsClient,
    IAmazonSimpleNotificationService? snsClient = null,
    IOptions<AwsMessagingOptions>? options = null,
    ILogger<SubmissionMessagePublisher>? logger = null)
    : ISubmissionMessagePublisher
{
    private readonly IAmazonSQS sqsClient = sqsClient ?? throw new ArgumentNullException(nameof(sqsClient));
    private readonly AwsMessagingOptions options = options?.Value ?? new AwsMessagingOptions();

    public async Task PublishSubmissionForValidationAsync(SubmissionValidationMessage message, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(message);

        var jsonBody = JsonSerializer.Serialize(message);

        // 1. Send message to SQS validation queue
        if (!string.IsNullOrWhiteSpace(options.SubmissionValidationQueueUrl))
        {
            try
            {
                var sendMessageRequest = new SendMessageRequest
                {
                    QueueUrl = options.SubmissionValidationQueueUrl,
                    MessageBody = jsonBody,
                };

                var response = await sqsClient.SendMessageAsync(sendMessageRequest, cancellationToken);
                logger?.LogInformation(
                    "Enqueued submission {SubmissionId} for validation to SQS queue {QueueUrl}. MessageId: {MessageId}",
                    message.SubmissionId,
                    options.SubmissionValidationQueueUrl,
                    response.MessageId);
            }
#pragma warning disable S2139
            catch (Exception ex)
#pragma warning restore S2139
            {
                logger?.LogError(ex, "Failed to send validation message to SQS for submission {SubmissionId}", message.SubmissionId);
                throw;
            }
        }

        // 2. Publish to SNS topic if configured
        if (snsClient != null && !string.IsNullOrWhiteSpace(options.SubmissionValidationTopicArn))
        {
            try
            {
                var publishRequest = new PublishRequest
                {
                    TopicArn = options.SubmissionValidationTopicArn,
                    Message = jsonBody,
                    Subject = $"SubmissionValidation:{message.SubmissionId}",
                };

                var response = await snsClient.PublishAsync(publishRequest, cancellationToken);
                logger?.LogInformation(
                    "Published submission {SubmissionId} validation event to SNS topic {TopicArn}. MessageId: {MessageId}",
                    message.SubmissionId,
                    options.SubmissionValidationTopicArn,
                    response.MessageId);
            }
            catch (Exception ex)
            {
                logger?.LogWarning(ex, "Failed to publish validation event to SNS topic for submission {SubmissionId}", message.SubmissionId);
            }
        }
    }
}
