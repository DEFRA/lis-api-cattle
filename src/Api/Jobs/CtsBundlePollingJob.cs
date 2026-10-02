// <copyright file="CtsBundlePollingJob.cs" company="Defra">
// Copyright (c) Defra. All rights reserved.
// </copyright>

namespace Defra.Lis.Api.Jobs;

using Defra.Lis.Api.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Quartz;

[DisallowConcurrentExecution]
public partial class CtsBundlePollingJob(
    IServiceScopeFactory scopeFactory,
    ILogger<CtsBundlePollingJob> logger)
    : IJob
{
    private readonly IServiceScopeFactory scopeFactory = scopeFactory ?? throw new ArgumentNullException(nameof(scopeFactory));
    private readonly ILogger<CtsBundlePollingJob> logger = logger ?? throw new ArgumentNullException(nameof(logger));

    public async ValueTask Execute(IJobExecutionContext context, CancellationToken cancellationToken = default)
    {
        LogStartingCtsBundlePollingJobExecutionAtTime(DateTimeOffset.UtcNow);

        try
        {
            using var scope = scopeFactory.CreateScope();
            var processor = scope.ServiceProvider.GetRequiredService<ICtsBundleProcessorService>();
            await processor.ProcessPendingBundlesAsync(cancellationToken);
            LogFinishedCtsBundlePollingJobExecutionSuccessfullyAtTime(DateTimeOffset.UtcNow);
        }
#pragma warning disable S2139
        catch (Exception ex)
#pragma warning restore S2139
        {
            LogCtsBundlePollingJobExecutionFailed(ex);
            throw;
        }
    }
}
