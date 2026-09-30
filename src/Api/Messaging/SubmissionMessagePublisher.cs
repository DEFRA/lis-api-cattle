// <copyright file="SubmissionMessagePublisher.cs" company="Defra">
// Copyright (c) Defra. All rights reserved.
// </copyright>

namespace Defra.Lis.Api.Messaging;

using System;
using System.Text.Json;
using Amazon.SimpleNotificationService;
using Amazon.SimpleNotificationService.Model;
using Amazon.SQS;
using Amazon.SQS.Model;
using Defra.Lis.Api.Configurations;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

public partial class SubmissionMessagePublisher(
    IAmazonSQS sqsClient,
    IAmazonSimpleNotificationService? snsClient,
    IOptions<AwsMessagingOptions> options,
    ILogger<SubmissionMessagePublisher> logger)
    : ISubmissionMessagePublisher
{
    private readonly IAmazonSQS sqsClient = sqsClient.ThrowIfNull();
    private readonly AwsMessagingOptions options = options.Value;

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
                LogEnqueuedSubmissionSubmissionidForValidationToSqsQueueQueueurlMessageidMessageid(message.SubmissionId, options.SubmissionValidationQueueUrl, response.MessageId);
            }
#pragma warning disable S2139
            catch (Exception ex)
#pragma warning restore S2139
            {
                LogFailedToSendValidationMessageToSqsForSubmissionSubmissionid(message.SubmissionId, ex);
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
                LogPublishedSubmissionSubmissionidValidationEventToSnsTopicTopicarnMessageidMessageid(message.SubmissionId, options.SubmissionValidationTopicArn, response.MessageId);
            }
            catch (Exception ex)
            {
                LogFailedToPublishValidationEventToSnsTopicForSubmissionSubmissionid(message.SubmissionId, ex);
            }
        }
    }
}
