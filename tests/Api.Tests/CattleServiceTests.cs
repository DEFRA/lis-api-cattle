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

        var submission = new Submission("ref1", cph, "user1");
        var animal1 = submission.AddAnimal(earTag1, "pending_update");
        animal1.AddError("ERR01", "Test Error");
        var animal2 = submission.AddAnimal(earTag2, "new_animal");

        _setupContext.Set<Submission>().Add(submission);
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

        _setupContext.Set<Submission>().AddRange(submission1, submission2, otherSubmission);
        await _setupContext.SaveChangesAsync(TestContext.Current.CancellationToken);

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
}