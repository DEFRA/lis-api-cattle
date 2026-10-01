// <copyright file="SubmissionValidationBackgroundService.cs" company="Defra">
// Copyright (c) Defra. All rights reserved.
// </copyright>

namespace Defra.Lis.Api.Messaging;

using Defra.Lis.Api.Configurations;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

public partial class SubmissionValidationBackgroundService(
    IServiceProvider serviceProvider,
    IOptions<AwsMessagingOptions>? options = null,
    ILogger<SubmissionValidationBackgroundService>? logger = null)
    : BackgroundService
{
    private readonly IServiceProvider serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
    private readonly AwsMessagingOptions options = options?.Value ?? new AwsMessagingOptions();
    private readonly ILogger<SubmissionValidationBackgroundService>? logger = logger;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!options.EnableBackgroundConsumer)
        {
            LogSubmissionvalidationbackgroundserviceIsDisabled();
            return;
        }

        LogSubmissionvalidationbackgroundserviceStarted();

        var interval = TimeSpan.FromSeconds(Math.Max(1, options.PollingIntervalSeconds));

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = serviceProvider.CreateScope();
                var processor = scope.ServiceProvider.GetRequiredService<ISubmissionValidationQueueProcessor>();
                await processor.ProcessMessagesAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                LogUnhandledErrorOccurredInSubmissionvalidationbackgroundservicePollingLoop(ex);
            }

            try
            {
                await Task.Delay(interval, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }

        LogSubmissionvalidationbackgroundserviceStopped();
    }
}
