// <copyright file="SubmissionValidationQueueProcessor.cs" company="Defra">
// Copyright (c) Defra. All rights reserved.
// </copyright>

namespace Defra.Lis.Api.Messaging;

using System.Text.Json;
using Amazon.SQS;
using Amazon.SQS.Model;
using Defra.Lis.Api.Configurations;
using Defra.Lis.Api.Validation;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

public class SubmissionValidationQueueProcessor(
    IAmazonSQS sqsClient,
    ISubmissionValidationService validationService,
    IOptions<AwsMessagingOptions>? options = null,
    ILogger<SubmissionValidationQueueProcessor>? logger = null)
    : ISubmissionValidationQueueProcessor
{
    private readonly IAmazonSQS sqsClient = sqsClient ?? throw new ArgumentNullException(nameof(sqsClient));
    private readonly ISubmissionValidationService validationService = validationService ?? throw new ArgumentNullException(nameof(validationService));
    private readonly AwsMessagingOptions options = options?.Value ?? new AwsMessagingOptions();

    public async Task<int> ProcessMessagesAsync(CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(options.SubmissionValidationQueueUrl))
        {
            logger?.LogDebug("Submission validation queue URL is not configured. Skipping queue processing.");
            return 0;
        }

        var receiveRequest = new ReceiveMessageRequest
        {
            QueueUrl = options.SubmissionValidationQueueUrl,
            MaxNumberOfMessages = options.MaxNumberOfMessages > 0 ? options.MaxNumberOfMessages : 10,
            WaitTimeSeconds = options.WaitTimeSeconds >= 0 ? options.WaitTimeSeconds : 5,
        };

        ReceiveMessageResponse receiveResponse;
        try
        {
            receiveResponse = await sqsClient.ReceiveMessageAsync(receiveRequest, cancellationToken);
        }
        catch (Exception ex)
        {
            logger?.LogError(ex, "Failed to receive messages from SQS queue {QueueUrl}", options.SubmissionValidationQueueUrl);
            return 0;
        }

        if (receiveResponse.Messages == null || receiveResponse.Messages.Count == 0)
        {
            return 0;
        }

        logger?.LogInformation("Received {Count} validation messages from SQS queue", receiveResponse.Messages.Count);

        var processedCount = 0;

        foreach (var message in receiveResponse.Messages)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                break;
            }

            try
            {
                var validationMessage = ExtractValidationMessage(message.Body);
                if (validationMessage != null && validationMessage.SubmissionId != Guid.Empty)
                {
                    logger?.LogInformation("Processing validation for submission {SubmissionId} from SQS", validationMessage.SubmissionId);
                    await validationService.ValidateSubmissionByIdAsync(validationMessage.SubmissionId, cancellationToken);
                }
                else
                {
                    logger?.LogWarning("SQS message {MessageId} did not contain valid submission data", message.MessageId);
                }

                await sqsClient.DeleteMessageAsync(
                    new DeleteMessageRequest
                    {
                        QueueUrl = options.SubmissionValidationQueueUrl,
                        ReceiptHandle = message.ReceiptHandle,
                    },
                    cancellationToken);

                processedCount++;
            }
            catch (Exception ex)
            {
                logger?.LogError(ex, "Error processing SQS validation message {MessageId}", message.MessageId);
            }
        }

        return processedCount;
    }

    private static SubmissionValidationMessage? ExtractValidationMessage(string body)
    {
        if (string.IsNullOrWhiteSpace(body))
        {
            return null;
        }

        try
        {
            // If message was delivered via SNS subscription to SQS, payload is inside "Message" property
            using var doc = JsonDocument.Parse(body);
            if (doc.RootElement.ValueKind == JsonValueKind.Object && doc.RootElement.TryGetProperty("Message", out var snsMessageProperty))
            {
                var nestedMessage = snsMessageProperty.GetString();
                if (!string.IsNullOrWhiteSpace(nestedMessage))
                {
                    return JsonSerializer.Deserialize<SubmissionValidationMessage>(nestedMessage, new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true,
                    });
                }
            }

            return JsonSerializer.Deserialize<SubmissionValidationMessage>(body, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
            });
        }
        catch
        {
            return null;
        }
    }
}
