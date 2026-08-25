using System.Text.Json;
using Amazon.SQS;
using Amazon.SQS.Model;
using Lis.Cattle.Configurations;
using Lis.Cattle.Messaging;
using Lis.Cattle.Validation;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace Lis.Cattle;

public class SubmissionValidationQueueProcessorTests
{
    private readonly Mock<IAmazonSQS> _mockSqs;
    private readonly Mock<ISubmissionValidationService> _mockValidationService;
    private readonly AwsMessagingOptions _options;

    public SubmissionValidationQueueProcessorTests()
    {
        _mockSqs = new Mock<IAmazonSQS>();
        _mockValidationService = new Mock<ISubmissionValidationService>();
        _options = new AwsMessagingOptions
        {
            SubmissionValidationQueueUrl = "http://localhost:4566/000000000000/submission-validation-queue"
        };
    }

    [Fact]
    public async Task ProcessMessagesAsync_WhenMessagesReceived_ValidatesAndDeletesFromQueue()
    {
        var submissionId = Guid.NewGuid();
        var message = new SubmissionValidationMessage
        {
            SubmissionId = submissionId,
            CountyParishHolding = "12/345/6789",
            ClientReference = "REF123"
        };

        var rawMessageBody = JsonSerializer.Serialize(message);

        _mockSqs.Setup(s => s.ReceiveMessageAsync(It.IsAny<ReceiveMessageRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ReceiveMessageResponse
            {
                Messages =
                [
                    new Message
                    {
                        MessageId = "msg-1",
                        ReceiptHandle = "handle-1",
                        Body = rawMessageBody
                    }
                ]
            });

        _mockValidationService.Setup(v => v.ValidateSubmissionByIdAsync(submissionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SubmissionValidationResult { SubmissionId = submissionId, IsValid = true });

        var processor = new SubmissionValidationQueueProcessor(
            _mockSqs.Object,
            _mockValidationService.Object,
            Options.Create(_options));

        var processedCount = await processor.ProcessMessagesAsync(TestContext.Current.CancellationToken);

        Assert.Equal(1, processedCount);

        _mockValidationService.Verify(v => v.ValidateSubmissionByIdAsync(submissionId, It.IsAny<CancellationToken>()), Times.Once);
        _mockSqs.Verify(s => s.DeleteMessageAsync(
            It.Is<DeleteMessageRequest>(d => d.ReceiptHandle == "handle-1" && d.QueueUrl == _options.SubmissionValidationQueueUrl),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ProcessMessagesAsync_WhenDeliveredViaSnsSubscription_ExtractsNestedMessageAndValidates()
    {
        var submissionId = Guid.NewGuid();
        var message = new SubmissionValidationMessage
        {
            SubmissionId = submissionId,
            CountyParishHolding = "12/345/6789",
            ClientReference = "REF123"
        };

        var nestedMessageJson = JsonSerializer.Serialize(message);
        var snsEnvelope = JsonSerializer.Serialize(new
        {
            Type = "Notification",
            MessageId = "sns-uuid",
            TopicArn = "arn:aws:sns:eu-west-2:000000000000:submission-validation-topic",
            Message = nestedMessageJson
        });

        _mockSqs.Setup(s => s.ReceiveMessageAsync(It.IsAny<ReceiveMessageRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ReceiveMessageResponse
            {
                Messages =
                [
                    new Message
                    {
                        MessageId = "msg-sns-1",
                        ReceiptHandle = "handle-sns-1",
                        Body = snsEnvelope
                    }
                ]
            });

        _mockValidationService.Setup(v => v.ValidateSubmissionByIdAsync(submissionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SubmissionValidationResult { SubmissionId = submissionId, IsValid = true });

        var processor = new SubmissionValidationQueueProcessor(
            _mockSqs.Object,
            _mockValidationService.Object,
            Options.Create(_options));

        var processedCount = await processor.ProcessMessagesAsync(TestContext.Current.CancellationToken);

        Assert.Equal(1, processedCount);
        _mockValidationService.Verify(v => v.ValidateSubmissionByIdAsync(submissionId, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ProcessMessagesAsync_WhenQueueEmpty_ReturnsZero()
    {
        _mockSqs.Setup(s => s.ReceiveMessageAsync(It.IsAny<ReceiveMessageRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ReceiveMessageResponse
            {
                Messages = []
            });

        var processor = new SubmissionValidationQueueProcessor(
            _mockSqs.Object,
            _mockValidationService.Object,
            Options.Create(_options));

        var processedCount = await processor.ProcessMessagesAsync(TestContext.Current.CancellationToken);

        Assert.Equal(0, processedCount);
        _mockValidationService.Verify(v => v.ValidateSubmissionByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
