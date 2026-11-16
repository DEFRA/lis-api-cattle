// <copyright file="CtsBundleClaimConcurrencyTests.cs" company="Defra">
// Copyright (c) Defra. All rights reserved.
// </copyright>

namespace Defra.Lis.Api.Tests.Integration;

using System.Collections.Concurrent;
using Defra.Lis.Api.Interfaces;
using Defra.Lis.Api.Models;
using Defra.Lis.Api.Models.Responses;
using Defra.Lis.Api.Services;
using Defra.Lis.Database.Claims;
using Defra.Lis.Database.Configurations;
using Defra.Lis.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Npgsql;

/// <summary>
/// Two instances of the service polling one database: each submission must reach CTS exactly once.
/// </summary>
[Trait("Category", "Integration")]
public sealed class CtsBundleClaimConcurrencyTests(PostgresFixture postgres) : IClassFixture<PostgresFixture>
{
    private const int Bundles = 20;
    private const int AnimalsPerBundle = 2;

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
    public async Task TwoInstances_CheckEachProcessingBundleExactlyOnce()
    {
        var seeded = await SeedAsync(Bundles, Statuses.Processing);
        var cts = new CountingCtsService();

        // Each instance runs its polling job several times, a new scope (DbContext) per run, as Quartz does.
        await Task.WhenAll(RunInstanceAsync(cts), RunInstanceAsync(cts));

        Assert.Equal(Bundles * AnimalsPerBundle, cts.Checks.Count);
        Assert.All(cts.Checks.Values, count => Assert.Equal(1, count));

        await using var db = postgres.CreateDbContext();
        var statuses = await db.Set<Submission>().Where(s => seeded.Contains(s.Id)).Select(s => s.Status).ToListAsync(TestContext.Current.CancellationToken);
        Assert.All(statuses, status => Assert.Equal(Statuses.Complete, status));
    }

    [Fact]
    public async Task PollingJob_LeavesSubmittedAndErroredBundlesToTheRegistrationBundleWorker()
    {
        // LREG-925: the queue worker sends "submitted" bundles and marks CTS failures "error"; the job must touch neither.
        var submitted = await SeedAsync(2, Statuses.Submitted, truncate: true);
        var errored = await SeedAsync(2, Statuses.Error, truncate: false);
        var cts = new CountingCtsService();

        await RunInstanceAsync(cts);

        Assert.Empty(cts.Checks);
        await using var db = postgres.CreateDbContext();
        Assert.All(
            await db.Set<Submission>().Where(s => submitted.Contains(s.Id)).Select(s => s.Status).ToListAsync(TestContext.Current.CancellationToken),
            status => Assert.Equal(Statuses.Submitted, status));
        Assert.All(
            await db.Set<Submission>().Where(s => errored.Contains(s.Id)).Select(s => s.Status).ToListAsync(TestContext.Current.CancellationToken),
            status => Assert.Equal(Statuses.Error, status));
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
    }

    private async Task RunInstanceAsync(ICtsService cts)
    {
        for (var run = 0; run < 8; run++)
        {
            await using var db = postgres.CreateRetryingDbContext();
            var processor = new CtsBundleProcessorService(
                db,
                claimer,
                cts,
                Options.Create(new CtsPollingJobOptions { BatchSize = 3 }),
                NullLogger<CtsBundleProcessorService>.Instance);
            await processor.ProcessPendingBundlesAsync(TestContext.Current.CancellationToken);
        }
    }

    private async Task<List<Guid>> SeedAsync(int count, string status = Statuses.Submitted, bool truncate = true)
    {
        await using var db = postgres.CreateDbContext();
        if (truncate)
        {
            await db.Database.ExecuteSqlRawAsync("TRUNCATE public.submissions CASCADE", TestContext.Current.CancellationToken);
        }

        var submissions = Enumerable.Range(0, count).Select(i =>
        {
            var submission = new Submission($"REF-{Guid.NewGuid():N}", "10/100/1000", "integration-test", status);
            for (var a = 0; a < AnimalsPerBundle; a++)
            {
                submission.AddAnimal($"UK{i:D6}{a:D6}", status);
            }

            return submission;
        }).ToList();

        db.Set<Submission>().AddRange(submissions);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        return submissions.OrderBy(s => s.CreatedAt).Select(s => s.Id).ToList();
    }

    /// <summary>
    /// Counts submissions per animal, slowly enough that the two instances overlap.
    /// </summary>
    private sealed class CountingCtsService : ICtsService
    {
        public ConcurrentDictionary<Guid, int> Checks { get; } = new();

        public async Task<CtsAnimalStatusResponse> CheckAnimalStatusAsync(string earTag, Guid animalId, CancellationToken cancellationToken = default)
        {
            Checks.AddOrUpdate(animalId, 1, (_, count) => count + 1);
            await Task.Delay(20, cancellationToken);
            return new CtsAnimalStatusResponse { EarTag = earTag, Status = "clean" };
        }
    }
}
