using System.Text.Json;
using Amazon.SQS;
using Amazon.SQS.Model;
using Lis.Cattle.Configurations;
using Lis.Cattle.Validation;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Lis.Cattle.Messaging;

public class SubmissionValidationQueueProcessor : ISubmissionValidationQueueProcessor
{
    private readonly IAmazonSQS _sqsClient;
    private readonly ISubmissionValidationService _validationService;
    private readonly AwsMessagingOptions _options;
    private readonly ILogger<SubmissionValidationQueueProcessor>? _logger;

    public SubmissionValidationQueueProcessor(
        IAmazonSQS sqsClient,
        ISubmissionValidationService validationService,
        IOptions<AwsMessagingOptions>? options = null,
        ILogger<SubmissionValidationQueueProcessor>? logger = null)
    {
        _sqsClient = sqsClient ?? throw new ArgumentNullException(nameof(sqsClient));
        _validationService = validationService ?? throw new ArgumentNullException(nameof(validationService));
        _options = options?.Value ?? new AwsMessagingOptions();
        _logger = logger;
    }

    public async Task<int> ProcessMessagesAsync(CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_options.SubmissionValidationQueueUrl))
        {
            _logger?.LogDebug("Submission validation queue URL is not configured. Skipping queue processing.");
            return 0;
        }

        var receiveRequest = new ReceiveMessageRequest
        {
            QueueUrl = _options.SubmissionValidationQueueUrl,
            MaxNumberOfMessages = _options.MaxNumberOfMessages > 0 ? _options.MaxNumberOfMessages : 10,
            WaitTimeSeconds = _options.WaitTimeSeconds >= 0 ? _options.WaitTimeSeconds : 5
        };

        ReceiveMessageResponse receiveResponse;
        try
        {
            receiveResponse = await _sqsClient.ReceiveMessageAsync(receiveRequest, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to receive messages from SQS queue {QueueUrl}", _options.SubmissionValidationQueueUrl);
            return 0;
        }

        if (receiveResponse.Messages == null || receiveResponse.Messages.Count == 0)
        {
            return 0;
        }

        _logger?.LogInformation("Received {Count} validation messages from SQS queue", receiveResponse.Messages.Count);

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
                    _logger?.LogInformation("Processing validation for submission {SubmissionId} from SQS", validationMessage.SubmissionId);
                    await _validationService.ValidateSubmissionByIdAsync(validationMessage.SubmissionId, cancellationToken);
                }
                else
                {
                    _logger?.LogWarning("SQS message {MessageId} did not contain valid submission data", message.MessageId);
                }

                await _sqsClient.DeleteMessageAsync(new DeleteMessageRequest
                {
                    QueueUrl = _options.SubmissionValidationQueueUrl,
                    ReceiptHandle = message.ReceiptHandle
                }, cancellationToken);

                processedCount++;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error processing SQS validation message {MessageId}", message.MessageId);
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
                        PropertyNameCaseInsensitive = true
                    });
                }
            }

            return JsonSerializer.Deserialize<SubmissionValidationMessage>(body, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });
        }
        catch
        {
            return null;
        }
    }
}
