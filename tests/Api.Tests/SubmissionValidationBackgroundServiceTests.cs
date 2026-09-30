// <copyright file="SubmissionValidationBackgroundServiceTests.cs" company="Defra">
// Copyright (c) Defra. All rights reserved.
// </copyright>

namespace Defra.Lis.Api.Tests;

using Defra.Lis.Api.Configurations;
using Defra.Lis.Api.Messaging;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;

public class SubmissionValidationBackgroundServiceTests
{
    [Fact]
    public async Task StartAsync_WhenConsumerIsDisabled_DoesNotProcessMessages()
    {
        var processor = new Mock<ISubmissionValidationQueueProcessor>();
        await using var serviceProvider = CreateServiceProvider(processor.Object);
        var service = CreateService(serviceProvider, enableBackgroundConsumer: false, pollingIntervalSeconds: 1);

        await service.StartAsync(TestContext.Current.CancellationToken);
        await service.StopAsync(TestContext.Current.CancellationToken);

        processor.Verify(
            p => p.ProcessMessagesAsync(It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task StartAsync_WhenProcessingIsCancelled_StopsConsumerLoop()
    {
        using var stoppingSource = new CancellationTokenSource();
        var processorCalled = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var processor = new Mock<ISubmissionValidationQueueProcessor>();
        processor
            .Setup(p => p.ProcessMessagesAsync(It.IsAny<CancellationToken>()))
            .Callback(() =>
            {
                stoppingSource.Cancel();
                processorCalled.SetResult();
            })
            .ThrowsAsync(new OperationCanceledException());

        await using var serviceProvider = CreateServiceProvider(processor.Object);
        var service = CreateService(serviceProvider, enableBackgroundConsumer: true, pollingIntervalSeconds: 1);

        await service.StartAsync(stoppingSource.Token);
        await processorCalled.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        await service.StopAsync(TestContext.Current.CancellationToken);

        processor.Verify(p => p.ProcessMessagesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task StartAsync_WhenProcessorFails_HandlesErrorAndStopsWhenCancelled()
    {
        using var stoppingSource = new CancellationTokenSource();
        var processorCalled = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var processor = new Mock<ISubmissionValidationQueueProcessor>();
        processor
            .Setup(p => p.ProcessMessagesAsync(It.IsAny<CancellationToken>()))
            .Callback(() =>
            {
                stoppingSource.Cancel();
                processorCalled.SetResult();
            })
            .ThrowsAsync(new InvalidOperationException("Queue unavailable."));

        await using var serviceProvider = CreateServiceProvider(processor.Object);
        var service = CreateService(serviceProvider, enableBackgroundConsumer: true, pollingIntervalSeconds: 1);

        await service.StartAsync(stoppingSource.Token);
        await processorCalled.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        await service.StopAsync(TestContext.Current.CancellationToken);

        processor.Verify(p => p.ProcessMessagesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    private static ServiceProvider CreateServiceProvider(ISubmissionValidationQueueProcessor processor)
    {
        return new ServiceCollection()
            .AddScoped(_ => processor)
            .BuildServiceProvider();
    }

    private static SubmissionValidationBackgroundService CreateService(
        IServiceProvider serviceProvider,
        bool enableBackgroundConsumer,
        int pollingIntervalSeconds)
    {
        var options = Options.Create(new AwsMessagingOptions
        {
            EnableBackgroundConsumer = enableBackgroundConsumer,
            PollingIntervalSeconds = pollingIntervalSeconds,
        });

        return new SubmissionValidationBackgroundService(
            serviceProvider,
            options,
            NullLogger<SubmissionValidationBackgroundService>.Instance);
    }
}
