// <copyright file="HealthService.cs" company="Defra">
// Copyright (c) Defra. All rights reserved.
// </copyright>

namespace Defra.Lis.Api.Services;

using System.Diagnostics;
using Amazon.SQS;
using Amazon.SQS.Model;
using Defra.Database.Postgres;
using Defra.Lis.Api.Configurations;
using Defra.Lis.Api.Interfaces;
using Defra.Lis.Api.Jobs;
using Defra.Lis.Api.Models;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Quartz;

public class HealthService : IHealthService
{
    public static readonly TimeSpan DefaultCheckTimeout = TimeSpan.FromSeconds(10);

    private readonly PostgresDbContext dbContext;
    private readonly IAmazonSQS sqsClient;
    private readonly ISchedulerFactory schedulerFactory;
    private readonly AwsMessagingOptions awsOptions;
    private readonly CtsPollingJobOptions ctsJobOptions;
    private readonly ILogger<HealthService> logger;
    private readonly TimeSpan checkTimeout;

    public HealthService(
        ILogger<HealthService> logger,
        PostgresDbContext dbContext,
        IAmazonSQS sqsClient,
        ISchedulerFactory schedulerFactory,
        IOptions<AwsMessagingOptions> awsOptions,
        IOptions<CtsPollingJobOptions> ctsJobOptions,
        TimeSpan? checkTimeout = null)
    {
        this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
        this.dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        this.sqsClient = sqsClient ?? throw new ArgumentNullException(nameof(sqsClient));
        this.schedulerFactory = schedulerFactory ?? throw new ArgumentNullException(nameof(schedulerFactory));
        this.awsOptions = awsOptions?.Value ?? throw new ArgumentNullException(nameof(awsOptions));
        this.ctsJobOptions = ctsJobOptions?.Value ?? throw new ArgumentNullException(nameof(ctsJobOptions));
        this.checkTimeout = checkTimeout ?? DefaultCheckTimeout;
    }

    public async Task<HealthCheckResponse> CheckHealthAsync(CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();
        var entries = new Dictionary<string, ComponentHealthResult>();

        var dbTask = CheckDatabaseHealthAsync(cancellationToken);
        var queueTask = CheckQueueHealthAsync(cancellationToken);
        var quartzTask = CheckQuartzHealthAsync(cancellationToken);

        await Task.WhenAll(dbTask, queueTask, quartzTask);

        entries["database"] = await dbTask;
        entries["queue"] = await queueTask;
        entries["quartz"] = await quartzTask;

        stopwatch.Stop();

        var isHealthy = entries.Values.All(e => e.Status == nameof(HealthStatus.Healthy));
        var isDegraded = entries.Values.Any(e => e.Status == nameof(HealthStatus.Degraded));

        string overallStatus;

        if (isHealthy)
        {
            overallStatus = nameof(HealthStatus.Healthy);
        }
        else if (isDegraded)
        {
            overallStatus = nameof(HealthStatus.Degraded);
        }
        else
        {
            overallStatus = nameof(HealthStatus.Unhealthy);
        }

        return new HealthCheckResponse(overallStatus, stopwatch.Elapsed, entries);
    }

    public async Task<ComponentHealthResult> CheckDatabaseHealthAsync(CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();

        using var timeoutCts = new CancellationTokenSource(checkTimeout);
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);

        try
        {
            var canConnect = await dbContext.Database.CanConnectAsync(linkedCts.Token);

            stopwatch.Stop();

            if (canConnect)
            {
                return new ComponentHealthResult(
                    nameof(HealthStatus.Healthy),
                    "PostgreSQL database connection is healthy.",
                    stopwatch.Elapsed);
            }

            return new ComponentHealthResult(
                nameof(HealthStatus.Unhealthy),
                "Cannot connect to the PostgreSQL database.",
                stopwatch.Elapsed);
        }
        catch (OperationCanceledException ex) when (timeoutCts.IsCancellationRequested &&
                                                    !cancellationToken.IsCancellationRequested)
        {
            stopwatch.Stop();

            logger.LogWarning(ex, "Database health check timed out after {Timeout}", checkTimeout);

            return new ComponentHealthResult(
                nameof(HealthStatus.Unhealthy),
                $"Database health check timed out after {checkTimeout.TotalSeconds}s.",
                stopwatch.Elapsed);
        }
        catch (Exception ex)
        {
            stopwatch.Stop();

            logger.LogError(ex, "Database health check failed");

            return new ComponentHealthResult(
                nameof(HealthStatus.Unhealthy),
                $"Database health check failed: {ex.Message}",
                stopwatch.Elapsed);
        }
    }

    public async Task<ComponentHealthResult> CheckQueueHealthAsync(CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();

        var queueUrl = awsOptions.SubmissionValidationQueueUrl;

        if (string.IsNullOrWhiteSpace(queueUrl))
        {
            stopwatch.Stop();

            return new ComponentHealthResult(
                nameof(HealthStatus.Healthy),
                "Queue health check skipped: No queue URL configured.",
                stopwatch.Elapsed);
        }

        using var timeoutCts = new CancellationTokenSource(checkTimeout);
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);

        try
        {
            var queueName = ExtractQueueName(queueUrl);
            var effectiveQueueUrl = await ResolveQueueUrlAsync(queueUrl, queueName, linkedCts.Token);

            GetQueueAttributesResponse attributesResponse;

            try
            {
                var request = new GetQueueAttributesRequest { QueueUrl = effectiveQueueUrl, AttributeNames = ["All"], };

                attributesResponse = await sqsClient.GetQueueAttributesAsync(request, linkedCts.Token);
            }
            catch (QueueDoesNotExistException) when (!string.IsNullOrWhiteSpace(queueName))
            {
                // Fall back: resolve URL via GetQueueUrlAsync in case configured URL host differed from client host
                var urlResponse =
                    await sqsClient.GetQueueUrlAsync(
                        new GetQueueUrlRequest { QueueName = queueName },
                        linkedCts.Token);
                effectiveQueueUrl = urlResponse.QueueUrl;

                attributesResponse = await sqsClient.GetQueueAttributesAsync(
                    new GetQueueAttributesRequest { QueueUrl = effectiveQueueUrl, AttributeNames = ["All"], },
                    linkedCts.Token);
            }

            stopwatch.Stop();

            var messageCount = attributesResponse.Attributes != null &&
                               attributesResponse.Attributes.TryGetValue(
                                   QueueAttributeName.ApproximateNumberOfMessages,
                                   out var countStr) && int.TryParse(countStr, out var count)
                ? count
                : 0;

            return new ComponentHealthResult(
                nameof(HealthStatus.Healthy),
                "SQS queue is healthy and accessible.",
                stopwatch.Elapsed,
                new Dictionary<string, object>
                {
                    ["queueUrl"] = effectiveQueueUrl, ["approximateNumberOfMessages"] = messageCount,
                });
        }
        catch (OperationCanceledException ex) when (timeoutCts.IsCancellationRequested &&
                                                    !cancellationToken.IsCancellationRequested)
        {
            stopwatch.Stop();

            logger.LogWarning(ex, "Queue health check timed out after {Timeout}", checkTimeout);

            return new ComponentHealthResult(
                nameof(HealthStatus.Unhealthy),
                $"Queue health check timed out after {checkTimeout.TotalSeconds}s.",
                stopwatch.Elapsed,
                new Dictionary<string, object> { ["queueUrl"] = queueUrl });
        }
        catch (Exception ex)
        {
            stopwatch.Stop();

            logger.LogError(ex, "Queue health check failed for SQS");

            return new ComponentHealthResult(
                nameof(HealthStatus.Unhealthy),
                $"Queue health check failed: {ex.Message}",
                stopwatch.Elapsed,
                new Dictionary<string, object> { ["queueUrl"] = queueUrl });
        }
    }

    public async Task<ComponentHealthResult> CheckQuartzHealthAsync(CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();

        using var timeoutCts = new CancellationTokenSource(checkTimeout);
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);

        try
        {
            var scheduler = await schedulerFactory.GetScheduler(linkedCts.Token);

            if (scheduler == null)
            {
                stopwatch.Stop();

                return new ComponentHealthResult(
                    nameof(HealthStatus.Unhealthy),
                    "Quartz scheduler instance could not be obtained.",
                    stopwatch.Elapsed);
            }

            if (scheduler.IsShutdown)
            {
                stopwatch.Stop();

                return new ComponentHealthResult(
                    nameof(HealthStatus.Unhealthy),
                    "Quartz scheduler is shutdown.",
                    stopwatch.Elapsed,
                    new Dictionary<string, object>
                    {
                        ["schedulerName"] = scheduler.SchedulerName,
                        ["isStarted"] = scheduler.IsStarted,
                        ["inStandbyMode"] = scheduler.InStandbyMode,
                        ["isShutdown"] = scheduler.IsShutdown,
                    });
            }

            var jobKey = new JobKey(nameof(CtsBundlePollingJob));
            var jobExists = await scheduler.CheckExists(jobKey, linkedCts.Token);

            var triggers = await scheduler.GetTriggersOfJob(jobKey, linkedCts.Token);
            var triggerList = triggers.ToList();

            var data = new Dictionary<string, object>
            {
                ["schedulerName"] = scheduler.SchedulerName,
                ["schedulerInstanceId"] = scheduler.SchedulerInstanceId,
                ["isStarted"] = scheduler.IsStarted,
                ["inStandbyMode"] = scheduler.InStandbyMode,
                ["isShutdown"] = scheduler.IsShutdown,
                ["jobEnabled"] = ctsJobOptions.Enabled,
                ["jobExists"] = jobExists,
                ["triggersCount"] = triggerList.Count,
            };

            if (ctsJobOptions.Enabled && !jobExists)
            {
                stopwatch.Stop();

                return new ComponentHealthResult(
                    nameof(HealthStatus.Unhealthy),
                    $"Quartz background job '{nameof(CtsBundlePollingJob)}' is enabled in options but not registered in scheduler.",
                    stopwatch.Elapsed,
                    data);
            }

            if (ctsJobOptions.Enabled && triggerList.Count == 0)
            {
                stopwatch.Stop();

                return new ComponentHealthResult(
                    nameof(HealthStatus.Degraded),
                    $"Quartz background job '{nameof(CtsBundlePollingJob)}' is registered but has no triggers scheduled.",
                    stopwatch.Elapsed,
                    data);
            }

            if (triggerList.Count > 0)
            {
                var trigger = triggerList[0];
                var triggerState = await scheduler.GetTriggerState(trigger.Key, linkedCts.Token);
                var nextFireTimeUtc = trigger.GetNextFireTimeUtc();

                data["triggerKey"] = trigger.Key.ToString();
                data["triggerState"] = triggerState.ToString();

                AddDataIfNotNull(data, "nextFireTimeUtc", nextFireTimeUtc?.ToString("o"));

                if (triggerState == TriggerState.Error)
                {
                    stopwatch.Stop();

                    return new ComponentHealthResult(
                        nameof(HealthStatus.Unhealthy),
                        $"Quartz background trigger '{trigger.Key}' is in Error state.",
                        stopwatch.Elapsed,
                        data);
                }

                if (triggerState == TriggerState.Paused)
                {
                    stopwatch.Stop();

                    return new ComponentHealthResult(
                        nameof(HealthStatus.Degraded),
                        $"Quartz background trigger '{trigger.Key}' is Paused.",
                        stopwatch.Elapsed,
                        data);
                }
            }

            stopwatch.Stop();

            var description = ctsJobOptions.Enabled
                ? $"Quartz scheduler is healthy and '{nameof(CtsBundlePollingJob)}' is scheduled."
                : "Quartz scheduler is healthy (background job is disabled by configuration).";

            return new ComponentHealthResult(
                nameof(HealthStatus.Healthy),
                description,
                stopwatch.Elapsed,
                data);
        }
        catch (OperationCanceledException ex) when (timeoutCts.IsCancellationRequested &&
                                                    !cancellationToken.IsCancellationRequested)
        {
            stopwatch.Stop();

            logger.LogWarning(ex, "Quartz health check timed out after {Timeout}", checkTimeout);

            return new ComponentHealthResult(
                nameof(HealthStatus.Unhealthy),
                $"Quartz health check timed out after {checkTimeout.TotalSeconds}s.",
                stopwatch.Elapsed);
        }
        catch (Exception ex)
        {
            stopwatch.Stop();

            logger.LogError(ex, "Quartz health check failed");

            return new ComponentHealthResult(
                nameof(HealthStatus.Unhealthy),
                $"Quartz health check failed: {ex.Message}",
                stopwatch.Elapsed);
        }
    }

    private static void AddDataIfNotNull(Dictionary<string, object> data, string key, object? value)
    {
        if (value != null)
        {
            data[key] = value;
        }
    }

    private static string ExtractQueueName(string queueUrl)
    {
        if (string.IsNullOrWhiteSpace(queueUrl))
        {
            return string.Empty;
        }

        var trimmed = queueUrl.TrimEnd('/');
        var lastSlashIndex = trimmed.LastIndexOf('/');
        return lastSlashIndex >= 0 ? trimmed[(lastSlashIndex + 1)..] : trimmed;
    }

    private async Task<string> ResolveQueueUrlAsync(
        string configuredUrl,
        string queueName,
        CancellationToken cancellationToken)
    {
        if (configuredUrl.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
            configuredUrl.StartsWith("https://", StringComparison.OrdinalIgnoreCase) ||
            string.IsNullOrWhiteSpace(queueName))
        {
            return configuredUrl;
        }

        var getUrlResponse =
            await sqsClient.GetQueueUrlAsync(
                new GetQueueUrlRequest { QueueName = queueName },
                cancellationToken);
        return getUrlResponse.QueueUrl;
    }
}
