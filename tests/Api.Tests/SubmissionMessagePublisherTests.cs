using System.Text.Json;
using Amazon.SimpleNotificationService;
using Amazon.SimpleNotificationService.Model;
using Amazon.SQS;
using Amazon.SQS.Model;
using Lis.Cattle.Configurations;
using Lis.Cattle.Messaging;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace Lis.Cattle;

public class SubmissionMessagePublisherTests
{
    private readonly Mock<IAmazonSQS> _mockSqs;
    private readonly Mock<IAmazonSimpleNotificationService> _mockSns;
    private readonly AwsMessagingOptions _options;

    public SubmissionMessagePublisherTests()
    {
        _mockSqs = new Mock<IAmazonSQS>();
        _mockSns = new Mock<IAmazonSimpleNotificationService>();
        _options = new AwsMessagingOptions
        {
            SubmissionValidationQueueUrl = "http://localhost:4566/000000000000/submission-validation-queue",
            SubmissionValidationTopicArn = "arn:aws:sns:eu-west-2:000000000000:submission-validation-topic"
        };
    }

    [Fact]
    public async Task PublishSubmissionForValidationAsync_SendsMessageToSqsAndPublishesToSns()
    {
        _mockSqs.Setup(s => s.SendMessageAsync(It.IsAny<SendMessageRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SendMessageResponse { MessageId = "sqs-msg-123" });

        _mockSns.Setup(s => s.PublishAsync(It.IsAny<PublishRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PublishResponse { MessageId = "sns-msg-456" });

        var publisher = new SubmissionMessagePublisher(
            _mockSqs.Object,
            _mockSns.Object,
            Options.Create(_options));

        var message = new SubmissionValidationMessage
        {
            SubmissionId = Guid.NewGuid(),
            CountyParishHolding = "12/345/6789",
            ClientReference = "REF123",
            SubmittedBy = "USER1",
            AnimalCount = 3
        };

        await publisher.PublishSubmissionForValidationAsync(message, TestContext.Current.CancellationToken);

        _mockSqs.Verify(s => s.SendMessageAsync(
            It.Is<SendMessageRequest>(r =>
                r.QueueUrl == _options.SubmissionValidationQueueUrl &&
                r.MessageBody.Contains(message.SubmissionId.ToString()) &&
                r.MessageBody.Contains("12/345/6789")),
            It.IsAny<CancellationToken>()), Times.Once);

        _mockSns.Verify(s => s.PublishAsync(
            It.Is<PublishRequest>(r =>
                r.TopicArn == _options.SubmissionValidationTopicArn &&
                r.Message.Contains(message.SubmissionId.ToString())),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task PublishSubmissionForValidationAsync_WhenSnsClientIsNull_OnlySendsToSqs()
    {
        _mockSqs.Setup(s => s.SendMessageAsync(It.IsAny<SendMessageRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SendMessageResponse { MessageId = "sqs-msg-123" });

        var publisher = new SubmissionMessagePublisher(
            _mockSqs.Object,
            null,
            Options.Create(_options));

        var message = new SubmissionValidationMessage
        {
            SubmissionId = Guid.NewGuid(),
            CountyParishHolding = "12/345/6789",
            ClientReference = "REF123"
        };

        await publisher.PublishSubmissionForValidationAsync(message, TestContext.Current.CancellationToken);

        _mockSqs.Verify(s => s.SendMessageAsync(It.IsAny<SendMessageRequest>(), It.IsAny<CancellationToken>()), Times.Once);
    }
}
