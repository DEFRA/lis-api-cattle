// <copyright file="CattleServiceTests.cs" company="Defra">
// Copyright (c) Defra. All rights reserved.
// </copyright>

namespace Defra.Lis.Api.Tests;

using Defra.Lis.Api.Interfaces;
using Defra.Lis.Api.Models;
using Defra.Lis.Api.Services;
using Defra.Lis.Entities;
using Microsoft.EntityFrameworkCore;
using Moq;

public class CattleServiceTests
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
    public async Task GetCattleForHoldingAsync_ReturnsMergedData()
    {
        // Arrange
        var cph = "12/345/6789";
        var earTag1 = "UK123456700001";
        var earTag2 = "UK123456700002";

        var cadsData = new List<CattleResponse> { new() { EarTag = earTag1, Status = "active", Breed = "Angus" } };

        mockCadsService.Setup(s => s.GetCattleByCphAsync(cph))
            .ReturnsAsync(cadsData);

        var submission = new Submission("ref1", cph, "user1");
        var animal1 = submission.AddAnimal(earTag1, "pending_update");
        animal1.AddError("ERR01", "Test Error");
        submission.AddAnimal(earTag2, "new_animal");

        context.Set<Submission>().Add(submission);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        var result = (await service.GetCattleForHoldingAsync(cph)).ToList();

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

    [Fact]
    public async Task CreateRegistrationBundleAsync_WithValidRequest_DecomposesAndSavesSubmissionWithPendingStatus()
    {
        // Arrange
        var request = new RegistrationBundleRequest
        {
            ClientReference = "REG-MNBX4Q2A",
            Holding = new HoldingRequest { Cph = "10/081/1234", },
            Animals =
            [
                new AnimalRegistrationRequest
                {
                    EarTag = "UK 12 3456 100003",
                    DateOfBirth = new DateOnly(2026, 2, 1),
                    Sex = "female",
                    Breed = "Aberdeen Angus",
                    Dam = new DamRegistrationRequest
                    {
                        Type = "surrogate",
                        GeneticDamEarTag = "UK 12 3456 000002",
                        SurrogateDamEarTag = "UK 12 3456 000003",
                    },
                    Sire = new SireRegistrationRequest { EarTag = "UK 12 3456 000010", Name = "Example sire", },
                }
            ],
        };

        // Act
        var result = await service.CreateRegistrationBundleAsync(request, TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(result);
        Assert.NotEqual(Guid.Empty, result.Id);
        Assert.Equal("REG-MNBX4Q2A", result.ClientReference);
        Assert.Equal("10/081/1234", result.CountyParishHolding);
        Assert.Equal("BE4FE", result.SubmittedBy);
        Assert.Equal(Statuses.Pending, result.Status);
        Assert.Single(result.Animals);

        var animalResult = result.Animals[0];
        Assert.NotEqual(Guid.Empty, animalResult.Id);
        Assert.Equal(result.Id, animalResult.SubmissionId);
        Assert.Equal("UK 12 3456 100003", animalResult.EarTag);
        Assert.Equal(Statuses.Pending, animalResult.Status);
        Assert.Equal(new DateOnly(2026, 2, 1), animalResult.DateBirth);
        Assert.Equal("female", animalResult.Sex);
        Assert.Equal("Aberdeen Angus", animalResult.Breed);
        Assert.Equal("surrogate", animalResult.DamType);
        Assert.Equal("UK 12 3456 000002", animalResult.DamGeneticEarTag);
        Assert.Equal("UK 12 3456 000003", animalResult.DamSurrogateEarTag);
        Assert.Equal("UK 12 3456 000010", animalResult.SireEarTag);
        Assert.Equal("Example sire", animalResult.SireName);

        // Verify database persistence
        var savedSubmission = await context.Set<Submission>()
            .Include(s => s.Animals)
            .FirstOrDefaultAsync(s => s.Id == result.Id, TestContext.Current.CancellationToken);

        Assert.NotNull(savedSubmission);
        Assert.Equal(Statuses.Pending, savedSubmission.Status);
        Assert.Single(savedSubmission.Animals);
        Assert.Equal(Statuses.Pending, savedSubmission.Animals.First().Status);
    }

    [Theory]
    [InlineData("", "10/081/1234")]
    [InlineData("REG-123", "")]
    public async Task CreateRegistrationBundleAsync_WithInvalidRequest_ThrowsArgumentException(
        string clientRef,
        string cph)
    {
        var request = new RegistrationBundleRequest
        {
            ClientReference = clientRef,
            Holding = new HoldingRequest { Cph = cph },
        };

        await Assert.ThrowsAsync<ArgumentException>(() =>
            service.CreateRegistrationBundleAsync(request, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task CreateRegistrationBundleAsync_WithNullRequest_ThrowsArgumentNullException()
    {
        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            service.CreateRegistrationBundleAsync(null!, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task CreateRegistrationBundleAsync_WithNullHolding_ThrowsArgumentNullException()
    {
        var request = new RegistrationBundleRequest { ClientReference = "REG-123", Holding = null };

        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            service.CreateRegistrationBundleAsync(request, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task CreateRegistrationBundleAsync_WithMultipleAnimalsWithoutDamSire_SavesCorrectly()
    {
        // Arrange
        var request = new RegistrationBundleRequest
        {
            ClientReference = "REG-BUNDLE-2",
            Holding = new HoldingRequest { Cph = "20/082/5678" },
            SubmittedBy = "CustomUser",
            Animals =
            [
                new AnimalRegistrationRequest
                {
                    EarTag = "UK 20 5678 000001",
                    DateOfBirth = new DateOnly(2026, 1, 15),
                    Sex = "male",
                    Breed = "Limousin",
                },
                new AnimalRegistrationRequest
                {
                    EarTag = "UK 20 5678 000002",
                    DateOfBirth = new DateOnly(2026, 1, 16),
                    Sex = "female",
                    Breed = "Hereford",
                }
            ],
        };

        // Act
        var result = await service.CreateRegistrationBundleAsync(request, TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("REG-BUNDLE-2", result.ClientReference);
        Assert.Equal("20/082/5678", result.CountyParishHolding);
        Assert.Equal("CustomUser", result.SubmittedBy);
        Assert.Equal(Statuses.Pending, result.Status);
        Assert.Equal(2, result.Animals.Count);
        Assert.All(result.Animals, a =>
        {
            Assert.Equal(Statuses.Pending, a.Status);
            Assert.Null(a.DamType);
            Assert.Null(a.DamGeneticEarTag);
            Assert.Null(a.DamSurrogateEarTag);
            Assert.Null(a.SireEarTag);
            Assert.Null(a.SireName);
        });
    }

    [Fact]
    public async Task CreateRegistrationBundleAsync_WithPublisherConfigured_PublishesValidationMessage()
    {
        // Arrange
        var mockPublisher = new Mock<global::Defra.Lis.Api.Messaging.ISubmissionMessagePublisher>();
        var serviceWithPublisher = new CattleService(mockCadsService.Object, context, mockPublisher.Object, null);

        var request = new RegistrationBundleRequest
        {
            ClientReference = "REG-MSG-1",
            Holding = new HoldingRequest { Cph = "10/081/1234" },
            SubmittedBy = "USER1",
            Animals =
            [
                new AnimalRegistrationRequest { EarTag = "UK123456700001" }
            ],
        };

        // Act
        var result =
            await serviceWithPublisher.CreateRegistrationBundleAsync(request, TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(result);
        mockPublisher.Verify(
            p => p.PublishSubmissionForValidationAsync(
                It.Is<global::Defra.Lis.Api.Messaging.SubmissionValidationMessage>(m =>
                    m.SubmissionId == result.Id &&
                    m.CountyParishHolding == "10/081/1234" &&
                    m.ClientReference == "REG-MSG-1" &&
                    m.AnimalCount == 1),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    private class TestDbContext(DbContextOptions<TestDbContext> options)
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
