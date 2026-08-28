// <copyright file="SubmissionMessagePublisherTests.cs" company="Defra">
// Copyright (c) Defra. All rights reserved.
// </copyright>

namespace Defra.Lis.Api.Tests;

using Amazon.SimpleNotificationService;
using Amazon.SimpleNotificationService.Model;
using Amazon.SQS;
using Amazon.SQS.Model;
using Defra.Lis.Api.Configurations;
using Defra.Lis.Api.Messaging;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

public class SubmissionMessagePublisherTests
{
    private readonly Mock<IAmazonSQS> mockSqs;
    private readonly Mock<IAmazonSimpleNotificationService> mockSns;
    private readonly AwsMessagingOptions options;

    public SubmissionMessagePublisherTests()
    {
        mockSqs = new Mock<IAmazonSQS>();
        mockSns = new Mock<IAmazonSimpleNotificationService>();
        options = new AwsMessagingOptions
        {
            SubmissionValidationQueueUrl = "http://localhost:4566/000000000000/submission_validation_queue",
            SubmissionValidationTopicArn = "arn:aws:sns:eu-west-2:000000000000:submission_validation_topic",
        };
    }

    [Fact]
    public async Task PublishSubmissionForValidationAsync_SendsMessageToSqsAndPublishesToSns()
    {
        mockSqs.Setup(s => s.SendMessageAsync(It.IsAny<SendMessageRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SendMessageResponse { MessageId = "sqs-msg-123" });

        mockSns.Setup(s => s.PublishAsync(It.IsAny<PublishRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PublishResponse { MessageId = "sns-msg-456" });

        var publisher = new SubmissionMessagePublisher(
            mockSqs.Object,
            mockSns.Object,
            Options.Create(options));

        var message = new SubmissionValidationMessage
        {
            SubmissionId = Guid.NewGuid(),
            CountyParishHolding = "12/345/6789",
            ClientReference = "REF123",
            SubmittedBy = "USER1",
            AnimalCount = 3,
        };

        await publisher.PublishSubmissionForValidationAsync(message, TestContext.Current.CancellationToken);

        mockSqs.Verify(
            s => s.SendMessageAsync(
                It.Is<SendMessageRequest>(r =>
                    r.QueueUrl == options.SubmissionValidationQueueUrl &&
                    r.MessageBody.Contains(message.SubmissionId.ToString()) &&
                    r.MessageBody.Contains("12/345/6789")),
                It.IsAny<CancellationToken>()),
            Times.Once);

        mockSns.Verify(
            s => s.PublishAsync(
                It.Is<PublishRequest>(r =>
                    r.TopicArn == options.SubmissionValidationTopicArn &&
                    r.Message.Contains(message.SubmissionId.ToString())),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task PublishSubmissionForValidationAsync_WhenSnsClientIsNull_OnlySendsToSqs()
    {
        mockSqs.Setup(s => s.SendMessageAsync(It.IsAny<SendMessageRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SendMessageResponse { MessageId = "sqs-msg-123" });

        var publisher = new SubmissionMessagePublisher(
            mockSqs.Object,
            null,
            Options.Create(options));

        var message = new SubmissionValidationMessage
        {
            SubmissionId = Guid.NewGuid(),
            CountyParishHolding = "12/345/6789",
            ClientReference = "REF123",
        };

        await publisher.PublishSubmissionForValidationAsync(message, TestContext.Current.CancellationToken);

        mockSqs.Verify(s => s.SendMessageAsync(It.IsAny<SendMessageRequest>(), It.IsAny<CancellationToken>()), Times.Once);
    }
}
