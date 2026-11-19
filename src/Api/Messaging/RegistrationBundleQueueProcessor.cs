// <copyright file="RegistrationBundleQueueProcessor.cs" company="Defra">
// Copyright (c) Defra. All rights reserved.
// </copyright>

namespace Defra.Lis.Api.Messaging;

using System.Globalization;
using System.Text.Json;
using Amazon.SQS;
using Amazon.SQS.Model;
using Defra.Lis.Api.Configurations;
using Defra.Lis.Api.Correlation;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Serilog.Context;

/// <summary>
/// Reads registration bundles from the queue and hands each to <see cref="IRegistrationBundleHandler"/>.
/// </summary>
/// <remarks>
/// SQS hides a received message from every other consumer for the visibility timeout, so only one instance works on
/// a message at a time. A message is deleted only after the handler has saved the bundle's CTS outcome. If the
/// handler throws, the message is left on the queue: SQS delivers it again after the visibility timeout, and moves it
/// to the dead-letter queue after the queue's maxReceiveCount.
/// </remarks>
public partial class RegistrationBundleQueueProcessor(
    IAmazonSQS sqsClient,
    IServiceScopeFactory scopeFactory,
    IOptions<AwsMessagingOptions> options,
    ILogger<RegistrationBundleQueueProcessor> logger)
    : IRegistrationBundleQueueProcessor
{
    private const string ApproximateReceiveCount = "ApproximateReceiveCount";

    private readonly IAmazonSQS sqsClient = sqsClient.ThrowIfNull();
    private readonly IServiceScopeFactory scopeFactory = scopeFactory.ThrowIfNull();
    private readonly AwsMessagingOptions options = options.Value;

    /// <inheritdoc />
    public async Task<int> ProcessMessagesAsync(CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(options.RegistrationBundleQueueUrl))
        {
            LogRegistrationBundleQueueUrlIsNotConfigured();
            return 0;
        }

        var receiveRequest = new ReceiveMessageRequest
        {
            QueueUrl = options.RegistrationBundleQueueUrl,
            MaxNumberOfMessages = options.MaxNumberOfMessages is > 0 and <= 10 ? options.MaxNumberOfMessages : 10,
            WaitTimeSeconds = options.WaitTimeSeconds is >= 0 and <= 20 ? options.WaitTimeSeconds : 5,
            VisibilityTimeout = Math.Max(1, options.RegistrationBundleVisibilityTimeoutSeconds),
            MessageSystemAttributeNames = [ApproximateReceiveCount],
            MessageAttributeNames = [RegistrationBundleMessage.CorrelationIdAttribute],
        };

        ReceiveMessageResponse response;
        try
        {
            response = await sqsClient.ReceiveMessageAsync(receiveRequest, cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            LogFailedToReceiveRegistrationBundles(ex);
            return 0;
        }

        if (response.Messages is not { Count: > 0 })
        {
            return 0;
        }

        // The messages in a batch are handled side by side, so every one finishes within its visibility timeout.
        var results = await Task.WhenAll(response.Messages.Select(m => ProcessMessageAsync(m, cancellationToken)));
        return results.Count(handled => handled);
    }

    private static RegistrationBundleMessage? Deserialize(string? body)
    {
        if (string.IsNullOrWhiteSpace(body))
        {
            return null;
        }

        try
        {
            var message = JsonSerializer.Deserialize<RegistrationBundleMessage>(body, RegistrationBundlePublisher.SerializerOptions);
            return message is { SubmissionId: var id, Bundle: not null } && id != Guid.Empty ? message : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static string? CorrelationIdOf(Message message, RegistrationBundleMessage? bundle) =>
        message.MessageAttributes != null
        && message.MessageAttributes.TryGetValue(RegistrationBundleMessage.CorrelationIdAttribute, out var attribute)
        && !string.IsNullOrWhiteSpace(attribute.StringValue)
            ? attribute.StringValue
            : bundle?.CorrelationId;

    private static int ReceiveCountOf(Message message) =>
        message.Attributes != null
        && message.Attributes.TryGetValue(ApproximateReceiveCount, out var value)
        && int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var count)
            ? count
            : 1;

    private async Task<bool> ProcessMessageAsync(Message message, CancellationToken cancellationToken)
    {
        var bundle = Deserialize(message.Body);
        var correlationId = CorrelationIdOf(message, bundle);
        var receiveCount = ReceiveCountOf(message);

        // Restore the correlation ID of the request that submitted the bundle, for the logs and the CTS call.
        CorrelationIdContext.Value = correlationId;
        using var correlationProperty = LogContext.PushProperty("CorrelationId", correlationId);

        try
        {
            if (bundle == null)
            {
                // A message that cannot be read never succeeds, so it is left to go to the dead-letter queue.
                LogUnreadableRegistrationBundleMessage(message.MessageId, receiveCount);
                return false;
            }

            using var scope = scopeFactory.CreateScope();
            var handler = scope.ServiceProvider.GetRequiredService<IRegistrationBundleHandler>();
            var outcome = await handler.HandleAsync(bundle, cancellationToken);

            await sqsClient.DeleteMessageAsync(
                new DeleteMessageRequest { QueueUrl = options.RegistrationBundleQueueUrl, ReceiptHandle = message.ReceiptHandle },
                cancellationToken);
            LogRegistrationBundleProcessed(bundle.SubmissionId, outcome);
            return true;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Shutting down: the message becomes visible again after the visibility timeout.
            return false;
        }
        catch (Exception ex)
        {
            if (receiveCount >= options.RegistrationBundleMaxReceiveCount)
            {
                LogRegistrationBundleGoingToDeadLetterQueue(bundle?.SubmissionId, message.MessageId, receiveCount, ex);
            }
            else
            {
                LogRegistrationBundleWillBeRetried(bundle?.SubmissionId, message.MessageId, receiveCount, ex);
            }

            return false;
        }
        finally
        {
            CorrelationIdContext.Value = null;
        }
    }
}
