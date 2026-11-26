// <copyright file="CtsBundleProcessorService.cs" company="Defra">
// Copyright (c) Defra. All rights reserved.
// </copyright>

namespace Defra.Lis.Api.Services;

using System.Globalization;
using System.Xml.Linq;
using Defra.Database.Postgres;
using Defra.Lis.Api.Correlation;
using Defra.Lis.Api.Cts;
using Defra.Lis.Api.Interfaces;
using Defra.Lis.Api.Models;
using Defra.Lis.Api.Validation;
using Defra.Lis.Database.Claims;
using Defra.Lis.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Serilog.Context;

/// <summary>
/// Polls CTS for the validation results of bundles the registration bundle worker sent (LREG-926). Each claimed
/// bundle gets one <c>Get_Register_Births_Validation_Results</c> call with its receipt. Every row CTS returns is
/// recorded on its animal; rows CTS has not returned yet are left for the next poll. A bundle completes once every
/// animal has its final outcome. The outcome is also recorded on each animal's LIS record (LREG-1003): it becomes
/// registered when CTS accepts it, or errored with the CTS errors when CTS rejects it.
/// </summary>
public partial class CtsBundleProcessorService(
    DbContext dbContext,
    ISubmissionBatchClaimer claimer,
    ICtsWebClient ctsWebClient,
    TimeProvider timeProvider,
    IOptions<CtsPollingJobOptions> options,
    ILogger<CtsBundleProcessorService> logger)
    : ICtsBundleProcessorService
{
    private readonly CtsPollingJobOptions options = options.Value;

    public CtsBundleProcessorService(
        PostgresDbContext dbContext,
        ISubmissionBatchClaimer claimer,
        ICtsWebClient ctsWebClient,
        TimeProvider timeProvider,
        IOptions<CtsPollingJobOptions> options,
        ILogger<CtsBundleProcessorService> logger)
        : this((DbContext)dbContext, claimer, ctsWebClient, timeProvider, options, logger)
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

    /// <summary>
    /// Reads the row number and ear tag of every Birth row in a stored request, so CTS results can be mapped back to
    /// the animals that were actually sent.
    /// </summary>
    /// <param name="payload">The stored Birth request XML.</param>
    /// <returns>The ear tag sent in each row, by row number.</returns>
    internal static IReadOnlyDictionary<int, string> RowsOf(string payload)
    {
        var rows = new Dictionary<int, string>();
        foreach (var birth in XElement.Parse(payload).Descendants(CtsBirthRequestBuilder.Namespace + "Birth"))
        {
            if (int.TryParse(birth.Attribute("RowNum")?.Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var rowNum)
                && birth.Attribute("Etg")?.Value is { Length: > 0 } earTag)
            {
                rows[rowNum] = earTag;
            }
        }

        return rows;
    }

    /// <summary>The errors CTS gave the animal, with the curated text the API's validation uses for each known code.</summary>
    private static IEnumerable<(string ErrorCode, string ErrorText)> CuratedErrors(SubmissionAnimal animal) =>
        animal.Errors.Select(e => (e.ErrorCode, ValidationRuleCodes.TryGetDescription(e.ErrorCode, out var curated) ? curated : e.ErrorText));

    private async Task ProcessBatchAsync(int batchSize, CancellationToken cancellationToken)
    {
        // A retried attempt must not save changes tracked by the attempt that failed.
        dbContext.ChangeTracker.Clear();

        // Claim the batch so that other instances of the service skip these rows until this one commits.
        await using var claim = await claimer.ClaimAwaitingCtsResultsAsync(dbContext, batchSize, cancellationToken);

        if (claim.SubmissionIds.Count == 0)
        {
            LogNoPendingBundlesFoundToProcess();
            return;
        }

        var claimedIds = claim.SubmissionIds;
        var bundles = await dbContext.Set<Submission>()
            .Include(s => s.CtsRequest)
            .Include(s => s.Animals)
                .ThenInclude(a => a.Errors)
            .Where(s => claimedIds.Contains(s.Id))
            .OrderBy(s => s.CreatedAt)
            .AsSplitQuery()
            .ToListAsync(cancellationToken);

        // The animal records the bundles created, by the submission animal that created each one.
        var animalRecords = await dbContext.Set<Animal>()
            .Include(a => a.Errors)
            .Where(a => claimedIds.Contains(a.SubmissionId))
            .AsSplitQuery()
            .ToDictionaryAsync(a => a.SubmissionAnimalId, cancellationToken);

        LogFoundCountBundlesToProcessWithCts(bundles.Count);

        foreach (var bundle in bundles)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                break;
            }

            CorrelationIdContext.Value = bundle.CorrelationId;
            using var correlationProperty = LogContext.PushProperty("CorrelationId", bundle.CorrelationId);
            try
            {
                await ProcessBundleAsync(bundle, animalRecords, cancellationToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // One bundle's failure must not stop the rest of the batch; this bundle is polled again next time.
                LogErrorProcessingBundleBundleid(bundle.Id, ex);
            }
            finally
            {
                CorrelationIdContext.Value = null;
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

    private async Task ProcessBundleAsync(Submission bundle, IReadOnlyDictionary<Guid, Animal> animalRecords, CancellationToken cancellationToken)
    {
        if (bundle.CtsReceiptNumber is not { } receiptNumber || bundle.CtsRequest is null)
        {
            LogBundleHasNoCtsRequest(bundle.Id);
            return;
        }

        var results = await ctsWebClient.GetBirthResultsAsync(receiptNumber, cancellationToken);
        var now = timeProvider.GetUtcNow();
        bundle.RecordCtsPoll(now);

        switch (results.State)
        {
            case CtsBirthResultsState.NotReady:
                LogCtsResultsNotReady(bundle.Id, receiptNumber);
                return;

            case CtsBirthResultsState.Failed:
                HandleFailure(bundle, receiptNumber, results, now);
                return;
        }

        if (!string.IsNullOrEmpty(results.TxnId) && results.TxnId != bundle.CtsRequest.TxnId)
        {
            LogCtsResultsForAnotherTransaction(bundle.Id, receiptNumber);
            return;
        }

        var updated = ApplyResults(bundle, results, animalRecords, now);

        if (bundle.TryCompleteFromCtsResults(now))
        {
            LogBundleCompleted(bundle.Id, bundle.Status, results.AcceptedRows.Count, results.RejectedRows.Count);
        }
        else
        {
            LogBundlePartiallyProcessed(bundle.Id, updated, bundle.Animals.Count(a => !a.HasFinalCtsOutcome));
        }
    }

    private void HandleFailure(Submission bundle, long receiptNumber, CtsBirthResults results, DateTimeOffset now)
    {
        var maxAge = TimeSpan.FromHours(Math.Max(0, options.UnknownReceiptMaxAgeHours));
        var sentAt = bundle.CtsSentAt ?? bundle.CreatedAt;

        if (results.ErrorCode == CtsBirthResults.ReceiptNotFoundExNum && now - sentAt >= maxAge)
        {
            // CTS still does not know the receipt long after it issued it, so the results can never be fetched.
            bundle.MarkAsCtsResultsFailed(results.ErrorCode, results.ErrorText ?? "CTS does not recognise the receipt.", now);
            LogBundleReceiptUnknown(bundle.Id, receiptNumber);
            return;
        }

        // CTS is unavailable or answered with an error: leave the bundle as it is for the next poll.
        LogCtsResultsFailed(bundle.Id, receiptNumber, results.ErrorCode, results.ErrorText, results.IsTransient);
    }

    private int ApplyResults(Submission bundle, CtsBirthResults results, IReadOnlyDictionary<Guid, Animal> animalRecords, DateTimeOffset now)
    {
        var sentRows = RowsOf(bundle.CtsRequest!.Payload);
        var animalsByEarTag = bundle.Animals
            .GroupBy(a => CtsFormat.EarTag(a.EarTag) ?? string.Empty, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.ToList(), StringComparer.Ordinal);

        var updated = 0;
        foreach (var rowNum in results.AcceptedRows)
        {
            if (AnimalFor(bundle, rowNum, null) is { } animal && animal.RecordCtsAccepted())
            {
                updated++;
                animalRecords.GetValueOrDefault(animal.Id)?.RecordCtsAccepted(now);
            }
        }

        foreach (var rejected in results.RejectedRows)
        {
            if (AnimalFor(bundle, rejected.RowNum, rejected.EarTag) is { } animal
                && animal.RecordCtsRejected(rejected.Causes.Select(c => (c.Code, c.Description))))
            {
                updated++;

                // The animal record holds the same CTWS codes, with the text the API's validation uses for them.
                animalRecords.GetValueOrDefault(animal.Id)?.RecordCtsRejected(CuratedErrors(animal), now);
            }
        }

        return updated;

        SubmissionAnimal? AnimalFor(Submission submission, int rowNum, string? echoedEarTag)
        {
            if (!sentRows.TryGetValue(rowNum, out var earTag)
                || (echoedEarTag != null && !string.Equals(echoedEarTag, earTag, StringComparison.Ordinal))
                || !animalsByEarTag.TryGetValue(earTag, out var matches)
                || matches.Count != 1)
            {
                // Leave the animal as it is rather than record an outcome against the wrong one.
                LogCtsRowNotMatched(submission.Id, rowNum);
                return null;
            }

            return matches[0];
        }
    }
}
