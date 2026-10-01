// <copyright file="CtsBundlePollingJob.cs" company="Defra">
// Copyright (c) Defra. All rights reserved.
// </copyright>

namespace Defra.Lis.Api.Jobs;

using Defra.Lis.Api.Interfaces;
using Defra.Lis.Api.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Quartz;

[DisallowConcurrentExecution]
public partial class CtsBundlePollingJob(
    IServiceScopeFactory scopeFactory,
    ILogger<CtsBundlePollingJob> logger)
    : IJob
{
    private readonly IServiceScopeFactory scopeFactory = scopeFactory ?? throw new ArgumentNullException(nameof(scopeFactory));
    private readonly ILogger<CtsBundlePollingJob> logger = logger ?? throw new ArgumentNullException(nameof(logger));

    public async Task Execute(IJobExecutionContext context)
    {
        LogStartingCtsBundlePollingJobExecutionAtTime(DateTimeOffset.UtcNow);

        try
        {
            using var scope = scopeFactory.CreateScope();
            var processor = scope.ServiceProvider.GetRequiredService<ICtsBundleProcessorService>();
            await processor.ProcessPendingBundlesAsync(context.CancellationToken);
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
