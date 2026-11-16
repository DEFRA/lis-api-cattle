// <copyright file="RegistrationBundleQueueProcessorTests.cs" company="Defra">
// Copyright (c) Defra. All rights reserved.
// </copyright>

namespace Defra.Lis.Api.Tests.Messaging;

using System.Text.Json;
using Amazon.SQS;
using Amazon.SQS.Model;
using Defra.Lis.Api.Configurations;
using Defra.Lis.Api.Correlation;
using Defra.Lis.Api.Messaging;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;

public class RegistrationBundleQueueProcessorTests
{
    private const string QueueUrl = "http://localhost:4566/000000000000/lis_cattle_registration_bundle";

    private readonly Mock<IAmazonSQS> sqs = new();
    private readonly Mock<IRegistrationBundleHandler> handler = new();
    private readonly AwsMessagingOptions options = new()
    {
        RegistrationBundleQueueUrl = QueueUrl,
        RegistrationBundleVisibilityTimeoutSeconds = 300,
        RegistrationBundleMaxReceiveCount = 5,
    };

    [Fact]
    public async Task ProcessMessagesAsync_HandlesTheBundleThenDeletesTheMessage()
    {
        var bundle = RegistrationBundleHandlerTests.Message();
        Receive(SqsMessage(bundle, receiveCount: 1));
        handler.Setup(h => h.HandleAsync(It.IsAny<RegistrationBundleMessage>(), It.IsAny<CancellationToken>())).ReturnsAsync(RegistrationBundleOutcome.Sent);

        var handled = await CreateProcessor().ProcessMessagesAsync(TestContext.Current.CancellationToken);

        Assert.Equal(1, handled);
        handler.Verify(h => h.HandleAsync(It.Is<RegistrationBundleMessage>(m => m.SubmissionId == bundle.SubmissionId), It.IsAny<CancellationToken>()), Times.Once);
        sqs.Verify(s => s.DeleteMessageAsync(It.Is<DeleteMessageRequest>(d => d.ReceiptHandle == "handle-1" && d.QueueUrl == QueueUrl), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Theory]
    [InlineData(RegistrationBundleOutcome.Failed)]
    [InlineData(RegistrationBundleOutcome.AlreadyHandled)]
    public async Task ProcessMessagesAsync_DeletesTheMessageOnceTheOutcomeIsSaved(RegistrationBundleOutcome outcome)
    {
        Receive(SqsMessage(RegistrationBundleHandlerTests.Message(), receiveCount: 2));
        handler.Setup(h => h.HandleAsync(It.IsAny<RegistrationBundleMessage>(), It.IsAny<CancellationToken>())).ReturnsAsync(outcome);

        await CreateProcessor().ProcessMessagesAsync(TestContext.Current.CancellationToken);

        sqs.Verify(s => s.DeleteMessageAsync(It.IsAny<DeleteMessageRequest>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ProcessMessagesAsync_AsksForAVisibilityTimeoutTheReceiveCountAndTheCorrelationId()
    {
        ReceiveMessageRequest? request = null;
        sqs.Setup(s => s.ReceiveMessageAsync(It.IsAny<ReceiveMessageRequest>(), It.IsAny<CancellationToken>()))
            .Callback<ReceiveMessageRequest, CancellationToken>((r, _) => request = r)
            .ReturnsAsync(new ReceiveMessageResponse { Messages = [] });

        await CreateProcessor().ProcessMessagesAsync(TestContext.Current.CancellationToken);

        Assert.NotNull(request);
        Assert.Equal(QueueUrl, request.QueueUrl);
        Assert.Equal(300, request.VisibilityTimeout);
        Assert.Contains("ApproximateReceiveCount", request.MessageSystemAttributeNames);
        Assert.Contains(RegistrationBundleMessage.CorrelationIdAttribute, request.MessageAttributeNames);
    }

    [Fact]
    public async Task ProcessMessagesAsync_RestoresTheCorrelationIdFromTheMessageAttributeWhileHandling()
    {
        var bundle = RegistrationBundleHandlerTests.Message();
        bundle.CorrelationId = "from-body";
        Receive(SqsMessage(bundle, receiveCount: 1, correlationAttribute: "from-attribute"));
        string? seen = null;
        handler.Setup(h => h.HandleAsync(It.IsAny<RegistrationBundleMessage>(), It.IsAny<CancellationToken>()))
            .Callback(() => seen = CorrelationIdContext.Value)
            .ReturnsAsync(RegistrationBundleOutcome.Sent);

        await CreateProcessor().ProcessMessagesAsync(TestContext.Current.CancellationToken);

        Assert.Equal("from-attribute", seen);
        Assert.Null(CorrelationIdContext.Value);
    }

    [Fact]
    public async Task ProcessMessagesAsync_WhenHandlingFails_LeavesTheMessageForRedelivery()
    {
        Receive(SqsMessage(RegistrationBundleHandlerTests.Message(), receiveCount: 1));
        handler.Setup(h => h.HandleAsync(It.IsAny<RegistrationBundleMessage>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Postgres is down"));
        var logger = new RecordingLogger();

        var handled = await CreateProcessor(logger).ProcessMessagesAsync(TestContext.Current.CancellationToken);

        Assert.Equal(0, handled);
        sqs.Verify(s => s.DeleteMessageAsync(It.IsAny<DeleteMessageRequest>(), It.IsAny<CancellationToken>()), Times.Never);
        Assert.Contains(logger.Entries, e => e.Level == LogLevel.Warning && e.Message.Contains("will be retried", StringComparison.Ordinal));
    }

    [Fact]
    public async Task ProcessMessagesAsync_OnTheLastAttempt_LogsAnErrorWithTheCorrelationIdBeforeTheDeadLetterQueue()
    {
        var bundle = RegistrationBundleHandlerTests.Message();
        Receive(SqsMessage(bundle, receiveCount: 5, correlationAttribute: "corr-dlq"));
        handler.Setup(h => h.HandleAsync(It.IsAny<RegistrationBundleMessage>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Postgres is down"));
        var logger = new RecordingLogger();

        await CreateProcessor(logger).ProcessMessagesAsync(TestContext.Current.CancellationToken);

        sqs.Verify(s => s.DeleteMessageAsync(It.IsAny<DeleteMessageRequest>(), It.IsAny<CancellationToken>()), Times.Never);
        var entry = Assert.Single(logger.Entries, e => e.Level == LogLevel.Error);
        Assert.Contains("dead-letter queue", entry.Message, StringComparison.Ordinal);
        Assert.Contains(bundle.SubmissionId.ToString(), entry.Message, StringComparison.Ordinal);
        Assert.Equal("corr-dlq", entry.CorrelationId);
    }

    [Fact]
    public async Task ProcessMessagesAsync_WhenTheMessageCannotBeRead_LeavesItForTheDeadLetterQueue()
    {
        Receive(new Message { MessageId = "bad", ReceiptHandle = "handle-bad", Body = "{not json" });

        var handled = await CreateProcessor().ProcessMessagesAsync(TestContext.Current.CancellationToken);

        Assert.Equal(0, handled);
        handler.Verify(h => h.HandleAsync(It.IsAny<RegistrationBundleMessage>(), It.IsAny<CancellationToken>()), Times.Never);
        sqs.Verify(s => s.DeleteMessageAsync(It.IsAny<DeleteMessageRequest>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ProcessMessagesAsync_WhenTheQueueIsNotConfigured_ReadsNothing()
    {
        options.RegistrationBundleQueueUrl = string.Empty;

        var handled = await CreateProcessor().ProcessMessagesAsync(TestContext.Current.CancellationToken);

        Assert.Equal(0, handled);
        sqs.Verify(s => s.ReceiveMessageAsync(It.IsAny<ReceiveMessageRequest>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ProcessMessagesAsync_WhenSqsCannotBeReached_ReturnsZero()
    {
        sqs.Setup(s => s.ReceiveMessageAsync(It.IsAny<ReceiveMessageRequest>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new AmazonSQSException("unavailable"));

        var handled = await CreateProcessor().ProcessMessagesAsync(TestContext.Current.CancellationToken);

        Assert.Equal(0, handled);
    }

    private static Message SqsMessage(RegistrationBundleMessage bundle, int receiveCount, string? correlationAttribute = null)
    {
        var message = new Message
        {
            MessageId = $"msg-{bundle.SubmissionId}",
            ReceiptHandle = "handle-1",
            Body = JsonSerializer.Serialize(bundle, RegistrationBundlePublisher.SerializerOptions),
            Attributes = new Dictionary<string, string> { ["ApproximateReceiveCount"] = receiveCount.ToString(System.Globalization.CultureInfo.InvariantCulture) },
        };

        if (correlationAttribute != null)
        {
            message.MessageAttributes = new Dictionary<string, MessageAttributeValue>
            {
                [RegistrationBundleMessage.CorrelationIdAttribute] = new() { DataType = "String", StringValue = correlationAttribute },
            };
        }

        return message;
    }

    private void Receive(params Message[] messages) =>
        sqs.Setup(s => s.ReceiveMessageAsync(It.IsAny<ReceiveMessageRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ReceiveMessageResponse { Messages = [.. messages] });

    private RegistrationBundleQueueProcessor CreateProcessor(ILogger<RegistrationBundleQueueProcessor>? logger = null)
    {
        var services = new ServiceCollection();
        services.AddSingleton(handler.Object);
        var provider = services.BuildServiceProvider();
        return new RegistrationBundleQueueProcessor(
            sqs.Object,
            provider.GetRequiredService<IServiceScopeFactory>(),
            Options.Create(options),
            logger ?? NullLogger<RegistrationBundleQueueProcessor>.Instance);
    }

    private sealed class RecordingLogger : ILogger<RegistrationBundleQueueProcessor>
    {
        public List<(LogLevel Level, string Message, string? CorrelationId)> Entries { get; } = [];

        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) =>
            Entries.Add((logLevel, formatter(state, exception), CorrelationIdContext.Value));
    }
}
