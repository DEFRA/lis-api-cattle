// <copyright file="RegistrationBundlePublisherTests.cs" company="Defra">
// Copyright (c) Defra. All rights reserved.
// </copyright>

namespace Defra.Lis.Api.Tests.Messaging;

using System.Text.Json;
using Amazon.SQS;
using Amazon.SQS.Model;
using Defra.Lis.Api.Configurations;
using Defra.Lis.Api.Messaging;
using Defra.Livestock.Sdk.Api.Strategies.Abstractions.Exceptions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;

public class RegistrationBundlePublisherTests
{
    private const string QueueUrl = "http://localhost:4566/000000000000/lis_cattle_registration_bundle";

    private readonly Mock<IAmazonSQS> sqs = new();

    [Fact]
    public async Task PublishAsync_SendsTheBundleWithTheCorrelationIdAttribute()
    {
        // Arrange
        SendMessageRequest? sent = null;
        sqs.Setup(s => s.SendMessageAsync(It.IsAny<SendMessageRequest>(), It.IsAny<CancellationToken>()))
            .Callback<SendMessageRequest, CancellationToken>((r, _) => sent = r)
            .ReturnsAsync(new SendMessageResponse { MessageId = "m-1" });
        var message = RegistrationBundleHandlerTests.Message();

        // Act
        await CreatePublisher(QueueUrl).PublishAsync(message, TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(sent);
        Assert.Equal(QueueUrl, sent.QueueUrl);
        Assert.Equal("corr-925", sent.MessageAttributes[RegistrationBundleMessage.CorrelationIdAttribute].StringValue);
        var body = JsonSerializer.Deserialize<RegistrationBundleMessage>(
            sent.MessageBody,
            RegistrationBundlePublisher.SerializerOptions);
        Assert.NotNull(body);
        Assert.Equal(message.SubmissionId, body.SubmissionId);
        Assert.Equal("corr-925", body.CorrelationId);
        Assert.Equal("REG-925", body.Bundle.ClientReference);
    }

    [Fact]
    public async Task PublishAsync_WhenSqsFails_ThrowsServiceUnavailable()
    {
        // Arrange
        sqs.Setup(s => s.SendMessageAsync(It.IsAny<SendMessageRequest>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new AmazonSQSException("Service unavailable"));

        // Act & Assert
        await Assert.ThrowsAsync<ServiceUnavailableException>(() =>
            CreatePublisher(QueueUrl).PublishAsync(
                RegistrationBundleHandlerTests.Message(),
                TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task PublishAsync_WhenSqsCannotBeReached_ThrowsServiceUnavailable()
    {
        // Arrange
        sqs.Setup(s => s.SendMessageAsync(It.IsAny<SendMessageRequest>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("Connection refused"));

        // Act & Assert
        await Assert.ThrowsAsync<ServiceUnavailableException>(() =>
            CreatePublisher(QueueUrl).PublishAsync(
                RegistrationBundleHandlerTests.Message(),
                TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task PublishAsync_WhenTheQueueIsNotConfigured_ThrowsServiceUnavailableAndSendsNothing()
    {
        // Arrange
        var publisher = CreatePublisher(string.Empty);
        var message = RegistrationBundleHandlerTests.Message();

        // Act & Assert
        await Assert.ThrowsAsync<ServiceUnavailableException>(() =>
            publisher.PublishAsync(message, TestContext.Current.CancellationToken));

        // Assert
        sqs.Verify(s => s.SendMessageAsync(It.IsAny<SendMessageRequest>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    private RegistrationBundlePublisher CreatePublisher(string queueUrl) =>
        new(
            sqs.Object,
            Options.Create(new AwsMessagingOptions { RegistrationBundleQueueUrl = queueUrl }),
            NullLogger<RegistrationBundlePublisher>.Instance);
}
