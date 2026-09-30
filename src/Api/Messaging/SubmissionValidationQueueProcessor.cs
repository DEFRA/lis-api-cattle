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

public partial class SubmissionValidationQueueProcessor(
    IAmazonSQS sqsClient,
    ISubmissionValidationService validationService,
    IOptions<AwsMessagingOptions> options,
    ILogger<SubmissionValidationQueueProcessor> logger)
    : ISubmissionValidationQueueProcessor
{
    private readonly IAmazonSQS sqsClient = sqsClient.ThrowIfNull();
    private readonly ISubmissionValidationService validationService = validationService.ThrowIfNull();
    private readonly AwsMessagingOptions options = options.Value;

    public async Task<int> ProcessMessagesAsync(CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(options.SubmissionValidationQueueUrl))
        {
            LogSubmissionValidationQueueUrlIsNotConfiguredSkippingQueueProcessing();
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
            LogFailedToReceiveMessagesFromSqsQueueQueueurl(options.SubmissionValidationQueueUrl, ex);
            return 0;
        }

        if (receiveResponse.Messages == null || receiveResponse.Messages.Count == 0)
        {
            return 0;
        }

        LogReceivedCountValidationMessagesFromSqsQueue(receiveResponse.Messages.Count);

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
                    LogProcessingValidationForSubmissionSubmissionidFromSqs(validationMessage.SubmissionId);
                    await validationService.ValidateSubmissionByIdAsync(validationMessage.SubmissionId, cancellationToken);
                }
                else
                {
                    LogSqsMessageMessageidDidNotContainValidSubmissionData(message.MessageId);
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
                LogErrorProcessingSqsValidationMessageMessageid(message.MessageId, ex);
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
            var jsonSerializerOptions = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
            };

            if (doc.RootElement.ValueKind == JsonValueKind.Object &&
                doc.RootElement.TryGetProperty("Message", out var snsMessageProperty))
            {
                var nestedMessage = snsMessageProperty.GetString();
                if (!string.IsNullOrWhiteSpace(nestedMessage))
                {
                    return JsonSerializer.Deserialize<SubmissionValidationMessage>(nestedMessage, jsonSerializerOptions);
                }
            }

            return JsonSerializer.Deserialize<SubmissionValidationMessage>(body, jsonSerializerOptions);
        }
        catch
        {
            return null;
        }
    }
}
