// <copyright file="RegistrationBundleBackgroundService.cs" company="Defra">
// Copyright (c) Defra. All rights reserved.
// </copyright>

namespace Defra.Lis.Api.Messaging;

using Defra.Lis.Api.Configurations;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

/// <summary>
/// Polls the registration bundle queue for as long as the service runs.
/// </summary>
public partial class RegistrationBundleBackgroundService(
    IRegistrationBundleQueueProcessor processor,
    IOptions<AwsMessagingOptions> options,
    ILogger<RegistrationBundleBackgroundService> logger)
    : BackgroundService
{
    private readonly IRegistrationBundleQueueProcessor processor = processor ?? throw new ArgumentNullException(nameof(processor));
    private readonly AwsMessagingOptions options = options.Value;

    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        LogRegistrationBundleConsumerStarted();
        var idleDelay = TimeSpan.FromSeconds(Math.Max(1, options.PollingIntervalSeconds));

        while (!stoppingToken.IsCancellationRequested)
        {
            int handled;
            try
            {
                handled = await processor.ProcessMessagesAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                LogUnhandledErrorInRegistrationBundleConsumer(ex);
                handled = 0;
            }

            // Keep reading while there is work; wait between polls only when the queue was empty.
            if (handled == 0)
            {
                try
                {
                    await Task.Delay(idleDelay, stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }
        }

        LogRegistrationBundleConsumerStopped();
    }
}
