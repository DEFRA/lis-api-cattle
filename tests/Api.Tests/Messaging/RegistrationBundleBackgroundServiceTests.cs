// <copyright file="RegistrationBundleBackgroundServiceTests.cs" company="Defra">
// Copyright (c) Defra. All rights reserved.
// </copyright>

namespace Defra.Lis.Api.Tests.Messaging;

using Defra.Lis.Api.Configurations;
using Defra.Lis.Api.Messaging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

public class RegistrationBundleBackgroundServiceTests
{
    [Fact]
    public async Task ExecuteAsync_KeepsReadingWhileThereIsWorkAndSurvivesAProcessorError()
    {
        var processor = new ScriptedProcessor([2, -1, 1]);
        using var service = CreateService(processor);

        await service.StartAsync(TestContext.Current.CancellationToken);
        await processor.Finished.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
        await service.StopAsync(TestContext.Current.CancellationToken);

        // 2 handled, then an error (followed by the idle delay), then 1 handled: the loop did not stop on the error.
        Assert.Equal(3, processor.Calls);
    }

    [Fact]
    public async Task ExecuteAsync_StopsWhenTheServiceStops()
    {
        var processor = new ScriptedProcessor([]);
        using var service = CreateService(processor);

        await service.StartAsync(TestContext.Current.CancellationToken);
        await service.StopAsync(TestContext.Current.CancellationToken);

        Assert.True(service.ExecuteTask?.IsCompleted);
    }

    [Fact]
    public void Constructor_RequiresAProcessor()
    {
        Assert.Throws<ArgumentNullException>(() => new RegistrationBundleBackgroundService(
            null!,
            Options.Create(new AwsMessagingOptions()),
            NullLogger<RegistrationBundleBackgroundService>.Instance));
    }

    private static RegistrationBundleBackgroundService CreateService(IRegistrationBundleQueueProcessor processor) =>
        new(processor, Options.Create(new AwsMessagingOptions { PollingIntervalSeconds = 1 }), NullLogger<RegistrationBundleBackgroundService>.Instance);

    /// <summary>Returns the scripted counts in turn (-1 throws), then waits for the service to stop.</summary>
    private sealed class ScriptedProcessor(int[] script) : IRegistrationBundleQueueProcessor
    {
        private readonly TaskCompletionSource finished = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public int Calls { get; private set; }

        public Task Finished => finished.Task;

        public async Task<int> ProcessMessagesAsync(CancellationToken cancellationToken = default)
        {
            if (Calls >= script.Length)
            {
                finished.TrySetResult();
                await Task.Delay(Timeout.Infinite, cancellationToken);
                return 0;
            }

            var next = script[Calls++];
            return next < 0 ? throw new InvalidOperationException("SQS failed") : next;
        }
    }
}
