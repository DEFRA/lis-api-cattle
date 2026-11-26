// <copyright file="RegistrationBundleHandlerTests.cs" company="Defra">
// Copyright (c) Defra. All rights reserved.
// </copyright>

namespace Defra.Lis.Api.Tests.Messaging;

using System.Xml.Linq;
using Defra.Lis.Api.Cts;
using Defra.Lis.Api.Messaging;
using Defra.Lis.Api.Models.Requests;
using Defra.Lis.Database.Claims;
using Defra.Lis.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

public class RegistrationBundleHandlerTests
{
    private readonly TestDbContext context;
    private readonly Mock<ICtsWebClient> ctsClient = new();
    private readonly FixedTimeProvider clock = new(DateTimeOffset.Parse("2026-11-16T09:30:00Z", System.Globalization.CultureInfo.InvariantCulture));
    private readonly InMemoryLocker locker = new();

    public RegistrationBundleHandlerTests()
    {
        context = new TestDbContext(new DbContextOptionsBuilder<TestDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
    }

    [Fact]
    public async Task HandleAsync_StoresTheBundleAsSubmittedStoresTheRequestThenStoresTheReceipt()
    {
        var message = Message(animals: 2);
        string? sentPayload = null;
        ctsClient
            .Setup(c => c.SubmitAsync(CtsBirthRequestBuilder.Operation, It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback<string, string, CancellationToken>((_, payload, _) => sentPayload = payload)
            .ReturnsAsync(CtsSubmitResult.Accepted(4242));

        var outcome = await CreateHandler().HandleAsync(message, TestContext.Current.CancellationToken);

        Assert.Equal(RegistrationBundleOutcome.Sent, outcome);
        var stored = await LoadAsync(message.SubmissionId);
        Assert.Equal(Statuses.Submitted, stored.Status);
        Assert.Equal("corr-925", stored.CorrelationId);
        Assert.Equal("REG-925", stored.ClientReference);
        Assert.Equal("10/081/1234", stored.CountyParishHolding);
        Assert.Equal(2, stored.Animals.Count);
        Assert.All(stored.Animals, a => Assert.Equal(Statuses.Submitted, a.Status));
        Assert.Equal(4242, stored.CtsReceiptNumber);
        Assert.Equal(clock.GetUtcNow(), stored.CtsSentAt);
        Assert.Null(stored.ErrorCode);

        Assert.NotNull(stored.CtsRequest);
        Assert.Equal(message.SubmissionId.ToString(), stored.CtsRequest.TxnId);
        Assert.Equal(4242, stored.CtsRequest.ReceiptNumber);
        Assert.Equal(stored.CtsRequest.Payload, sentPayload);
        Assert.Equal(2, XElement.Parse(stored.CtsRequest.Payload).Descendants(CtsBirthRequestBuilder.Namespace + "Birth").Count());
        Assert.True(locker.Committed);
    }

    [Fact]
    public async Task HandleAsync_WhenTheMessageIsRedeliveredAfterTheReceipt_DoesNotStoreOrSendASecondTime()
    {
        var message = Message();
        ctsClient
            .Setup(c => c.SubmitAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(CtsSubmitResult.Accepted(7));
        await CreateHandler().HandleAsync(message, TestContext.Current.CancellationToken);

        var outcome = await CreateHandler().HandleAsync(message, TestContext.Current.CancellationToken);

        Assert.Equal(RegistrationBundleOutcome.AlreadyHandled, outcome);
        Assert.Equal(1, await context.Set<Submission>().CountAsync(TestContext.Current.CancellationToken));
        Assert.Equal(1, await context.Set<CtsRequest>().CountAsync(TestContext.Current.CancellationToken));
        ctsClient.Verify(c => c.SubmitAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_WhenTheMessageIsRedeliveredBeforeAnyOutcome_SendsTheStoredRequestOnce()
    {
        // The first delivery stored the bundle and its request, then the instance stopped before sending.
        var message = Message();
        var submission = RegistrationBundleMapper.ToSubmission(message);
        submission.RecordCtsRequest(CtsBirthRequestBuilder.TxnIdFor(submission), CtsBirthRequestBuilder.Operation, CtsBirthRequestBuilder.Build(submission, DateTimeOffset.UtcNow));
        context.Add(submission);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        var storedPayload = submission.CtsRequest!.Payload;
        context.ChangeTracker.Clear();

        ctsClient
            .Setup(c => c.SubmitAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(CtsSubmitResult.Accepted(99));

        var outcome = await CreateHandler().HandleAsync(message, TestContext.Current.CancellationToken);

        Assert.Equal(RegistrationBundleOutcome.Sent, outcome);
        Assert.Equal(1, await context.Set<Submission>().CountAsync(TestContext.Current.CancellationToken));
        ctsClient.Verify(c => c.SubmitAsync(CtsBirthRequestBuilder.Operation, storedPayload, It.IsAny<CancellationToken>()), Times.Once);
        Assert.Equal(99, (await LoadAsync(message.SubmissionId)).CtsReceiptNumber);
    }

    [Theory]
    [InlineData("CTWS808", "Request rejected - 'data' XML does not conform to the 'data' XSD", false)]
    [InlineData(CtsSubmitResult.UnavailableCode, "CTS could not be reached.", true)]
    [InlineData(CtsSubmitResult.ServiceUnavailableExNum, "Request rejected - service unavailable", true)]
    [InlineData(CtsSubmitResult.AlreadySubmittedExNum, "Request rejected - already submitted", false)]
    public async Task HandleAsync_WhenCtsRejectsOrCannotBeReached_MarksTheBundleAsErroredAndNeverAsSent(string code, string text, bool transient)
    {
        var message = Message();
        ctsClient
            .Setup(c => c.SubmitAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(CtsSubmitResult.Failed(code, text, transient));

        var outcome = await CreateHandler().HandleAsync(message, TestContext.Current.CancellationToken);

        Assert.Equal(RegistrationBundleOutcome.Failed, outcome);
        var stored = await LoadAsync(message.SubmissionId);
        Assert.Equal(Statuses.Error, stored.Status);
        Assert.Equal(code, stored.ErrorCode);
        Assert.Equal(text, stored.ErrorText);
        Assert.Null(stored.CtsReceiptNumber);
        Assert.Null(stored.CtsSentAt);
        Assert.NotNull(stored.CtsRequest);
        Assert.Null(stored.CtsRequest.SentAt);
        Assert.Equal($"{code}: {text}", stored.CtsRequest.LastError);
        Assert.True(locker.Committed);
    }

    [Fact]
    public async Task HandleAsync_WhenTheBundleAlreadyFailed_DoesNotSendItAgain()
    {
        var message = Message();
        ctsClient
            .Setup(c => c.SubmitAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(CtsSubmitResult.Failed("CTWS808", "Invalid", false));
        await CreateHandler().HandleAsync(message, TestContext.Current.CancellationToken);

        var outcome = await CreateHandler().HandleAsync(message, TestContext.Current.CancellationToken);

        Assert.Equal(RegistrationBundleOutcome.AlreadyHandled, outcome);
        ctsClient.Verify(c => c.SubmitAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_WhenTheCtsSettingsAreMissing_ThrowsSoTheMessageIsRetried()
    {
        var message = Message();
        ctsClient
            .Setup(c => c.SubmitAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Microsoft.Extensions.Options.OptionsValidationException("CtsWeb", typeof(CtsWebOptions), ["Url is required"]));

        await Assert.ThrowsAsync<Microsoft.Extensions.Options.OptionsValidationException>(
            () => CreateHandler().HandleAsync(message, TestContext.Current.CancellationToken));

        var stored = await LoadAsync(message.SubmissionId);
        Assert.Equal(Statuses.Submitted, stored.Status);
        Assert.False(stored.HasCtsOutcome);
        Assert.False(locker.Committed);
    }

    [Fact]
    public async Task HandleAsync_DefaultsTheSubmitterAndDropsEmptyOptionalEarTags()
    {
        var message = Message();
        message.Bundle.SubmittedBy = string.Empty;
        message.Bundle.Animals[0].Dam.SurrogateDamEarTag = string.Empty;
        message.Bundle.Animals[0].Sire = null;
        ctsClient
            .Setup(c => c.SubmitAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(CtsSubmitResult.Accepted(1));

        await CreateHandler().HandleAsync(message, TestContext.Current.CancellationToken);

        var stored = await LoadAsync(message.SubmissionId);
        Assert.Equal(RegistrationBundleMapper.DefaultSubmittedBy, stored.SubmittedBy);
        var animal = Assert.Single(stored.Animals);
        Assert.Null(animal.DamSurrogateEarTag);
        Assert.Null(animal.SireEarTag);
        var birth = XElement.Parse(stored.CtsRequest!.Payload).Descendants(CtsBirthRequestBuilder.Namespace + "Birth").Single();
        Assert.Null(birth.Attribute("SuEtg"));
        Assert.Null(birth.Attribute("SiEtg"));
    }

    [Fact]
    public async Task HandleAsync_CreatesAPendingAnimalRecordForEachAnimalLinkedToItsSubmission()
    {
        var message = Message(animals: 2);
        string? sentPayload = null;
        ctsClient
            .Setup(c => c.SubmitAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback<string, string, CancellationToken>((_, payload, _) => sentPayload = payload)
            .ReturnsAsync(CtsSubmitResult.Accepted(1));

        await CreateHandler().HandleAsync(message, TestContext.Current.CancellationToken);

        var stored = await LoadAsync(message.SubmissionId);
        var records = await context.Set<Animal>().ToListAsync(TestContext.Current.CancellationToken);
        Assert.Equal(2, records.Count);
        foreach (var submissionAnimal in stored.Animals)
        {
            var record = Assert.Single(records, r => r.SubmissionAnimalId == submissionAnimal.Id);
            Assert.Equal(AnimalStatuses.Pending, record.Status);
            Assert.Equal(message.SubmissionId, record.SubmissionId);
            Assert.Equal(CtsFormat.EarTag(submissionAnimal.EarTag), record.EarTag);
            Assert.Equal(submissionAnimal.DateBirth, record.DateBirth);
            Assert.Equal("female", record.Sex);
            Assert.Equal("AA", record.Breed);
            Assert.Equal("surrogate", record.DamType);
            Assert.Equal(submissionAnimal.DamGeneticEarTag, record.DamGeneticEarTag);
            Assert.Equal(submissionAnimal.DamSurrogateEarTag, record.DamSurrogateEarTag);
            Assert.Equal(submissionAnimal.SireEarTag, record.SireEarTag);
            Assert.Equal("Example sire", record.SireName);
            Assert.Empty(record.Errors);
        }

        // The CTS request is built and sent as before.
        Assert.All(stored.Animals, a => Assert.Equal(Statuses.Submitted, a.Status));
        Assert.Equal(stored.CtsRequest!.Payload, sentPayload);
        Assert.Equal(2, XElement.Parse(sentPayload!).Descendants(CtsBirthRequestBuilder.Namespace + "Birth").Count());
    }

    [Fact]
    public async Task HandleAsync_WhenAnEarTagIsAlreadyAllocated_CreatesNoRecordForItAndRejectsItWithCtws192()
    {
        ctsClient
            .Setup(c => c.SubmitAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(CtsSubmitResult.Accepted(1));
        var first = Message(animals: 1);
        await CreateHandler().HandleAsync(first, TestContext.Current.CancellationToken);

        // The second bundle reuses the first animal's tag, written differently, alongside a new animal.
        var second = Message(animals: 2);
        second.Bundle.Animals[0].EarTag = first.Bundle.Animals[0].EarTag.Replace(" ", string.Empty, StringComparison.Ordinal).ToLowerInvariant();
        await CreateHandler().HandleAsync(second, TestContext.Current.CancellationToken);

        var stored = await LoadAsync(second.SubmissionId);
        var reused = stored.Animals.Single(a => a.EarTag == second.Bundle.Animals[0].EarTag);
        var fresh = stored.Animals.Single(a => a.EarTag == second.Bundle.Animals[1].EarTag);
        Assert.Equal(Statuses.Error, reused.Status);
        var error = Assert.Single(reused.Errors);
        Assert.Equal("CTWS192", error.ErrorCode);
        Assert.Equal("Ear Tag has already been used", error.ErrorText);
        Assert.Equal(Statuses.Submitted, fresh.Status);

        var records = await context.Set<Animal>().ToListAsync(TestContext.Current.CancellationToken);
        Assert.Equal(2, records.Count);
        Assert.Single(records, r => r.EarTag == CtsFormat.EarTag(first.Bundle.Animals[0].EarTag) && r.SubmissionId == first.SubmissionId);
        Assert.Single(records, r => r.SubmissionAnimalId == fresh.Id);

        // The CTS request still names every animal.
        Assert.Equal(2, XElement.Parse(stored.CtsRequest!.Payload).Descendants(CtsBirthRequestBuilder.Namespace + "Birth").Count());
    }

    [Fact]
    public async Task HandleAsync_WhenAnEarTagIsRepeatedInTheBundle_CreatesOneRecordAndRejectsTheRepeatWithCtws204()
    {
        ctsClient
            .Setup(c => c.SubmitAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(CtsSubmitResult.Accepted(1));
        var message = Message(animals: 2);
        message.Bundle.Animals[1].EarTag = message.Bundle.Animals[0].EarTag;

        await CreateHandler().HandleAsync(message, TestContext.Current.CancellationToken);

        var stored = await LoadAsync(message.SubmissionId);
        var record = await context.Set<Animal>().SingleAsync(TestContext.Current.CancellationToken);
        var created = Assert.Single(stored.Animals, a => a.Id == record.SubmissionAnimalId);
        Assert.Equal(Statuses.Submitted, created.Status);
        var repeat = Assert.Single(stored.Animals, a => a.Id != record.SubmissionAnimalId);
        Assert.Equal(Statuses.Error, repeat.Status);
        Assert.Equal("CTWS204", Assert.Single(repeat.Errors).ErrorCode);
    }

    [Fact]
    public async Task HandleAsync_WhenTheMessageIsRedelivered_DoesNotCreateTheAnimalRecordsAgain()
    {
        ctsClient
            .Setup(c => c.SubmitAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(CtsSubmitResult.Accepted(1));
        var message = Message(animals: 2);
        await CreateHandler().HandleAsync(message, TestContext.Current.CancellationToken);

        await CreateHandler().HandleAsync(message, TestContext.Current.CancellationToken);

        Assert.Equal(2, await context.Set<Animal>().CountAsync(TestContext.Current.CancellationToken));
        Assert.All((await LoadAsync(message.SubmissionId)).Animals, a => Assert.Empty(a.Errors));
    }

    [Fact]
    public async Task HandleAsync_WhenTheSaveFails_StoresNeitherTheBundleNorItsAnimalRecords()
    {
        var message = Message();
        var failing = new TestDbContext(
            new DbContextOptionsBuilder<TestDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options,
            failSaves: true);
        var handler = new RegistrationBundleHandler(failing, locker, ctsClient.Object, clock, NullLogger<RegistrationBundleHandler>.Instance);

        await Assert.ThrowsAsync<DbUpdateException>(() => handler.HandleAsync(message, TestContext.Current.CancellationToken));

        failing.ChangeTracker.Clear();
        Assert.Equal(0, await failing.Set<Submission>().CountAsync(TestContext.Current.CancellationToken));
        Assert.Equal(0, await failing.Set<Animal>().CountAsync(TestContext.Current.CancellationToken));
        ctsClient.Verify(c => c.SubmitAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>
    /// A bundle message. Each call uses a new herd number in its ear tags, because an ear tag identifies one animal
    /// across every bundle stored in the same database.
    /// </summary>
    /// <param name="animals">The number of animals in the bundle.</param>
    /// <returns>The message.</returns>
    internal static RegistrationBundleMessage Message(int animals = 1) => Message(animals, Random.Shared.Next(100000, 1000000));

    internal static RegistrationBundleMessage Message(int animals, int herd) => new()
    {
        SubmissionId = Guid.NewGuid(),
        CorrelationId = "corr-925",
        AcceptedAt = DateTimeOffset.UtcNow,
        Bundle = new RegistrationBundleRequest
        {
            ClientReference = "REG-925",
            Holding = new HoldingRequest { Cph = "10/081/1234" },
            SubmittedBy = "test user",
            Animals = Enumerable.Range(1, animals).Select(i => new AnimalRegistrationRequest
            {
                EarTag = string.Create(System.Globalization.CultureInfo.InvariantCulture, $"UK {herd / 10000:D2} {herd % 10000:D4} 10000{i}"),
                DateOfBirth = new DateOnly(2026, 2, i),
                Sex = "female",
                Breed = "AA",
                Dam = new DamRegistrationRequest
                {
                    Type = "surrogate",
                    GeneticDamEarTag = "UK 12 3456 000002",
                    SurrogateDamEarTag = "UK 12 3456 000003",
                },
                Sire = new SireRegistrationRequest { EarTag = "UK 12 3456 000010", Name = "Example sire" },
            }).ToList(),
        },
    };

    private RegistrationBundleHandler CreateHandler() =>
        new(context, locker, ctsClient.Object, clock, NullLogger<RegistrationBundleHandler>.Instance);

    private async Task<Submission> LoadAsync(Guid id)
    {
        context.ChangeTracker.Clear();
        return await context.Set<Submission>()
            .Include(s => s.Animals).ThenInclude(a => a.Errors)
            .Include(s => s.CtsRequest)
            .SingleAsync(s => s.Id == id, TestContext.Current.CancellationToken);
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    private sealed class InMemoryLocker : ISubmissionLocker, ISubmissionLock
    {
        private DbContext? dbContext;
        private Guid submissionId;

        public bool Found => dbContext!.Set<Submission>().Any(s => s.Id == submissionId);

        public bool Committed { get; private set; }

        public Task<ISubmissionLock> LockAsync(DbContext dbContext, Guid submissionId, CancellationToken cancellationToken = default)
        {
            this.dbContext = dbContext;
            this.submissionId = submissionId;
            Committed = false;
            return Task.FromResult<ISubmissionLock>(this);
        }

        public Task CommitAsync(CancellationToken cancellationToken = default)
        {
            Committed = true;
            return Task.CompletedTask;
        }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    private sealed class TestDbContext(DbContextOptions<TestDbContext> options, bool failSaves = false) : DbContext(options)
    {
        public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) =>
            failSaves ? throw new DbUpdateException("The save failed.") : base.SaveChangesAsync(cancellationToken);

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Submission>(builder =>
            {
                builder.HasKey(e => e.Id);
                builder.Property(e => e.Id).ValueGeneratedNever();
                builder.Ignore(e => e.HasCtsOutcome);
                builder.HasMany(e => e.Animals).WithOne(a => a.Submission).HasForeignKey(a => a.SubmissionId);
                builder.HasOne(e => e.CtsRequest).WithOne(r => r.Submission).HasForeignKey<CtsRequest>(r => r.SubmissionId);
            });

            modelBuilder.Entity<SubmissionAnimal>(builder =>
            {
                builder.HasKey(e => e.Id);
                builder.Property(e => e.Id).ValueGeneratedNever();
                builder.HasMany(e => e.Errors).WithOne(a => a.Animal).HasForeignKey(a => a.AnimalId);
            });

            modelBuilder.Entity<SubmissionAnimalError>(builder => builder.HasKey(e => e.Id));
            modelBuilder.Entity<CtsRequest>(builder =>
            {
                builder.HasKey(e => e.Id);
                builder.Property(e => e.Id).ValueGeneratedNever();
            });

            modelBuilder.Entity<Animal>(builder =>
            {
                builder.HasKey(e => e.Id);
                builder.Property(e => e.Id).ValueGeneratedNever();
                builder.HasMany(e => e.Errors).WithOne().HasForeignKey(e => e.AnimalId);
            });

            modelBuilder.Entity<AnimalError>(builder =>
            {
                builder.HasKey(e => e.Id);
                builder.Property(e => e.Id).ValueGeneratedNever();
            });
        }
    }
}
