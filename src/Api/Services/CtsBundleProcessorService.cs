// <copyright file="CtsBundleProcessorService.cs" company="Defra">
// Copyright (c) Defra. All rights reserved.
// </copyright>

namespace Defra.Lis.Api.Services;

using Defra.Database.Postgres;
using Defra.Lis.Api.Interfaces;
using Defra.Lis.Api.Models;
using Defra.Lis.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

public partial class CtsBundleProcessorService(
    DbContext dbContext,
    ICtsService ctsService,
    IOptions<CtsPollingJobOptions> options,
    ILogger<CtsBundleProcessorService> logger)
    : ICtsBundleProcessorService
{
    private readonly CtsPollingJobOptions options = options.Value;

    public CtsBundleProcessorService(
        PostgresDbContext dbContext,
        ICtsService ctsService,
        IOptions<CtsPollingJobOptions> options,
        ILogger<CtsBundleProcessorService> logger)
        : this((DbContext)dbContext, ctsService, options, logger)
    {
    }

    public async Task ProcessPendingBundlesAsync(CancellationToken cancellationToken = default)
    {
        var batchSize = options.BatchSize > 0 ? options.BatchSize : 10;

        var targetStatuses = new[] { Statuses.Submitted, Statuses.Processing, Statuses.Error, Statuses.Pending };

        var bundles = await dbContext.Set<Submission>()
            .Include(s => s.Animals)
                .ThenInclude(a => a.Errors)
            .Where(s => targetStatuses.Contains(s.Status))
            .OrderBy(s => s.CreatedAt)
            .Take(batchSize)
            .ToListAsync(cancellationToken);

        if (bundles.Count == 0)
        {
            LogNoPendingBundlesFoundToProcess();
            return;
        }

        LogFoundCountBundlesToProcessWithCts(bundles.Count);

        foreach (var bundle in bundles)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                break;
            }

            try
            {
                await ProcessBundleAsync(bundle, cancellationToken);
            }
            catch (Exception ex)
            {
                LogErrorProcessingBundleBundleid(bundle.Id, ex);
            }
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task ProcessBundleAsync(Submission bundle, CancellationToken cancellationToken)
    {
        string[] toProcess = [Statuses.Submitted, Statuses.Pending];
        string[] beingProcess = [Statuses.Processing, Statuses.Error];

        if (toProcess.Contains(bundle.Status))
        {
            bundle.MarkAsProcessing();
            await SubmitAnimalRegistrationAndHandleResponse(bundle, cancellationToken);
        }
        else if (beingProcess.Contains(bundle.Status))
        {
            var targetAnimals = bundle.Animals
                .Where(a => a.Status == Statuses.Processing || a.Status == Statuses.Error || a.Status == Statuses.Submitted || a.Status == Statuses.Pending)
                .ToList();
            await CheckAnimalStatusAndUpdate(targetAnimals, cancellationToken);
        }

        bundle.RefreshStatusFromAnimals();
    }

    private async Task CheckAnimalStatusAndUpdate(List<SubmissionAnimal> targetAnimals, CancellationToken cancellationToken)
    {
        foreach (var animal in targetAnimals)
        {
            var response = await ctsService.CheckAnimalStatusAsync(animal.EarTag, animal.Id, cancellationToken);

            if (response.IsClean)
            {
                animal.MarkAsComplete();
            }
            else if (response.IsError)
            {
                var errorCode = response.Errors.FirstOrDefault()?.ErrorCode ?? "CTS_ERR";
                var errorText = response.Errors.FirstOrDefault()?.ErrorText ?? "CTS Validation Error";
                animal.MarkAsError(errorCode, errorText);
            }
            else
            {
                animal.MarkAsProcessing();
            }
        }
    }

    private async Task SubmitAnimalRegistrationAndHandleResponse(Submission bundle, CancellationToken cancellationToken)
    {
        foreach (var animal in bundle.Animals)
        {
            var response = await ctsService.SubmitAnimalRegistrationAsync(animal, cancellationToken);
            if (response.IsError)
            {
                animal.MarkAsError(
                    response.Errors.FirstOrDefault()?.ErrorCode ?? "CTS_ERR",
                    response.Errors.FirstOrDefault()?.ErrorText ?? "CTS Submission Error");
            }
            else
            {
                animal.MarkAsProcessing();
            }
        }
    }
}
