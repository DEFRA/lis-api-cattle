// <copyright file="SubmissionChildInsertTests.cs" company="Defra">
// Copyright (c) Defra. All rights reserved.
// </copyright>

namespace Defra.Lis.Api.Tests.Integration;

using Defra.Lis.Entities;
using Microsoft.EntityFrameworkCore;

/// <summary>
/// The entities create their own IDs, so a child added to a bundle that is already stored must be inserted, not
/// saved as an update to a row that does not exist (which fails as a concurrency conflict).
/// </summary>
[Trait("Category", "Integration")]
public sealed class SubmissionChildInsertTests(PostgresFixture postgres) : IClassFixture<PostgresFixture>
{
    [Fact]
    public async Task AnAnimalAddedToAStoredBundle_IsInserted()
    {
        var id = await StoreBundleAsync();

        await using (var db = postgres.CreateDbContext())
        {
            var bundle = await LoadAsync(db, id);
            bundle.AddAnimal("UK123456100002");
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        await using var check = postgres.CreateDbContext();
        Assert.Equal(2, (await LoadAsync(check, id)).Animals.Count);
    }

    [Fact]
    public async Task ACtsRequestRecordedAgainstAStoredBundle_IsInserted()
    {
        var id = await StoreBundleAsync();

        await using (var db = postgres.CreateDbContext())
        {
            var bundle = await LoadAsync(db, id);
            bundle.RecordCtsRequest(id.ToString(), "Register_Births_Asynchronous-V1-0", "<RegBirths/>");
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        await using var check = postgres.CreateDbContext();
        var stored = await LoadAsync(check, id);
        Assert.NotNull(stored.CtsRequest);
        Assert.Equal("<RegBirths/>", stored.CtsRequest.Payload);
    }

    [Fact]
    public async Task AnErrorAddedToAStoredAnimal_IsInserted()
    {
        var id = await StoreBundleAsync();

        await using (var db = postgres.CreateDbContext())
        {
            var bundle = await LoadAsync(db, id);
            bundle.Animals.Single().RecordCtsRejected([("CTWS014", "Invalid Breed")]);
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        await using var check = postgres.CreateDbContext();
        var animal = (await LoadAsync(check, id)).Animals.Single();
        Assert.Equal(Statuses.Error, animal.Status);
        Assert.Equal("CTWS014", Assert.Single(animal.Errors).ErrorCode);
    }

    private static Task<Submission> LoadAsync(DbContext db, Guid id) =>
        db.Set<Submission>()
            .Include(s => s.CtsRequest)
            .Include(s => s.Animals).ThenInclude(a => a.Errors)
            .AsSplitQuery()
            .SingleAsync(s => s.Id == id, TestContext.Current.CancellationToken);

    private async Task<Guid> StoreBundleAsync()
    {
        var bundle = new Submission(Guid.NewGuid(), "REF-KEYS", "10/081/1234", "integration-test");
        bundle.AddAnimal("UK123456100001");

        await using var db = postgres.CreateDbContext();
        db.Set<Submission>().Add(bundle);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        return bundle.Id;
    }
}
