// <copyright file="RegistrationBundlePublisher.cs" company="Defra">
// Copyright (c) Defra. All rights reserved.
// </copyright>

namespace Defra.Lis.Api.Messaging;

using System.Text.Json;
using Amazon.Runtime;
using Amazon.SQS;
using Amazon.SQS.Model;
using Defra.Lis.Api.Configurations;
using Defra.Livestock.Sdk.Core.Exceptions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

/// <summary>
/// Sends accepted registration bundles to the SQS queue in <c>AWS:RegistrationBundleQueueUrl</c>.
/// </summary>
public partial class RegistrationBundlePublisher(
    IAmazonSQS sqsClient,
    IOptions<AwsMessagingOptions> options,
    ILogger<RegistrationBundlePublisher> logger)
    : IRegistrationBundlePublisher
{
    /// <summary>The JSON options shared by the publisher and the queue worker.</summary>
    public static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    private readonly IAmazonSQS sqsClient = sqsClient.ThrowIfNull();
    private readonly AwsMessagingOptions options = options.Value;

    /// <inheritdoc />
    public async Task PublishAsync(RegistrationBundleMessage message, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(message);

        if (string.IsNullOrWhiteSpace(options.RegistrationBundleQueueUrl))
        {
            LogRegistrationBundleQueueIsNotConfigured(message.SubmissionId);
            throw new ServiceUnavailableException(
                "The registration bundle queue is not configured.",
                new InvalidOperationException("AWS:RegistrationBundleQueueUrl is not set."));
        }

        var request = new SendMessageRequest
        {
            QueueUrl = options.RegistrationBundleQueueUrl,
            MessageBody = JsonSerializer.Serialize(message, SerializerOptions),
        };

        if (!string.IsNullOrWhiteSpace(message.CorrelationId))
        {
            request.MessageAttributes = new Dictionary<string, MessageAttributeValue>
            {
                [RegistrationBundleMessage.CorrelationIdAttribute] =
                    new() { DataType = "String", StringValue = message.CorrelationId },
            };
        }

        try
        {
            var response = await sqsClient.SendMessageAsync(request, cancellationToken);
            LogEnqueuedRegistrationBundle(message.SubmissionId, response.MessageId);
        }
        catch (Exception ex) when (ex is AmazonServiceException or AmazonClientException or HttpRequestException
                                       or TimeoutException
                                   || (ex is OperationCanceledException && !cancellationToken.IsCancellationRequested))
        {
            // SQS being unreachable is retryable for the caller, so it surfaces as 503 rather than 500.
            LogFailedToEnqueueRegistrationBundle(message.SubmissionId, ex);
            throw new ServiceUnavailableException("The registration bundle could not be queued. Try again later.", ex);
        }
    }
}
