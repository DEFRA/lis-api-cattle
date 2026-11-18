// <copyright file="CtsBundleClaimConcurrencyTests.cs" company="Defra">
// Copyright (c) Defra. All rights reserved.
// </copyright>

namespace Defra.Lis.Api.Tests.Integration;

using System.Collections.Concurrent;
using Defra.Lis.Api.Cts;
using Defra.Lis.Api.Models;
using Defra.Lis.Api.Services;
using Defra.Lis.Api.Tests.Cts;
using Defra.Lis.Database.Claims;
using Defra.Lis.Database.Configurations;
using Defra.Lis.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Npgsql;

/// <summary>
/// Two instances of the service polling one database: each sent bundle must be polled by only one of them per cycle.
/// </summary>
[Trait("Category", "Integration")]
public sealed class CtsBundleClaimConcurrencyTests(PostgresFixture postgres) : IClassFixture<PostgresFixture>
{
    private const int Bundles = 20;
    private const int AnimalsPerBundle = 2;

    private static readonly System.Runtime.CompilerServices.StrongBox<long> NextReceipt = new(5000);

    private readonly PostgresSubmissionBatchClaimer claimer = new();

    [Fact]
    public async Task ConcurrentClaims_TakeSeparateRows()
    {
        var seeded = await SeedAsync(Bundles);

        await using var first = postgres.CreateDbContext();
        await using var second = postgres.CreateDbContext();
        await using var firstClaim = await claimer.ClaimAsync(first, 5, [Statuses.Submitted], TestContext.Current.CancellationToken);
        await using var secondClaim = await claimer.ClaimAsync(second, 5, [Statuses.Submitted], TestContext.Current.CancellationToken);

        Assert.Equal(5, firstClaim.SubmissionIds.Count);
        Assert.Equal(5, secondClaim.SubmissionIds.Count);
        Assert.Empty(firstClaim.SubmissionIds.Intersect(secondClaim.SubmissionIds));
        Assert.Equal(seeded.Take(5), firstClaim.SubmissionIds);
    }

    [Fact]
    public async Task ConcurrentClaimsAwaitingCtsResults_TakeSeparateRows()
    {
        await SeedAsync(Bundles);

        await using var first = postgres.CreateDbContext();
        await using var second = postgres.CreateDbContext();
        await using var firstClaim = await claimer.ClaimAwaitingCtsResultsAsync(first, 5, TestContext.Current.CancellationToken);
        await using var secondClaim = await claimer.ClaimAwaitingCtsResultsAsync(second, 5, TestContext.Current.CancellationToken);

        Assert.Equal(5, firstClaim.SubmissionIds.Count);
        Assert.Equal(5, secondClaim.SubmissionIds.Count);
        Assert.Empty(firstClaim.SubmissionIds.Intersect(secondClaim.SubmissionIds));
    }

    [Fact]
    public async Task TwoInstances_PollEachSentBundleOnceAndCompleteIt()
    {
        var seeded = await SeedAsync(Bundles);
        var cts = new CountingCtsWebClient();

        // Each instance runs its polling job several times, a new scope (DbContext) per run, as Quartz does.
        await Task.WhenAll(RunInstanceAsync(cts), RunInstanceAsync(cts));

        Assert.Equal(Bundles, cts.Polls.Count);
        Assert.All(cts.Polls.Values, count => Assert.Equal(1, count));

        await using var db = postgres.CreateDbContext();
        var bundles = await db.Set<Submission>().Include(s => s.Animals).Where(s => seeded.Contains(s.Id)).ToListAsync(TestContext.Current.CancellationToken);
        Assert.All(bundles, b => Assert.Equal(Statuses.Complete, b.Status));
        Assert.All(bundles, b => Assert.NotNull(b.CompletedAt));
        Assert.All(bundles.SelectMany(b => b.Animals), a => Assert.Equal(Statuses.Complete, a.Status));
    }

    [Fact]
    public async Task ClaimAwaitingCtsResults_SkipsBundlesWithoutAReceiptCompletedOrErrored()
    {
        var waiting = await SeedAsync(1);
        var unsent = await SeedAsync(1, sent: false, truncate: false);
        var errored = await SeedAsync(1, status: Statuses.Error, truncate: false);
        var completed = await SeedAsync(1, truncate: false);
        await using (var db = postgres.CreateDbContext())
        {
            await db.Database.ExecuteSqlAsync(
                $"UPDATE public.submissions SET status = 'complete', completed_at = now() WHERE id = {completed[0]}",
                TestContext.Current.CancellationToken);
        }

        await using var context = postgres.CreateDbContext();
        await using var claim = await claimer.ClaimAwaitingCtsResultsAsync(context, 10, TestContext.Current.CancellationToken);

        Assert.Equal(waiting, claim.SubmissionIds);
        Assert.DoesNotContain(unsent[0], claim.SubmissionIds);
        Assert.DoesNotContain(errored[0], claim.SubmissionIds);
    }

    [Fact]
    public async Task ClaimAwaitingCtsResults_TakesNeverPolledBundlesFirstThenThoseWaitingLongest()
    {
        var seeded = await SeedAsync(3);
        await using (var db = postgres.CreateDbContext())
        {
            await db.Database.ExecuteSqlAsync(
                $"UPDATE public.submissions SET cts_last_polled_at = now() WHERE id = {seeded[0]}",
                TestContext.Current.CancellationToken);
            await db.Database.ExecuteSqlAsync(
                $"UPDATE public.submissions SET cts_last_polled_at = now() - interval '1 minute' WHERE id = {seeded[1]}",
                TestContext.Current.CancellationToken);
        }

        await using var context = postgres.CreateDbContext();
        await using var claim = await claimer.ClaimAwaitingCtsResultsAsync(context, 3, TestContext.Current.CancellationToken);

        Assert.Equal([seeded[2], seeded[1], seeded[0]], claim.SubmissionIds);
    }

    [Fact]
    public async Task TheStatusCheck_AcceptsCompleteWithErrors()
    {
        var seeded = await SeedAsync(1);

        await using var db = postgres.CreateDbContext();
        var updated = await db.Database.ExecuteSqlAsync(
            $"UPDATE public.submissions SET status = 'complete_with_errors' WHERE id = {seeded[0]}",
            TestContext.Current.CancellationToken);

        Assert.Equal(1, updated);
    }

    [Fact]
    public async Task SavingOverAnotherWritersChange_RollsTheBatchBack()
    {
        var seeded = await SeedAsync(1);

        await using var db = postgres.CreateDbContext();
        await using (var claim = await claimer.ClaimAsync(db, 1, [Statuses.Submitted], TestContext.Current.CancellationToken))
        {
            var bundle = await db.Set<Submission>().SingleAsync(s => s.Id == claim.SubmissionIds[0], TestContext.Current.CancellationToken);
            bundle.MarkAsProcessing();

            // Simulate a row that changed between the read and the save: the xmin it was read with is stale.
            db.Entry(bundle).Property(SubmissionConfiguration.RowVersion).OriginalValue = 0u;

            await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => db.SaveChangesAsync(TestContext.Current.CancellationToken));
        }

        await using var check = postgres.CreateDbContext();
        Assert.Equal(Statuses.Submitted, (await check.Set<Submission>().SingleAsync(s => s.Id == seeded[0], TestContext.Current.CancellationToken)).Status);
    }

    [Fact]
    public async Task ClaimAsync_ReleasesItsTransaction_WhenTheClaimQueryFails()
    {
        await using var db = postgres.CreateDbContextWithoutSchema();

        await Assert.ThrowsAsync<PostgresException>(() =>
            claimer.ClaimAsync(db, 1, [Statuses.Submitted], TestContext.Current.CancellationToken));
        Assert.Null(db.Database.CurrentTransaction);

        await Assert.ThrowsAsync<PostgresException>(() =>
            claimer.ClaimAwaitingCtsResultsAsync(db, 1, TestContext.Current.CancellationToken));
        Assert.Null(db.Database.CurrentTransaction);
    }

    private async Task RunInstanceAsync(ICtsWebClient cts)
    {
        for (var run = 0; run < 8; run++)
        {
            await using var db = postgres.CreateRetryingDbContext();
            var processor = new CtsBundleProcessorService(
                db,
                claimer,
                cts,
                TimeProvider.System,
                Options.Create(new CtsPollingJobOptions { BatchSize = 3 }),
                NullLogger<CtsBundleProcessorService>.Instance);
            await processor.ProcessPendingBundlesAsync(TestContext.Current.CancellationToken);
        }
    }

    private async Task<List<Guid>> SeedAsync(int count, string status = Statuses.Submitted, bool truncate = true, bool sent = true)
    {
        await using var db = postgres.CreateDbContext();
        if (truncate)
        {
            await db.Database.ExecuteSqlRawAsync("TRUNCATE public.submissions CASCADE", TestContext.Current.CancellationToken);
        }

        var submissions = Enumerable.Range(0, count).Select(_ =>
        {
            var submission = CtsBirthRequestBuilderTests.Bundle(AnimalsPerBundle);
            if (sent)
            {
                submission.RecordCtsRequest(CtsBirthRequestBuilder.TxnIdFor(submission), CtsBirthRequestBuilder.Operation, CtsBirthRequestBuilder.Build(submission, DateTimeOffset.UtcNow));
                submission.MarkAsSentToCts(Interlocked.Increment(ref NextReceipt.Value), DateTimeOffset.UtcNow);
            }

            submission.UpdateStatus(status);
            return submission;
        }).ToList();

        db.Set<Submission>().AddRange(submissions);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        return submissions.OrderBy(s => s.CreatedAt).Select(s => s.Id).ToList();
    }

    /// <summary>
    /// Counts polls per receipt, slowly enough that the two instances overlap, and reports every row accepted.
    /// </summary>
    private sealed class CountingCtsWebClient : ICtsWebClient
    {
        public ConcurrentDictionary<long, int> Polls { get; } = new();

        public Task<CtsSubmitResult> SubmitAsync(string operation, string payload, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException("The polling job does not send bundles.");

        public async Task<CtsBirthResults> GetBirthResultsAsync(long receiptNumber, CancellationToken cancellationToken = default)
        {
            Polls.AddOrUpdate(receiptNumber, 1, (_, count) => count + 1);
            await Task.Delay(20, cancellationToken);
            return CtsBirthResults.Ready(null, Enumerable.Range(1, AnimalsPerBundle).ToList(), []);
        }
    }
}
