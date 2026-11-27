// <copyright file="CattleServiceTests.cs" company="Defra">
// Copyright (c) Defra. All rights reserved.
// </copyright>

namespace Defra.Lis.Api.Tests;

using Defra.Lis.Api.Interfaces;
using Defra.Lis.Api.Models;
using Defra.Lis.Api.Models.Responses;
using Defra.Lis.Api.Services;
using Defra.Lis.Entities;
using Defra.Livestock.Sdk.Core.Exceptions;
using Microsoft.EntityFrameworkCore;
using Moq;

public sealed class CattleServiceTests : IDisposable
{
    private readonly Mock<ICadsService> mockCadsService;
    private readonly CattleService service;
    private readonly TestDbContext context;

    public CattleServiceTests()
    {
        mockCadsService = new Mock<ICadsService>();

        var dbName = Guid.NewGuid().ToString();
        var options = new DbContextOptionsBuilder<TestDbContext>()
            .UseInMemoryDatabase(dbName)
            .Options;

        context = new TestDbContext(options);
        service = new CattleService(mockCadsService.Object, context);
    }

    [Fact]
    public async Task GetCattleDetailsAsync_ReturnsTheCadsAnimalDetails()
    {
        var expected = new CattleDetailsResponse { EarTag = "UK200000000001", State = "Alive" };
        mockCadsService.Setup(s => s.GetAnimalDetailsAsync("UK200000000001", It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);

        var result = await service.GetCattleDetailsAsync("UK200000000001", TestContext.Current.CancellationToken);

        Assert.Same(expected, result);
        mockCadsService.Verify(
            s => s.GetAnimalDetailsAsync("UK200000000001", It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task GetCattleDetailsAsync_DoesNotFallBackToLocalSubmissions()
    {
        // Details are CADS-only: an animal that exists only in a local bundle is not found here,
        // even though GetCattleForHoldingAsync lists it against its holding.
        var submission = new Submission("ref1", "12/345/6789", "user1");
        submission.AddAnimal("UK123456700002");
        context.Set<Submission>().Add(submission);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        mockCadsService.Setup(s => s.GetAnimalDetailsAsync("UK123456700002", It.IsAny<CancellationToken>()))
            .ThrowsAsync(new NotFoundException("Animal 'UK123456700002' was not found."));

        await Assert.ThrowsAsync<NotFoundException>(() =>
            service.GetCattleDetailsAsync("UK123456700002", TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task GetCattleForHoldingAsync_ReturnsMergedData()
    {
        // Arrange
        var cph = "12/345/6789";
        var earTag1 = "UK123456700001";
        var earTag2 = "UK123456700002";

        var cadsData = new List<CattleResponse> { new() { EarTag = earTag1, Status = "active", Breed = "Angus" } };

        mockCadsService.Setup(s => s.GetCattleByCphAsync(cph, It.IsAny<CancellationToken>()))
            .ReturnsAsync(cadsData);

        var submission = new Submission("ref1", cph, "user1");
        var animal1 = submission.AddAnimal(earTag1, "pending_update");
        animal1.AddError("ERR01", "Test Error");
        submission.AddAnimal(earTag2, "new_animal");

        context.Set<Submission>().Add(submission);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        var result = (await service.GetCattleForHoldingAsync(cph, null, TestContext.Current.CancellationToken))
            .ToList();

        // Assert
        Assert.Equal(2, result.Count);

        var r1 = result.First(r => r.EarTag == earTag1);
        Assert.Equal("pending_update", r1.Status); // Enhanced from local
        Assert.Single(r1.Errors);
        Assert.Equal("ERR01", r1.Errors[0].ErrorCode);

        var r2 = result.First(r => r.EarTag == earTag2);
        Assert.Equal("new_animal", r2.Status); // Added from local
    }

    [Fact]
    public async Task GetCattleForHoldingAsync_AppliesFilterAfterMergingLocalRecords()
    {
        // Arrange
        var cph = "12/345/6789";
        var cadsData = new List<CattleResponse>
        {
            new()
            {
                EarTag = "UK123456700001",
                Status = "Alive",
                Sex = "Female",
                BreedCode = "AA",
                BreedName = "Aberdeen Angus",
            },
            new()
            {
                EarTag = "UK123456700002",
                Status = "Alive",
                Sex = "Male",
                BreedCode = "AA",
                BreedName = $"Aberdeen Angus",
            },
        };

        mockCadsService.Setup(s => s.GetCattleByCphAsync(cph, It.IsAny<CancellationToken>()))
            .ReturnsAsync(cadsData);

        var submission = new Submission("ref1", cph, "user1");
        submission.AddAnimal("UK123456700003", sex: "Female", breed: "AA");

        context.Set<Submission>().Add(submission);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        var females = (
                await service.GetCattleForHoldingAsync(
                    cph, new CattleFilter(Sex: "female"), TestContext.Current.CancellationToken))
            .Select(c => c.EarTag)
            .ToList();
        var byTag = (
                await service.GetCattleForHoldingAsync(
                    cph, new CattleFilter(EarTag: "700002"), TestContext.Current.CancellationToken))
            .Select(c => c.EarTag)
            .ToList();
        var unfiltered =
            (await service.GetCattleForHoldingAsync(cph, new CattleFilter(), TestContext.Current.CancellationToken))
            .ToList();

        // Assert
        Assert.Equal(["UK123456700001", "UK123456700003"], females);
        Assert.Equal(["UK123456700002"], byTag);
        Assert.Equal(3, unfiltered.Count);
    }

    [Fact]
    public async Task GetBundlesForHoldingAsync_ReturnsBundlesWithAnimalsAndErrors()
    {
        // Arrange
        var cph = "12/345/6789";
        var otherCph = "99/888/7777";

        var submission1 = new Submission("ref1", cph, "user1");
        var animal1 = submission1.AddAnimal("UK123456700001", Statuses.Pending);
        animal1.AddError("ERR01", "Test Error 1");
        animal1.AddError("ERR02", "Test Error 2");
        submission1.AddAnimal("UK123456700002", "valid");

        var submission2 = new Submission("ref2", cph, "user2");
        var animal3 = submission2.AddAnimal("UK123456700003", "rejected");
        animal3.AddError("ERR03", "Test Error 3");

        var otherSubmission = new Submission("ref3", otherCph, "user3");
        otherSubmission.AddAnimal("UK999999900001", Statuses.Pending);

        context.Set<Submission>().AddRange(submission1, submission2, otherSubmission);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        var result = (await service.GetBundlesForHoldingAsync(cph)).ToList();

        // Assert
        Assert.Equal(2, result.Count);
        Assert.All(result, b => Assert.Equal(cph, b.CountyParishHolding));

        var bundle1 = result.First(b => b.ClientReference == "ref1");
        Assert.Equal("user1", bundle1.SubmittedBy);
        Assert.Equal(2, bundle1.Animals.Count);

        var a1 = bundle1.Animals.First(a => a.EarTag == "UK123456700001");
        Assert.Equal(2, a1.Errors.Count);
        Assert.Contains(a1.Errors, e => e.ErrorCode == "ERR01" && e.ErrorText == "Test Error 1");
        Assert.Contains(a1.Errors, e => e.ErrorCode == "ERR02" && e.ErrorText == "Test Error 2");

        var a2 = bundle1.Animals.First(a => a.EarTag == "UK123456700002");
        Assert.Empty(a2.Errors);

        var bundle2 = result.First(b => b.ClientReference == "ref2");
        Assert.Equal("user2", bundle2.SubmittedBy);
        Assert.Single(bundle2.Animals);
        Assert.Single(bundle2.Animals[0].Errors);
        Assert.Equal("ERR03", bundle2.Animals[0].Errors[0].ErrorCode);
    }

    [Fact]
    public async Task GetBundlesForHoldingAsync_WhenNoBundles_ReturnsEmptyList()
    {
        // Act
        var result = await service.GetBundlesForHoldingAsync("00/000/0000");

        // Assert
        Assert.Empty(result);
    }

    public void Dispose()
    {
        context.Dispose();
    }

    private sealed class TestDbContext(DbContextOptions<TestDbContext> options)
        : DbContext(options)
    {
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            ConfigureModel(modelBuilder);
        }

        private static void ConfigureModel(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Submission>(builder =>
            {
                builder.ToTable("submissions", "public");
                builder.HasKey(e => e.Id);
                builder.HasMany(e => e.Animals).WithOne(a => a.Submission).HasForeignKey(a => a.SubmissionId);
            });

            modelBuilder.Entity<SubmissionAnimal>(builder =>
            {
                builder.ToTable("submission_animals", "public");
                builder.HasKey(e => e.Id);
                builder.HasMany(e => e.Errors).WithOne(a => a.Animal).HasForeignKey(a => a.AnimalId);
            });

            modelBuilder.Entity<SubmissionAnimalError>(builder =>
            {
                builder.ToTable("submission_animal_errors", "public");
                builder.HasKey(e => e.Id);
            });
        }
    }
}
