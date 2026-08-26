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

public class CtsBundleProcessorService(
    ICtsService ctsService,
    DbContext dbContext,
    IOptions<CtsPollingJobOptions>? options = null,
    ILogger<CtsBundleProcessorService>? logger = null)
    : ICtsBundleProcessorService
{
    private readonly ICtsService ctsService = ctsService ?? throw new ArgumentNullException(nameof(ctsService));
    private readonly DbContext dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
    private readonly CtsPollingJobOptions options = options?.Value ?? new CtsPollingJobOptions();

    public CtsBundleProcessorService(
        ICtsService ctsService,
        PostgresDbContext dbContext,
        IOptions<CtsPollingJobOptions>? options = null,
        ILogger<CtsBundleProcessorService>? logger = null)
        : this(ctsService, (DbContext)dbContext, options, logger)
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
            logger?.LogDebug("No pending bundles found to process.");
            return;
        }

        logger?.LogInformation("Found {Count} bundles to process with CTS.", bundles.Count);

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
                logger?.LogError(ex, "Error processing bundle {BundleId}", bundle.Id);
            }
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task ProcessBundleAsync(Submission bundle, CancellationToken cancellationToken)
    {
        if (bundle.Status == Statuses.Submitted || bundle.Status == Statuses.Pending)
        {
            bundle.MarkAsProcessing();

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
        else if (bundle.Status == Statuses.Processing || bundle.Status == Statuses.Error)
        {
            var targetAnimals = bundle.Animals
                .Where(a => a.Status == Statuses.Processing || a.Status == Statuses.Error || a.Status == Statuses.Submitted || a.Status == Statuses.Pending)
                .ToList();

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

        bundle.RefreshStatusFromAnimals();
    }
}
