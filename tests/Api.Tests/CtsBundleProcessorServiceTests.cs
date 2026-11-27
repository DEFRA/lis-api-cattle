// <copyright file="CtsBundleProcessorServiceTests.cs" company="Defra">
// Copyright (c) Defra. All rights reserved.
// </copyright>

namespace Defra.Lis.Api.Tests;

using System.Globalization;
using Defra.Lis.Api.Correlation;
using Defra.Lis.Api.Cts;
using Defra.Lis.Api.Models;
using Defra.Lis.Api.Services;
using Defra.Lis.Api.Tests.Cts;
using Defra.Lis.Database.Claims;
using Defra.Lis.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;

public sealed class CtsBundleProcessorServiceTests : IDisposable
{
    private static readonly DateTimeOffset SentAt = DateTimeOffset.Parse("2026-11-18T09:00:00Z", CultureInfo.InvariantCulture);

    private readonly Mock<ICtsWebClient> cts = new();
    private readonly MutableTimeProvider clock = new(SentAt.AddMinutes(5));
    private readonly TestDbContext context;
    private long nextReceipt = 1000;

    public CtsBundleProcessorServiceTests()
    {
        var options = new DbContextOptionsBuilder<TestDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        context = new TestDbContext(options);
    }

    [Fact]
    public async Task ProcessPendingBundlesAsync_WhenCtsAcceptsEveryAnimal_CompletesTheBundle()
    {
        var bundle = await SeedSentBundleAsync(2);
        Respond(bundle, Ready(bundle, accepted: [1, 2]));

        await CreateService().ProcessPendingBundlesAsync(TestContext.Current.CancellationToken);

        var updated = await ReloadAsync(bundle.Id);
        Assert.Equal(Statuses.Complete, updated.Status);
        Assert.Equal(clock.GetUtcNow(), updated.CompletedAt);
        Assert.Equal(clock.GetUtcNow(), updated.UpdatedAt);
        Assert.All(updated.Animals, a => Assert.Equal(Statuses.Complete, a.Status));
        Assert.All(updated.Animals, a => Assert.Empty(a.Errors));
        cts.Verify(c => c.GetBirthResultsAsync(bundle.CtsReceiptNumber!.Value, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ProcessPendingBundlesAsync_WhenCtsRejectsSomeAnimals_CompletesWithErrorsAndStoresEachCause()
    {
        var bundle = await SeedSentBundleAsync(2);
        Respond(bundle, Ready(bundle, accepted: [1], rejected: [Reject(bundle, 2, ("CTWS014", "Invalid Breed"), ("CTWS003", "Missing Ear Tag"))]));

        await CreateService().ProcessPendingBundlesAsync(TestContext.Current.CancellationToken);

        var updated = await ReloadAsync(bundle.Id);
        Assert.Equal(Statuses.CompleteWithErrors, updated.Status);
        Assert.NotNull(updated.CompletedAt);

        var rows = CtsBirthRequestBuilder.OrderedAnimals(updated);
        Assert.Equal(Statuses.Complete, rows[0].Status);
        Assert.Equal(Statuses.Error, rows[1].Status);
        Assert.Equal(["CTWS003", "CTWS014"], rows[1].Errors.Select(e => e.ErrorCode).Order(StringComparer.Ordinal));
        Assert.Contains(rows[1].Errors, e => e.ErrorText == "Invalid Breed");
    }

    [Fact]
    public async Task ProcessPendingBundlesAsync_WhenCtsReturnsSomeRows_LeavesTheRestForALaterPollThatCompletesTheBundle()
    {
        var bundle = await SeedSentBundleAsync(3);
        Respond(bundle, Ready(bundle, accepted: [1]));

        await CreateService().ProcessPendingBundlesAsync(TestContext.Current.CancellationToken);

        var afterFirst = await ReloadAsync(bundle.Id);
        Assert.Equal(Statuses.Submitted, afterFirst.Status);
        Assert.Null(afterFirst.CompletedAt);
        Assert.Equal(clock.GetUtcNow(), afterFirst.CtsLastPolledAt);
        Assert.Equal([Statuses.Complete, Statuses.Submitted, Statuses.Submitted], CtsBirthRequestBuilder.OrderedAnimals(afterFirst).Select(a => a.Status));

        clock.Advance(TimeSpan.FromSeconds(30));
        Respond(bundle, Ready(bundle, accepted: [1, 3], rejected: [Reject(bundle, 2, ("CTWS014", "Invalid Breed"))]));

        await CreateService().ProcessPendingBundlesAsync(TestContext.Current.CancellationToken);

        var afterSecond = await ReloadAsync(bundle.Id);
        Assert.Equal(Statuses.CompleteWithErrors, afterSecond.Status);
        Assert.Equal(clock.GetUtcNow(), afterSecond.CompletedAt);
        Assert.Equal([Statuses.Complete, Statuses.Error, Statuses.Complete], CtsBirthRequestBuilder.OrderedAnimals(afterSecond).Select(a => a.Status));
    }

    [Fact]
    public async Task ProcessPendingBundlesAsync_WhenTheSameResultsArePolledTwice_AddsNoDuplicateErrorsAndChangesNothing()
    {
        var bundle = await SeedSentBundleAsync(2);
        Respond(bundle, Ready(bundle, rejected: [Reject(bundle, 1, ("CTWS014", "Invalid Breed"))]));

        await CreateService().ProcessPendingBundlesAsync(TestContext.Current.CancellationToken);
        await CreateService().ProcessPendingBundlesAsync(TestContext.Current.CancellationToken);

        var updated = await ReloadAsync(bundle.Id);
        var rows = CtsBirthRequestBuilder.OrderedAnimals(updated);
        Assert.Equal(Statuses.Error, rows[0].Status);
        Assert.Single(rows[0].Errors);
        Assert.Equal(Statuses.Submitted, rows[1].Status);
        Assert.Equal(Statuses.Submitted, updated.Status);
        Assert.Single(await context.Set<SubmissionAnimalError>().ToListAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task ProcessPendingBundlesAsync_WhenAnAcceptedAnimalComesBackRejected_KeepsItsFirstOutcome()
    {
        var bundle = await SeedSentBundleAsync(2);
        Respond(bundle, Ready(bundle, accepted: [1]));
        await CreateService().ProcessPendingBundlesAsync(TestContext.Current.CancellationToken);

        Respond(bundle, Ready(bundle, rejected: [Reject(bundle, 1, ("CTWS014", "Invalid Breed"))]));
        await CreateService().ProcessPendingBundlesAsync(TestContext.Current.CancellationToken);

        var row = CtsBirthRequestBuilder.OrderedAnimals(await ReloadAsync(bundle.Id))[0];
        Assert.Equal(Statuses.Complete, row.Status);
        Assert.Empty(row.Errors);
    }

    [Fact]
    public async Task ProcessPendingBundlesAsync_WhenCtsIsStillValidating_LeavesTheBundleAndRecordsThePoll()
    {
        var bundle = await SeedSentBundleAsync(1);
        Respond(bundle, CtsBirthResults.NotReady("Results not yet available"));

        await CreateService().ProcessPendingBundlesAsync(TestContext.Current.CancellationToken);

        var updated = await ReloadAsync(bundle.Id);
        Assert.Equal(Statuses.Submitted, updated.Status);
        Assert.Null(updated.CompletedAt);
        Assert.Equal(clock.GetUtcNow(), updated.CtsLastPolledAt);
        Assert.All(updated.Animals, a => Assert.Equal(Statuses.Submitted, a.Status));
    }

    [Fact]
    public async Task ProcessPendingBundlesAsync_WhenCtsIsUnavailableForOneBundle_LeavesItAndProcessesTheRest()
    {
        var unavailable = await SeedSentBundleAsync(1);
        var available = await SeedSentBundleAsync(1);
        Respond(unavailable, CtsBirthResults.Failed(CtsSubmitResult.UnavailableCode, "CTS could not be reached.", true));
        Respond(available, Ready(available, accepted: [1]));

        await CreateService().ProcessPendingBundlesAsync(TestContext.Current.CancellationToken);

        var left = await ReloadAsync(unavailable.Id);
        Assert.Equal(Statuses.Submitted, left.Status);
        Assert.Null(left.ErrorCode);
        Assert.All(left.Animals, a => Assert.Equal(Statuses.Submitted, a.Status));
        Assert.Equal(Statuses.Complete, (await ReloadAsync(available.Id)).Status);
    }

    [Fact]
    public async Task ProcessPendingBundlesAsync_WhenTheCtsClientThrowsForOneBundle_ProcessesTheRest()
    {
        var failing = await SeedSentBundleAsync(1);
        var healthy = await SeedSentBundleAsync(1);
        cts.Setup(c => c.GetBirthResultsAsync(failing.CtsReceiptNumber!.Value, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Unexpected"));
        Respond(healthy, Ready(healthy, accepted: [1]));

        await CreateService().ProcessPendingBundlesAsync(TestContext.Current.CancellationToken);

        Assert.Equal(Statuses.Submitted, (await ReloadAsync(failing.Id)).Status);
        Assert.Equal(Statuses.Complete, (await ReloadAsync(healthy.Id)).Status);
    }

    [Fact]
    public async Task ProcessPendingBundlesAsync_WhenCtsDoesNotKnowARecentReceipt_KeepsPolling()
    {
        var bundle = await SeedSentBundleAsync(1);
        Respond(bundle, CtsBirthResults.Failed(CtsBirthResults.ReceiptNotFoundExNum, "Receipt not found", false));

        await CreateService(unknownReceiptMaxAgeHours: 24).ProcessPendingBundlesAsync(TestContext.Current.CancellationToken);

        var updated = await ReloadAsync(bundle.Id);
        Assert.Equal(Statuses.Submitted, updated.Status);
        Assert.Null(updated.ErrorCode);
    }

    [Fact]
    public async Task ProcessPendingBundlesAsync_WhenCtsStillDoesNotKnowTheReceiptAfterTheMaxAge_MarksTheBundleAsErrored()
    {
        var bundle = await SeedSentBundleAsync(1);
        Respond(bundle, CtsBirthResults.Failed(CtsBirthResults.ReceiptNotFoundExNum, "Receipt not found", false));
        clock.Set(SentAt.AddHours(24));

        await CreateService(unknownReceiptMaxAgeHours: 24).ProcessPendingBundlesAsync(TestContext.Current.CancellationToken);

        var updated = await ReloadAsync(bundle.Id);
        Assert.Equal(Statuses.Error, updated.Status);
        Assert.Equal(CtsBirthResults.ReceiptNotFoundExNum, updated.ErrorCode);
        Assert.Equal("Receipt not found", updated.ErrorText);
        Assert.Equal(bundle.CtsReceiptNumber, updated.CtsReceiptNumber);
        Assert.Null(updated.CompletedAt);

        // An errored bundle is no longer claimed.
        cts.Invocations.Clear();
        await CreateService().ProcessPendingBundlesAsync(TestContext.Current.CancellationToken);
        cts.Verify(c => c.GetBirthResultsAsync(It.IsAny<long>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ProcessPendingBundlesAsync_WhenTheResultsAreForAnotherTransaction_IgnoresThem()
    {
        var bundle = await SeedSentBundleAsync(1);
        Respond(bundle, CtsBirthResults.Ready("another-transaction", [1], []));

        await CreateService().ProcessPendingBundlesAsync(TestContext.Current.CancellationToken);

        var updated = await ReloadAsync(bundle.Id);
        Assert.Equal(Statuses.Submitted, updated.Status);
        Assert.All(updated.Animals, a => Assert.Equal(Statuses.Submitted, a.Status));
    }

    [Fact]
    public async Task ProcessPendingBundlesAsync_WhenARowDoesNotMatchTheStoredRequest_LeavesThatAnimal()
    {
        var bundle = await SeedSentBundleAsync(2);
        Respond(bundle, Ready(
            bundle,
            accepted: [7],
            rejected: [new CtsRejectedBirth(2, "UK999999999999", [new CtsCause("CTWS014", "Invalid Breed", "e", "Brd")]), Reject(bundle, 1, ("CTWS003", "Missing Ear Tag"))]));

        await CreateService().ProcessPendingBundlesAsync(TestContext.Current.CancellationToken);

        var rows = CtsBirthRequestBuilder.OrderedAnimals(await ReloadAsync(bundle.Id));
        Assert.Equal(Statuses.Error, rows[0].Status);
        Assert.Equal(Statuses.Submitted, rows[1].Status);
        Assert.Empty(rows[1].Errors);
    }

    [Fact]
    public async Task ProcessPendingBundlesAsync_CallsCtsWithTheBundlesCorrelationId()
    {
        var bundle = await SeedSentBundleAsync(1, correlationId: "corr-926");
        string? seen = null;
        cts.Setup(c => c.GetBirthResultsAsync(bundle.CtsReceiptNumber!.Value, It.IsAny<CancellationToken>()))
            .Callback(() => seen = CorrelationIdContext.Value)
            .ReturnsAsync(Ready(bundle, accepted: [1]));

        await CreateService().ProcessPendingBundlesAsync(TestContext.Current.CancellationToken);

        Assert.Equal("corr-926", seen);
        Assert.Null(CorrelationIdContext.Value);
    }

    [Fact]
    public async Task ProcessPendingBundlesAsync_DoesNotPollBundlesWithoutAReceiptOrAlreadyCompleted()
    {
        var unsent = new Submission(Guid.NewGuid(), "REF-UNSENT", "10/081/1234", "user");
        unsent.AddAnimal("UK123456100001");
        var completed = await SeedSentBundleAsync(1);
        Respond(completed, Ready(completed, accepted: [1]));
        await CreateService().ProcessPendingBundlesAsync(TestContext.Current.CancellationToken);
        context.Set<Submission>().Add(unsent);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        cts.Invocations.Clear();

        await CreateService().ProcessPendingBundlesAsync(TestContext.Current.CancellationToken);

        cts.Verify(c => c.GetBirthResultsAsync(It.IsAny<long>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ProcessPendingBundlesAsync_WhenNoBundlesAreWaiting_DoesNotCallCts()
    {
        var claimer = new InMemoryClaimer();

        await CreateService(claimer: claimer).ProcessPendingBundlesAsync(TestContext.Current.CancellationToken);

        cts.Verify(c => c.GetBirthResultsAsync(It.IsAny<long>(), It.IsAny<CancellationToken>()), Times.Never);
        Assert.False(claimer.Committed);
        Assert.True(claimer.Disposed);
    }

    [Fact]
    public async Task ProcessPendingBundlesAsync_OnlyPollsTheBundlesItClaimed()
    {
        var claimed = await SeedSentBundleAsync(1);
        var heldElsewhere = await SeedSentBundleAsync(1);
        Respond(claimed, Ready(claimed, accepted: [1]));

        var claimer = new InMemoryClaimer(claimed.Id);
        await CreateService(claimer: claimer).ProcessPendingBundlesAsync(TestContext.Current.CancellationToken);

        cts.Verify(c => c.GetBirthResultsAsync(claimed.CtsReceiptNumber!.Value, It.IsAny<CancellationToken>()), Times.Once);
        cts.Verify(c => c.GetBirthResultsAsync(heldElsewhere.CtsReceiptNumber!.Value, It.IsAny<CancellationToken>()), Times.Never);
        Assert.True(claimer.Committed);
        Assert.True(claimer.Disposed);
    }

    [Fact]
    public async Task ProcessPendingBundlesAsync_ClaimsTheConfiguredBatchSize()
    {
        var claimer = new InMemoryClaimer();

        await CreateService(batchSize: 25, claimer: claimer).ProcessPendingBundlesAsync(TestContext.Current.CancellationToken);

        Assert.Equal(25, claimer.RequestedBatchSize);
    }

    [Fact]
    public async Task ProcessPendingBundlesAsync_DoesNotCommit_WhenAnotherWriterChangedTheBatch()
    {
        var bundle = await SeedSentBundleAsync(1);
        Respond(bundle, Ready(bundle, accepted: [1]));

        var claimer = new InMemoryClaimer { CommitFailure = new DbUpdateConcurrencyException("Row version changed") };
        await CreateService(claimer: claimer).ProcessPendingBundlesAsync(TestContext.Current.CancellationToken);

        Assert.False(claimer.Committed);
        Assert.True(claimer.Disposed);
        Assert.Empty(context.ChangeTracker.Entries());
    }

    [Fact]
    public async Task ProcessPendingBundlesAsync_RecordsTheCtsOutcomeOnEachAnimalRecord()
    {
        var bundle = await SeedSentBundleAsync(2);
        await SeedAnimalRecordsAsync(bundle);
        Respond(bundle, Ready(bundle, accepted: [1], rejected: [Reject(bundle, 2, ("CTWS014", "Invalid Breed"), ("CTWS999", "Something CTS-only"))]));

        await CreateService().ProcessPendingBundlesAsync(TestContext.Current.CancellationToken);

        var rows = CtsBirthRequestBuilder.OrderedAnimals(await ReloadAsync(bundle.Id));
        var records = await ReloadAnimalRecordsAsync();
        var accepted = records[rows[0].Id];
        Assert.Equal(AnimalStatuses.Registered, accepted.Status);
        Assert.Equal(clock.GetUtcNow(), accepted.UpdatedAt);
        Assert.Empty(accepted.Errors);

        var rejected = records[rows[1].Id];
        Assert.Equal(AnimalStatuses.Error, rejected.Status);
        Assert.Equal(clock.GetUtcNow(), rejected.UpdatedAt);
        Assert.Equal(["CTWS014", "CTWS999"], rejected.Errors.Select(e => e.ErrorCode).Order(StringComparer.Ordinal));

        // A code the API's validation knows gets its curated text; any other keeps the text CTS sent.
        Assert.Equal("Invalid Breed Code", rejected.Errors.Single(e => e.ErrorCode == "CTWS014").ErrorText);
        Assert.Equal("Something CTS-only", rejected.Errors.Single(e => e.ErrorCode == "CTWS999").ErrorText);
        Assert.All(rejected.Errors, e => Assert.Equal(Animal.CtsErrorSource, e.Source));
        Assert.All(rejected.Errors, e => Assert.Equal(clock.GetUtcNow(), e.CreatedAt));
    }

    [Fact]
    public async Task ProcessPendingBundlesAsync_WhenTheSameResultsAreRecordedTwice_AddsNoDuplicateAnimalErrors()
    {
        var bundle = await SeedSentBundleAsync(2);
        await SeedAnimalRecordsAsync(bundle);
        Respond(bundle, Ready(bundle, accepted: [1]));
        await CreateService().ProcessPendingBundlesAsync(TestContext.Current.CancellationToken);

        // The rejection arrives on a later poll, then the same response is returned again.
        Respond(bundle, Ready(bundle, accepted: [1], rejected: [Reject(bundle, 2, ("CTWS014", "Invalid Breed"))]));
        await CreateService().ProcessPendingBundlesAsync(TestContext.Current.CancellationToken);
        Respond(bundle, Ready(bundle, accepted: [1], rejected: [Reject(bundle, 2, ("CTWS014", "Invalid Breed"))]));
        await CreateService().ProcessPendingBundlesAsync(TestContext.Current.CancellationToken);

        var rows = CtsBirthRequestBuilder.OrderedAnimals(await ReloadAsync(bundle.Id));
        var records = await ReloadAnimalRecordsAsync();
        Assert.Equal("CTWS014", Assert.Single(records[rows[1].Id].Errors).ErrorCode);
        Assert.Equal(1, await context.Set<AnimalError>().CountAsync(TestContext.Current.CancellationToken));
        Assert.Equal(AnimalStatuses.Registered, records[rows[0].Id].Status);
    }

    [Fact]
    public async Task ProcessPendingBundlesAsync_WhenAnAnimalHasNoRecord_StillRecordsItsCtsOutcome()
    {
        // An animal whose ear tag was already allocated got no record of its own.
        var bundle = await SeedSentBundleAsync(1);
        Respond(bundle, Ready(bundle, rejected: [Reject(bundle, 1, ("CTWS192", "Ear Tag has already been used"))]));

        await CreateService().ProcessPendingBundlesAsync(TestContext.Current.CancellationToken);

        var updated = await ReloadAsync(bundle.Id);
        Assert.Equal(Statuses.CompleteWithErrors, updated.Status);
        Assert.Empty(await context.Set<Animal>().ToListAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public void RowsOf_ReadsTheEarTagSentInEachRow()
    {
        var bundle = CtsBirthRequestBuilderTests.Bundle(2);

        var rows = CtsBundleProcessorService.RowsOf(CtsBirthRequestBuilder.Build(bundle, SentAt));

        Assert.Equal(new Dictionary<int, string> { [1] = "UK123456100001", [2] = "UK123456100002" }, rows);
    }

    public void Dispose()
    {
        context.Dispose();
    }

    private static CtsBirthResults Ready(Submission bundle, int[]? accepted = null, CtsRejectedBirth[]? rejected = null) =>
        CtsBirthResults.Ready(CtsBirthRequestBuilder.TxnIdFor(bundle), accepted ?? [], rejected ?? []);

    private static CtsRejectedBirth Reject(Submission bundle, int rowNum, params (string Code, string Desc)[] causes) =>
        new(
            rowNum,
            CtsFormat.EarTag(CtsBirthRequestBuilder.OrderedAnimals(bundle)[rowNum - 1].EarTag),
            causes.Select(c => new CtsCause(c.Code, c.Desc, "e", "Brd")).ToList());

    private void Respond(Submission bundle, CtsBirthResults results) =>
        cts.Setup(c => c.GetBirthResultsAsync(bundle.CtsReceiptNumber!.Value, It.IsAny<CancellationToken>()))
            .ReturnsAsync(results);

    private async Task<Submission> SeedSentBundleAsync(int animals, string? correlationId = null)
    {
        var bundle = CtsBirthRequestBuilderTests.Bundle(animals);
        bundle.RecordCorrelationId(correlationId);
        bundle.RecordCtsRequest(CtsBirthRequestBuilder.TxnIdFor(bundle), CtsBirthRequestBuilder.Operation, CtsBirthRequestBuilder.Build(bundle, SentAt));
        bundle.MarkAsSentToCts(nextReceipt++, SentAt);

        context.Set<Submission>().Add(bundle);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        context.ChangeTracker.Clear();
        return bundle;
    }

    private async Task SeedAnimalRecordsAsync(Submission bundle)
    {
        context.Set<Animal>().AddRange(bundle.Animals.Select(a => new Animal(a, CtsFormat.EarTag(a.EarTag)!)));
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        context.ChangeTracker.Clear();
    }

    private async Task<Dictionary<Guid, Animal>> ReloadAnimalRecordsAsync()
    {
        context.ChangeTracker.Clear();
        return await context.Set<Animal>()
            .Include(a => a.Errors)
            .ToDictionaryAsync(a => a.SubmissionAnimalId, TestContext.Current.CancellationToken);
    }

    private async Task<Submission> ReloadAsync(Guid id)
    {
        context.ChangeTracker.Clear();
        return await context.Set<Submission>()
            .Include(s => s.CtsRequest)
            .Include(s => s.Animals)
                .ThenInclude(a => a.Errors)
            .SingleAsync(s => s.Id == id, TestContext.Current.CancellationToken);
    }

    private CtsBundleProcessorService CreateService(int batchSize = 10, int unknownReceiptMaxAgeHours = 24, InMemoryClaimer? claimer = null) =>
        new(
            context,
            claimer ?? new InMemoryClaimer(),
            cts.Object,
            clock,
            Options.Create(new CtsPollingJobOptions { BatchSize = batchSize, UnknownReceiptMaxAgeHours = unknownReceiptMaxAgeHours }),
            NullLogger<CtsBundleProcessorService>.Instance);

    private sealed class MutableTimeProvider(DateTimeOffset now) : TimeProvider
    {
        private DateTimeOffset now = now;

        public override DateTimeOffset GetUtcNow() => now;

        public void Advance(TimeSpan by) => now += by;

        public void Set(DateTimeOffset to) => now = to;
    }

    private sealed class TestDbContext(DbContextOptions<TestDbContext> options)
        : DbContext(options)
    {
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Submission>(builder =>
            {
                builder.ToTable("submissions", "public");
                builder.HasKey(e => e.Id);
                builder.Property(e => e.Id).ValueGeneratedNever();
                builder.Ignore(e => e.HasCtsOutcome);
                builder.HasMany(e => e.Animals).WithOne(a => a.Submission).HasForeignKey(a => a.SubmissionId);
                builder.HasOne(e => e.CtsRequest).WithOne(r => r.Submission).HasForeignKey<CtsRequest>(r => r.SubmissionId);
            });

            modelBuilder.Entity<CtsRequest>(builder =>
            {
                builder.ToTable("cts_requests", "public");
                builder.HasKey(e => e.Id);
                builder.Property(e => e.Id).ValueGeneratedNever();
            });

            modelBuilder.Entity<SubmissionAnimal>(builder =>
            {
                builder.ToTable("submission_animals", "public");
                builder.HasKey(e => e.Id);
                builder.Property(e => e.Id).ValueGeneratedNever();
                builder.Ignore(e => e.HasFinalCtsOutcome);
                builder.HasMany(e => e.Errors).WithOne(a => a.Animal).HasForeignKey(a => a.AnimalId);
            });

            modelBuilder.Entity<SubmissionAnimalError>(builder =>
            {
                builder.ToTable("submission_animal_errors", "public");
                builder.HasKey(e => e.Id);
                builder.Property(e => e.Id).ValueGeneratedNever();
            });

            modelBuilder.Entity<Animal>(builder =>
            {
                builder.ToTable("animals", "public");
                builder.HasKey(e => e.Id);
                builder.Property(e => e.Id).ValueGeneratedNever();
                builder.HasMany(e => e.Errors).WithOne().HasForeignKey(e => e.AnimalId);
            });

            modelBuilder.Entity<AnimalError>(builder =>
            {
                builder.ToTable("animal_errors", "public");
                builder.HasKey(e => e.Id);
                builder.Property(e => e.Id).ValueGeneratedNever();
            });
        }
    }

    /// <summary>
    /// Stands in for the Postgres claimer, which needs FOR UPDATE SKIP LOCKED: claims the given submissions, or
    /// every submission waiting for CTS results when none are given.
    /// </summary>
    private sealed class InMemoryClaimer(params Guid[] onlyThese) : ISubmissionBatchClaimer, ISubmissionBatchClaim
    {
        public IReadOnlyList<Guid> SubmissionIds { get; private set; } = [];

        public int RequestedBatchSize { get; private set; }

        public bool Committed { get; private set; }

        public bool Disposed { get; private set; }

        public Exception? CommitFailure { get; init; }

        public Task<ISubmissionBatchClaim> ClaimAsync(DbContext dbContext, int batchSize, IReadOnlyCollection<string> statuses, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException("The CTS polling job claims bundles waiting for CTS results.");

        public async Task<ISubmissionBatchClaim> ClaimAwaitingCtsResultsAsync(DbContext dbContext, int batchSize, CancellationToken cancellationToken = default)
        {
            RequestedBatchSize = batchSize;
            SubmissionIds = onlyThese.Length > 0
                ? onlyThese
                : await dbContext.Set<Submission>()
                    .Where(s => s.Status == Statuses.Submitted && s.CtsReceiptNumber != null && s.CompletedAt == null)
                    .OrderBy(s => s.CtsLastPolledAt).ThenBy(s => s.CreatedAt)
                    .Take(batchSize)
                    .Select(s => s.Id)
                    .ToListAsync(cancellationToken);
            return this;
        }

        public Task CommitAsync(CancellationToken cancellationToken = default)
        {
            if (CommitFailure is not null)
            {
                return Task.FromException(CommitFailure);
            }

            Committed = true;
            return Task.CompletedTask;
        }

        public ValueTask DisposeAsync()
        {
            Disposed = true;
            return ValueTask.CompletedTask;
        }
    }
}
