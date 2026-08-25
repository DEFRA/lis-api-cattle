using System.Text.Json;
using Amazon.SimpleNotificationService;
using Amazon.SimpleNotificationService.Model;
using Amazon.SQS;
using Amazon.SQS.Model;
using Lis.Cattle.Configurations;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Lis.Cattle.Messaging;

public class SubmissionMessagePublisher : ISubmissionMessagePublisher
{
    private readonly IAmazonSQS _sqsClient;
    private readonly IAmazonSimpleNotificationService? _snsClient;
    private readonly AwsMessagingOptions _options;
    private readonly ILogger<SubmissionMessagePublisher>? _logger;

    public SubmissionMessagePublisher(
        IAmazonSQS sqsClient,
        IAmazonSimpleNotificationService? snsClient = null,
        IOptions<AwsMessagingOptions>? options = null,
        ILogger<SubmissionMessagePublisher>? logger = null)
    {
        _sqsClient = sqsClient ?? throw new ArgumentNullException(nameof(sqsClient));
        _snsClient = snsClient;
        _options = options?.Value ?? new AwsMessagingOptions();
        _logger = logger;
    }

    public async Task PublishSubmissionForValidationAsync(SubmissionValidationMessage message, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(message);

        var jsonBody = JsonSerializer.Serialize(message);

        // 1. Send message to SQS validation queue
        if (!string.IsNullOrWhiteSpace(_options.SubmissionValidationQueueUrl))
        {
            try
            {
                var sendMessageRequest = new SendMessageRequest
                {
                    QueueUrl = _options.SubmissionValidationQueueUrl,
                    MessageBody = jsonBody
                };

                var response = await _sqsClient.SendMessageAsync(sendMessageRequest, cancellationToken);
                _logger?.LogInformation(
                    "Enqueued submission {SubmissionId} for validation to SQS queue {QueueUrl}. MessageId: {MessageId}",
                    message.SubmissionId,
                    _options.SubmissionValidationQueueUrl,
                    response.MessageId);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Failed to send validation message to SQS for submission {SubmissionId}", message.SubmissionId);
                throw;
            }
        }

        // 2. Publish to SNS topic if configured
        if (_snsClient != null && !string.IsNullOrWhiteSpace(_options.SubmissionValidationTopicArn))
        {
            try
            {
                var publishRequest = new PublishRequest
                {
                    TopicArn = _options.SubmissionValidationTopicArn,
                    Message = jsonBody,
                    Subject = $"SubmissionValidation:{message.SubmissionId}"
                };

                var response = await _snsClient.PublishAsync(publishRequest, cancellationToken);
                _logger?.LogInformation(
                    "Published submission {SubmissionId} validation event to SNS topic {TopicArn}. MessageId: {MessageId}",
                    message.SubmissionId,
                    _options.SubmissionValidationTopicArn,
                    response.MessageId);
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "Failed to publish validation event to SNS topic for submission {SubmissionId}", message.SubmissionId);
            }
        }
    }
}
