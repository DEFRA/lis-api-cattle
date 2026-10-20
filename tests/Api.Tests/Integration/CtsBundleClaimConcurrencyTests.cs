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
    public async Task TwoInstances_SendEachSubmissionToCtsExactlyOnce()
    {
        var seeded = await SeedAsync(Bundles);
        var cts = new CountingCtsService();

        // Each instance runs its polling job several times, a new scope (DbContext) per run, as Quartz does.
        await Task.WhenAll(RunInstanceAsync(cts), RunInstanceAsync(cts));

        Assert.Equal(Bundles * AnimalsPerBundle, cts.Submissions.Count);
        Assert.All(cts.Submissions.Values, count => Assert.Equal(1, count));

        await using var db = postgres.CreateDbContext();
        var statuses = await db.Set<Submission>().Where(s => seeded.Contains(s.Id)).Select(s => s.Status).ToListAsync(TestContext.Current.CancellationToken);
        Assert.All(statuses, status => Assert.Equal(Statuses.Complete, status));
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

    private async Task RunInstanceAsync(ICtsService cts)
    {
        for (var run = 0; run < 8; run++)
        {
            await using var db = postgres.CreateDbContext();
            var processor = new CtsBundleProcessorService(
                db,
                claimer,
                cts,
                Options.Create(new CtsPollingJobOptions { BatchSize = 3 }),
                NullLogger<CtsBundleProcessorService>.Instance);
            await processor.ProcessPendingBundlesAsync(TestContext.Current.CancellationToken);
        }
    }

    private async Task<List<Guid>> SeedAsync(int count)
    {
        await using var db = postgres.CreateDbContext();
        await db.Database.ExecuteSqlRawAsync("TRUNCATE public.submissions CASCADE", TestContext.Current.CancellationToken);

        var submissions = Enumerable.Range(0, count).Select(i =>
        {
            var submission = new Submission($"REF-{Guid.NewGuid():N}", "10/100/1000", "integration-test");
            for (var a = 0; a < AnimalsPerBundle; a++)
            {
                submission.AddAnimal($"UK{i:D6}{a:D6}");
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
        public ConcurrentDictionary<Guid, int> Submissions { get; } = new();

        public async Task<CtsAnimalStatusResponse> SubmitAnimalRegistrationAsync(SubmissionAnimal animal, CancellationToken cancellationToken = default)
        {
            Submissions.AddOrUpdate(animal.Id, 1, (_, count) => count + 1);
            await Task.Delay(20, cancellationToken);
            return new CtsAnimalStatusResponse { EarTag = animal.EarTag, Status = Statuses.Processing };
        }

        public Task<CtsAnimalStatusResponse> CheckAnimalStatusAsync(string earTag, Guid animalId, CancellationToken cancellationToken = default) =>
            Task.FromResult(new CtsAnimalStatusResponse { EarTag = earTag, Status = "clean" });
    }
}
