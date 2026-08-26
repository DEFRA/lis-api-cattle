// <copyright file="SubmissionValidationQueueProcessorTests.cs" company="Defra">
// Copyright (c) Defra. All rights reserved.
// </copyright>

namespace Defra.Lis.Api.Tests;

using System.Text.Json;
using Amazon.SQS;
using Amazon.SQS.Model;
using Defra.Lis.Api.Configurations;
using Defra.Lis.Api.Messaging;
using Defra.Lis.Api.Validation;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

public class SubmissionValidationQueueProcessorTests
{
    private readonly Mock<IAmazonSQS> mockSqs;
    private readonly Mock<ISubmissionValidationService> mockValidationService;
    private readonly AwsMessagingOptions options;

    public SubmissionValidationQueueProcessorTests()
    {
        mockSqs = new Mock<IAmazonSQS>();
        mockValidationService = new Mock<ISubmissionValidationService>();
        options = new AwsMessagingOptions
        {
            SubmissionValidationQueueUrl = "http://localhost:4566/000000000000/submission-validation-queue",
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
            ClientReference = "REF123",
        };

        var rawMessageBody = JsonSerializer.Serialize(message);

        mockSqs.Setup(s => s.ReceiveMessageAsync(It.IsAny<ReceiveMessageRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ReceiveMessageResponse
            {
                Messages =
                [
                    new Message
                    {
                        MessageId = "msg-1",
                        ReceiptHandle = "handle-1",
                        Body = rawMessageBody,
                    },
                ],
            });

        mockValidationService.Setup(v => v.ValidateSubmissionByIdAsync(submissionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SubmissionValidationResult { SubmissionId = submissionId, IsValid = true });

        var processor = new SubmissionValidationQueueProcessor(
            mockSqs.Object,
            mockValidationService.Object,
            Options.Create(options));

        var processedCount = await processor.ProcessMessagesAsync(TestContext.Current.CancellationToken);

        Assert.Equal(1, processedCount);

        mockValidationService.Verify(v => v.ValidateSubmissionByIdAsync(submissionId, It.IsAny<CancellationToken>()), Times.Once);
        mockSqs.Verify(
            s => s.DeleteMessageAsync(
                It.Is<DeleteMessageRequest>(d =>
                    d.ReceiptHandle == "handle-1" && d.QueueUrl == options.SubmissionValidationQueueUrl),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task ProcessMessagesAsync_WhenDeliveredViaSnsSubscription_ExtractsNestedMessageAndValidates()
    {
        var submissionId = Guid.NewGuid();
        var message = new SubmissionValidationMessage
        {
            SubmissionId = submissionId,
            CountyParishHolding = "12/345/6789",
            ClientReference = "REF123",
        };

        var nestedMessageJson = JsonSerializer.Serialize(message);
        var snsEnvelope = JsonSerializer.Serialize(new
        {
            Type = "Notification",
            MessageId = "sns-uuid",
            TopicArn = "arn:aws:sns:eu-west-2:000000000000:submission-validation-topic",
            Message = nestedMessageJson,
        });

        mockSqs.Setup(s => s.ReceiveMessageAsync(It.IsAny<ReceiveMessageRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ReceiveMessageResponse
            {
                Messages =
                [
                    new Message
                    {
                        MessageId = "msg-sns-1",
                        ReceiptHandle = "handle-sns-1",
                        Body = snsEnvelope,
                    },
                ],
            });

        mockValidationService.Setup(v => v.ValidateSubmissionByIdAsync(submissionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SubmissionValidationResult { SubmissionId = submissionId, IsValid = true });

        var processor = new SubmissionValidationQueueProcessor(
            mockSqs.Object,
            mockValidationService.Object,
            Options.Create(options));

        var processedCount = await processor.ProcessMessagesAsync(TestContext.Current.CancellationToken);

        Assert.Equal(1, processedCount);
        mockValidationService.Verify(v => v.ValidateSubmissionByIdAsync(submissionId, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ProcessMessagesAsync_WhenQueueEmpty_ReturnsZero()
    {
        mockSqs.Setup(s => s.ReceiveMessageAsync(It.IsAny<ReceiveMessageRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ReceiveMessageResponse
            {
                Messages = [],
            });

        var processor = new SubmissionValidationQueueProcessor(
            mockSqs.Object,
            mockValidationService.Object,
            Options.Create(options));

        var processedCount = await processor.ProcessMessagesAsync(TestContext.Current.CancellationToken);

        Assert.Equal(0, processedCount);
        mockValidationService.Verify(v => v.ValidateSubmissionByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
