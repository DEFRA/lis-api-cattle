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
    private readonly ReadOnlyPostgresDbContext _readOnlyContext;
    private readonly DbContext _setupContext;

    public CattleServiceTests()
    {
        _mockCadsService = new Mock<ICadsService>();

        var options = new DbContextOptionsBuilder<ReadOnlyPostgresDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        
        var setupOptions = new DbContextOptionsBuilder<TestSetupDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString()) // Wait, must be same database name!
            .Options;
        
        var dbName = Guid.NewGuid().ToString();
        var opt = new DbContextOptionsBuilder().UseInMemoryDatabase(dbName).Options;

        _setupContext = new TestSetupDbContext(new DbContextOptionsBuilder<TestSetupDbContext>().UseInMemoryDatabase(dbName).Options);
        _readOnlyContext = new TestReadOnlyPostgresDbContext(new DbContextOptionsBuilder<ReadOnlyPostgresDbContext>().UseInMemoryDatabase(dbName).Options);

        _service = new CattleService(_mockCadsService.Object, _readOnlyContext);
    }

    private class TestSetupDbContext : DbContext
    {
        public TestSetupDbContext(DbContextOptions<TestSetupDbContext> options) : base(options) { }
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            ConfigureModel(modelBuilder);
        }
    }

    private class TestReadOnlyPostgresDbContext : ReadOnlyPostgresDbContext
    {
        public TestReadOnlyPostgresDbContext(DbContextOptions<ReadOnlyPostgresDbContext> options) : base(options) { }
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
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

        var submission = new Submission
        {
            Id = Guid.NewGuid(),
            CountyParishHolding = cph,
            Status = "submitted",
            ClientReference = "ref1",
            SubmittedBy = "user1"
        };

        var animal1 = new SubmissionAnimal
        {
            Id = Guid.NewGuid(),
            SubmissionId = submission.Id,
            EarTag = earTag1,
            Status = "pending_update",
            Errors = new List<SubmissionAnimalError>
            {
                new() { ErrorCode = "ERR01", ErrorText = "Test Error" }
            }
        };

        var animal2 = new SubmissionAnimal
        {
            Id = Guid.NewGuid(),
            SubmissionId = submission.Id,
            EarTag = earTag2,
            Status = "new_animal"
        };

        _setupContext.Set<Submission>().Add(submission);
        _setupContext.Set<SubmissionAnimal>().AddRange(animal1, animal2);
        await _setupContext.SaveChangesAsync(TestContext.Current.CancellationToken);

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
}
