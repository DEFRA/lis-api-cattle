// <copyright file="PostgresSubmissionLockerTests.cs" company="Defra">
// Copyright (c) Defra. All rights reserved.
// </copyright>

namespace Defra.Lis.Api.Tests.Integration;

using Defra.Lis.Database.Claims;
using Defra.Lis.Entities;
using Npgsql;

[Trait("Category", "Integration")]
public sealed class PostgresSubmissionLockerTests(PostgresFixture postgres) : IClassFixture<PostgresFixture>
{
    private readonly PostgresSubmissionLocker locker = new();

    [Fact]
    public async Task LockAsync_FindsAStoredSubmissionAndCommits()
    {
        await using var db = postgres.CreateDbContext();
        var submission = new Submission(Guid.NewGuid(), "REF", "10/081/1234", "user");
        db.Add(submission);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        await using var submissionLock = await locker.LockAsync(db, submission.Id, TestContext.Current.CancellationToken);
        Assert.True(submissionLock.Found);
        await submissionLock.CommitAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task LockAsync_ReportsAMissingSubmission()
    {
        await using var db = postgres.CreateDbContext();

        await using var submissionLock = await locker.LockAsync(db, Guid.NewGuid(), TestContext.Current.CancellationToken);

        Assert.False(submissionLock.Found);
    }

    [Fact]
    public async Task LockAsync_ReleasesItsTransaction_WhenTheLockQueryFails()
    {
        await using var db = postgres.CreateDbContextWithoutSchema();

        await Assert.ThrowsAsync<PostgresException>(() =>
            locker.LockAsync(db, Guid.NewGuid(), TestContext.Current.CancellationToken));

        Assert.Null(db.Database.CurrentTransaction);
    }
}
