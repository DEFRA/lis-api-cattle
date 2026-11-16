// <copyright file="CtsBundleProcessorService.cs" company="Defra">
// Copyright (c) Defra. All rights reserved.
// </copyright>

namespace Defra.Lis.Api.Services;

using Defra.Database.Postgres;
using Defra.Lis.Api.Interfaces;
using Defra.Lis.Api.Models;
using Defra.Lis.Database.Claims;
using Defra.Lis.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

public partial class CtsBundleProcessorService(
    DbContext dbContext,
    ISubmissionBatchClaimer claimer,
    ICtsService ctsService,
    IOptions<CtsPollingJobOptions> options,
    ILogger<CtsBundleProcessorService> logger)
    : ICtsBundleProcessorService
{
    // Bundles are sent to CTS by the registration bundle queue worker (LREG-925), which leaves them "submitted" with a
    // receipt, or "error" with the CTS error. This job only checks bundles that are already being processed; it must
    // not touch "submitted" bundles (that would send them a second time) or "error" bundles (that would overwrite
    // the CTS error). Polling CTS with the receipt replaces it in part two.
    private static readonly string[] TargetStatuses = [Statuses.Processing];

    private readonly CtsPollingJobOptions options = options.Value;

    public CtsBundleProcessorService(
        PostgresDbContext dbContext,
        ISubmissionBatchClaimer claimer,
        ICtsService ctsService,
        IOptions<CtsPollingJobOptions> options,
        ILogger<CtsBundleProcessorService> logger)
        : this((DbContext)dbContext, claimer, ctsService, options, logger)
    {
    }

    public async Task ProcessPendingBundlesAsync(CancellationToken cancellationToken = default)
    {
        var batchSize = options.BatchSize > 0 ? options.BatchSize : 10;

        // A retrying execution strategy rejects a transaction it does not own, so the claim and everything
        // done under it run as one retriable unit; a transient failure replays the whole batch.
        var strategy = dbContext.Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(ct => ProcessBatchAsync(batchSize, ct), cancellationToken);
    }

    private async Task ProcessBatchAsync(int batchSize, CancellationToken cancellationToken)
    {
        // A retried attempt must not save changes tracked by the attempt that failed.
        dbContext.ChangeTracker.Clear();

        // Claim the batch so that other instances of the service skip these rows until this one commits.
        await using var claim = await claimer.ClaimAsync(dbContext, batchSize, TargetStatuses, cancellationToken);

        if (claim.SubmissionIds.Count == 0)
        {
            LogNoPendingBundlesFoundToProcess();
            return;
        }

        var claimedIds = claim.SubmissionIds;
        var bundles = await dbContext.Set<Submission>()
            .Include(s => s.Animals)
                .ThenInclude(a => a.Errors)
            .Where(s => claimedIds.Contains(s.Id))
            .OrderBy(s => s.CreatedAt)
            .ToListAsync(cancellationToken);

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

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
            await claim.CommitAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException ex)
        {
            // Another writer changed a claimed submission; disposing the claim rolls this batch back for the next run.
            var conflicts = ex.Entries.Select(e => e.Entity).OfType<Submission>().Select(s => s.Id);
            LogBatchConflictedWithAnotherWriter(string.Join(',', conflicts));
            dbContext.ChangeTracker.Clear();
        }
    }

    private async Task ProcessBundleAsync(Submission bundle, CancellationToken cancellationToken)
    {
        var targetAnimals = bundle.Animals
            .Where(a => a.Status == Statuses.Processing || a.Status == Statuses.Error || a.Status == Statuses.Submitted || a.Status == Statuses.Pending)
            .ToList();
        await CheckAnimalStatusAndUpdate(targetAnimals, cancellationToken);

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
}
