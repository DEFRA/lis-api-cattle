using Defra.Database.Postgres;
using Lis.Cattle.Interfaces;
using Lis.Cattle.Models;
using Lis.Cattle.Services;
using Microsoft.EntityFrameworkCore;
using Moq;

namespace Lis.Cattle;

public class CattleServiceTests
{
    private readonly Mock<ICadsService> _mockCadsService;
    private readonly CattleService _service;
    private readonly TestDbContext _context;

    public CattleServiceTests()
    {
        _mockCadsService = new Mock<ICadsService>();

        var dbName = Guid.NewGuid().ToString();
        var options = new DbContextOptionsBuilder<TestDbContext>()
            .UseInMemoryDatabase(dbName)
            .Options;

        _context = new TestDbContext(options);
        _service = new CattleService(_mockCadsService.Object, _context);
    }

    private class TestDbContext : DbContext
    {
        public TestDbContext(DbContextOptions<TestDbContext> options) : base(options) { }
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            ConfigureModel(modelBuilder);
        }
    }

    private static void ConfigureModel(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Submission>(builder =>
        {
            builder.ToTable("submission", "public");
            builder.HasKey(e => e.Id);
            builder.HasMany(e => e.Animals).WithOne(a => a.Submission).HasForeignKey(a => a.SubmissionId);
        });

        modelBuilder.Entity<SubmissionAnimal>(builder =>
        {
            builder.ToTable("submission_animal", "public");
            builder.HasKey(e => e.Id);
            builder.HasMany(e => e.Errors).WithOne(a => a.Animal).HasForeignKey(a => a.AnimalId);
        });

        modelBuilder.Entity<SubmissionAnimalError>(builder =>
        {
            builder.ToTable("submission_animal_error", "public");
            builder.HasKey(e => e.Id);
        });
    }

    [Fact]
    public async Task GetCattleForHoldingAsync_ReturnsMergedData()
    {
        // Arrange
        var cph = "12/345/6789";
        var earTag1 = "UK123456700001";
        var earTag2 = "UK123456700002";

        var cadsData = new List<CattleResponse>
        {
            new() { EarTag = earTag1, Status = "active", Breed = "Angus" }
        };

        _mockCadsService.Setup(s => s.GetCattleByCphAsync(cph))
            .ReturnsAsync(cadsData);

        var submission = new Submission("ref1", cph, "user1");
        var animal1 = submission.AddAnimal(earTag1, "pending_update");
        animal1.AddError("ERR01", "Test Error");
        var animal2 = submission.AddAnimal(earTag2, "new_animal");

        _context.Set<Submission>().Add(submission);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        var result = (await _service.GetCattleForHoldingAsync(cph)).ToList();

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
        var animal1 = submission1.AddAnimal("UK123456700001", "pending");
        animal1.AddError("ERR01", "Test Error 1");
        animal1.AddError("ERR02", "Test Error 2");
        var animal2 = submission1.AddAnimal("UK123456700002", "valid");

        var submission2 = new Submission("ref2", cph, "user2");
        var animal3 = submission2.AddAnimal("UK123456700003", "rejected");
        animal3.AddError("ERR03", "Test Error 3");

        var otherSubmission = new Submission("ref3", otherCph, "user3");
        otherSubmission.AddAnimal("UK999999900001", "pending");

        _context.Set<Submission>().AddRange(submission1, submission2, otherSubmission);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        var result = (await _service.GetBundlesForHoldingAsync(cph)).ToList();

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
        var result = await _service.GetBundlesForHoldingAsync("00/000/0000");

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
            Holding = new HoldingRequest
            {
                Cph = "10/081/1234"
            },
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
                        SurrogateDamEarTag = "UK 12 3456 000003"
                    },
                    Sire = new SireRegistrationRequest
                    {
                        EarTag = "UK 12 3456 000010",
                        Name = "Example sire"
                    }
                }
            ]
        };

        // Act
        var result = await _service.CreateRegistrationBundleAsync(request, TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(result);
        Assert.NotEqual(Guid.Empty, result.Id);
        Assert.Equal("REG-MNBX4Q2A", result.ClientReference);
        Assert.Equal("10/081/1234", result.CountyParishHolding);
        Assert.Equal("BE4FE", result.SubmittedBy);
        Assert.Equal("pending", result.Status);
        Assert.Single(result.Animals);

        var animalResult = result.Animals.First();
        Assert.NotEqual(Guid.Empty, animalResult.Id);
        Assert.Equal(result.Id, animalResult.SubmissionId);
        Assert.Equal("UK 12 3456 100003", animalResult.EarTag);
        Assert.Equal("pending", animalResult.Status);
        Assert.Equal(new DateOnly(2026, 2, 1), animalResult.DateBirth);
        Assert.Equal("female", animalResult.Sex);
        Assert.Equal("Aberdeen Angus", animalResult.Breed);
        Assert.Equal("surrogate", animalResult.DamType);
        Assert.Equal("UK 12 3456 000002", animalResult.DamGeneticEarTag);
        Assert.Equal("UK 12 3456 000003", animalResult.DamSurrogateEarTag);
        Assert.Equal("UK 12 3456 000010", animalResult.SireEarTag);
        Assert.Equal("Example sire", animalResult.SireName);

        // Verify database persistence
        var savedSubmission = await _context.Set<Submission>()
            .Include(s => s.Animals)
            .FirstOrDefaultAsync(s => s.Id == result.Id, TestContext.Current.CancellationToken);

        Assert.NotNull(savedSubmission);
        Assert.Equal("pending", savedSubmission.Status);
        Assert.Single(savedSubmission.Animals);
        Assert.Equal("pending", savedSubmission.Animals.First().Status);
    }

    [Theory]
    [InlineData("", "10/081/1234")]
    [InlineData("REG-123", "")]
    public async Task CreateRegistrationBundleAsync_WithInvalidRequest_ThrowsArgumentException(string clientRef, string cph)
    {
        var request = new RegistrationBundleRequest
        {
            ClientReference = clientRef,
            Holding = new HoldingRequest { Cph = cph }
        };

        await Assert.ThrowsAsync<ArgumentException>(() =>
            _service.CreateRegistrationBundleAsync(request, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task CreateRegistrationBundleAsync_WithNullRequest_ThrowsArgumentNullException()
    {
        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            _service.CreateRegistrationBundleAsync(null!, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task CreateRegistrationBundleAsync_WithNullHolding_ThrowsArgumentNullException()
    {
        var request = new RegistrationBundleRequest
        {
            ClientReference = "REG-123",
            Holding = null
        };

        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            _service.CreateRegistrationBundleAsync(request, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task CreateRegistrationBundleAsync_WithMultipleAnimalsWithoutDamSire_SavesCorrectly()
    {
        // Arrange
        var request = new RegistrationBundleRequest
        {
            ClientReference = "REG-BUNDLE-2",
            Holding = new HoldingRequest
            {
                Cph = "20/082/5678"
            },
            SubmittedBy = "CustomUser",
            Animals =
            [
                new AnimalRegistrationRequest
                {
                    EarTag = "UK 20 5678 000001",
                    DateOfBirth = new DateOnly(2026, 1, 15),
                    Sex = "male",
                    Breed = "Limousin"
                },
                new AnimalRegistrationRequest
                {
                    EarTag = "UK 20 5678 000002",
                    DateOfBirth = new DateOnly(2026, 1, 16),
                    Sex = "female",
                    Breed = "Hereford"
                }
            ]
        };

        // Act
        var result = await _service.CreateRegistrationBundleAsync(request, TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("REG-BUNDLE-2", result.ClientReference);
        Assert.Equal("20/082/5678", result.CountyParishHolding);
        Assert.Equal("CustomUser", result.SubmittedBy);
        Assert.Equal("pending", result.Status);
        Assert.Equal(2, result.Animals.Count);
        Assert.All(result.Animals, a =>
        {
            Assert.Equal("pending", a.Status);
            Assert.Null(a.DamType);
            Assert.Null(a.DamGeneticEarTag);
            Assert.Null(a.DamSurrogateEarTag);
            Assert.Null(a.SireEarTag);
            Assert.Null(a.SireName);
        });
    }
}