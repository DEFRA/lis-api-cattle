// <copyright file="CtsBundleProcessorServiceTests.cs" company="Defra">
// Copyright (c) Defra. All rights reserved.
// </copyright>

namespace Defra.Lis.Api.Tests;

using Defra.Lis.Api.Interfaces;
using Defra.Lis.Api.Models;
using Defra.Lis.Api.Services;
using Defra.Lis.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Moq;

public class CtsBundleProcessorServiceTests
{
    private readonly Mock<ICtsService> mockCtsService;
    private readonly TestDbContext context;

    public CtsBundleProcessorServiceTests()
    {
        mockCtsService = new Mock<ICtsService>();

        var dbName = Guid.NewGuid().ToString();
        var options = new DbContextOptionsBuilder<TestDbContext>()
            .UseInMemoryDatabase(dbName)
            .Options;

        context = new TestDbContext(options);
    }

    [Fact]
    public async Task ProcessPendingBundlesAsync_WhenBundleIsSubmitted_SubmitsAnimalsAndMarksProcessing()
    {
        // Arrange
        var submission = new Submission("REF1", "10/100/1000", "testUser", status: Statuses.Submitted);
        submission.AddAnimal("UK100001", status: Statuses.Submitted);
        submission.AddAnimal("UK100002", status: Statuses.Submitted);

        context.Set<Submission>().Add(submission);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        mockCtsService.Setup(s => s.SubmitAnimalRegistrationAsync(It.IsAny<SubmissionAnimal>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CtsAnimalStatusResponse { Status = Statuses.Processing });

        var options = Options.Create(new CtsPollingJobOptions { BatchSize = 10 });
        var service = new CtsBundleProcessorService(mockCtsService.Object, context, options);

        // Act
        await service.ProcessPendingBundlesAsync(TestContext.Current.CancellationToken);

        // Assert
        var updated = await context.Set<Submission>().Include(s => s.Animals).FirstAsync(s => s.Id == submission.Id, TestContext.Current.CancellationToken);
        Assert.Equal(Statuses.Processing, updated.Status);
        Assert.All(updated.Animals, a => Assert.Equal(Statuses.Processing, a.Status));
        mockCtsService.Verify(s => s.SubmitAnimalRegistrationAsync(It.IsAny<SubmissionAnimal>(), It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    [Fact]
    public async Task ProcessPendingBundlesAsync_WhenProcessingBundleAnimalsComeBackClean_CompletesBundle()
    {
        // Arrange
        var submission = new Submission("REF2", "10/100/1000", "testUser", status: Statuses.Processing);
        submission.AddAnimal("UK100001", status: "processing");
        submission.AddAnimal("UK100002", status: "processing");

        context.Set<Submission>().Add(submission);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        mockCtsService.Setup(s => s.CheckAnimalStatusAsync(It.IsAny<string>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CtsAnimalStatusResponse { Status = "clean" });

        var options = Options.Create(new CtsPollingJobOptions { BatchSize = 10 });
        var service = new CtsBundleProcessorService(mockCtsService.Object, context, options);

        // Act
        await service.ProcessPendingBundlesAsync(TestContext.Current.CancellationToken);

        // Assert
        var updated = await context.Set<Submission>().Include(s => s.Animals).FirstAsync(s => s.Id == submission.Id, TestContext.Current.CancellationToken);
        Assert.Equal(Statuses.Complete, updated.Status);
        Assert.All(updated.Animals, a => Assert.Equal(Statuses.Complete, a.Status));
    }

    [Fact]
    public async Task ProcessPendingBundlesAsync_WhenProcessingBundleHasErrors_SetsAnimalAndBundleError()
    {
        // Arrange
        var submission = new Submission("REF3", "10/100/1000", "testUser", status: Statuses.Processing);
        var animal1 = submission.AddAnimal("UK100001", status: Statuses.Processing);
        var animal2 = submission.AddAnimal("UK100002", status: Statuses.Processing);

        context.Set<Submission>().Add(submission);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        mockCtsService.Setup(s => s.CheckAnimalStatusAsync("UK100001", animal1.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CtsAnimalStatusResponse { Status = "clean" });

        mockCtsService.Setup(s => s.CheckAnimalStatusAsync("UK100002", animal2.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CtsAnimalStatusResponse
            {
                Status = Statuses.Error,
                Errors = [new CtsErrorResponse { ErrorCode = "ERR_AGE", ErrorText = "Animal age exceeds maximum" }],
            });

        var options = Options.Create(new CtsPollingJobOptions { BatchSize = 10 });
        var service = new CtsBundleProcessorService(mockCtsService.Object, context, options);

        // Act
        await service.ProcessPendingBundlesAsync(TestContext.Current.CancellationToken);

        // Assert
        var updated = await context.Set<Submission>().Include(s => s.Animals).ThenInclude(a => a.Errors).FirstAsync(s => s.Id == submission.Id, TestContext.Current.CancellationToken);
        Assert.Equal(Statuses.Error, updated.Status);

        var a1 = updated.Animals.First(a => a.EarTag == "UK100001");
        Assert.Equal(Statuses.Complete, a1.Status);

        var a2 = updated.Animals.First(a => a.EarTag == "UK100002");
        Assert.Equal(Statuses.Error, a2.Status);
        Assert.Single(a2.Errors);
        Assert.Equal("ERR_AGE", a2.Errors.First().ErrorCode);
    }

    [Fact]
    public async Task ProcessPendingBundlesAsync_WhenErrorBundleErrorsAreResolved_MarksBundleComplete()
    {
        // Arrange
        var submission = new Submission("REF4", "10/100/1000", "testUser", status: Statuses.Error);
        var animal1 = submission.AddAnimal("UK100001", status: Statuses.Error);
        animal1.AddError("OLD_ERR", "Previous error");

        context.Set<Submission>().Add(submission);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        mockCtsService.Setup(s => s.CheckAnimalStatusAsync("UK100001", animal1.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CtsAnimalStatusResponse { Status = "clean" });

        var options = Options.Create(new CtsPollingJobOptions { BatchSize = 10 });
        var service = new CtsBundleProcessorService(mockCtsService.Object, context, options);

        // Act
        await service.ProcessPendingBundlesAsync(TestContext.Current.CancellationToken);

        // Assert
        var updated = await context.Set<Submission>().Include(s => s.Animals).ThenInclude(a => a.Errors).FirstAsync(s => s.Id == submission.Id, TestContext.Current.CancellationToken);
        Assert.Equal(Statuses.Complete, updated.Status);
        Assert.Equal(Statuses.Complete, updated.Animals.First().Status);
        Assert.Empty(updated.Animals.First().Errors);
    }

    [Fact]
    public async Task ProcessPendingBundlesAsync_WhenNoPendingBundles_CompletesGracefully()
    {
        var options = Options.Create(new CtsPollingJobOptions { BatchSize = 10 });
        var service = new CtsBundleProcessorService(mockCtsService.Object, context, options);

        await service.ProcessPendingBundlesAsync(TestContext.Current.CancellationToken);

        mockCtsService.Verify(s => s.SubmitAnimalRegistrationAsync(It.IsAny<SubmissionAnimal>(), It.IsAny<CancellationToken>()), Times.Never);
        mockCtsService.Verify(s => s.CheckAnimalStatusAsync(It.IsAny<string>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    private class TestDbContext(DbContextOptions<TestDbContext> options)
        : DbContext(options)
    {
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Submission>(builder =>
            {
                builder.ToTable("submissions", "public");
                builder.HasKey(e => e.Id);
                builder.Property(e => e.Id).ValueGeneratedNever();
                builder.HasMany(e => e.Animals).WithOne(a => a.Submission).HasForeignKey(a => a.SubmissionId);
            });

            modelBuilder.Entity<SubmissionAnimal>(builder =>
            {
                builder.ToTable("submission_animals", "public");
                builder.HasKey(e => e.Id);
                builder.Property(e => e.Id).ValueGeneratedNever();
                builder.HasMany(e => e.Errors).WithOne(a => a.Animal).HasForeignKey(a => a.AnimalId);
            });

            modelBuilder.Entity<SubmissionAnimalError>(builder =>
            {
                builder.ToTable("submission_animal_errors", "public");
                builder.HasKey(e => e.Id);
                builder.Property(e => e.Id).ValueGeneratedNever();
            });
        }
    }
}
