using Defra.Database.Postgres;
using Lis.Cattle.Configurations;
using Lis.Cattle.Interfaces;
using Lis.Cattle.Models;
using Lis.Cattle.Validation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace Lis.Cattle;

public class SubmissionValidationServiceTests
{
    private readonly Mock<ICadsService> _mockCadsService;
    private readonly SubmissionValidationOptions _options;

    public SubmissionValidationServiceTests()
    {
        _mockCadsService = new Mock<ICadsService>();
        _options = new SubmissionValidationOptions
        {
            MinDamAgeInMonths = 15,
            MaxDamAgeInYears = 20,
            MinCalvingIntervalDays = 240,
            MaxApplicationLateDays = 27
        };
    }

    private class TestDbContext : DbContext
    {
        public TestDbContext(DbContextOptions<TestDbContext> options) : base(options) { }
        protected override void OnModelCreating(ModelBuilder modelBuilder)
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

    private (DbContext dbContext, SubmissionValidationService service) CreateService(string dbName)
    {
        var dbOptions = new DbContextOptionsBuilder<TestDbContext>()
            .UseInMemoryDatabase(databaseName: dbName)
            .Options;

        var dbContext = new TestDbContext(dbOptions);
        var optionsWrapper = Options.Create(_options);
        var service = new SubmissionValidationService(dbContext, _mockCadsService.Object, optionsWrapper);

        return (dbContext, service);
    }

    [Fact]
    public async Task ValidateSubmissionAsync_ValidAnimal_MarksAsCompleteAndReturnsValid()
    {
        var (dbContext, service) = CreateService(nameof(ValidateSubmissionAsync_ValidAnimal_MarksAsCompleteAndReturnsValid));

        var submission = new Submission("REF1", "12/345/6789", "USER1");
        var animal = submission.AddAnimal(
            earTag: "UK123456789012",
            dateBirth: DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-5)),
            sex: "F",
            breed: "LIM",
            damType: "Genetic",
            damGeneticEarTag: "UK987654321098",
            sireEarTag: "UK111111222222");

        var result = await service.ValidateSubmissionAsync(submission, TestContext.Current.CancellationToken);

        Assert.True(result.IsValid);
        Assert.Equal("complete", result.Status);
        Assert.Equal(0, result.ErrorCount);
        Assert.Equal("complete", animal.Status);
        Assert.Empty(animal.Errors);
    }

    [Fact]
    public async Task ValidateSubmissionAsync_MissingEarTag_TriggersCTWS003()
    {
        var (dbContext, service) = CreateService(nameof(ValidateSubmissionAsync_MissingEarTag_TriggersCTWS003));

        var submission = new Submission("REF1", "12/345/6789", "USER1");
        var animal = submission.AddAnimal(
            earTag: "   ",
            dateBirth: DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-5)));

        var result = await service.ValidateSubmissionAsync(submission, TestContext.Current.CancellationToken);

        Assert.False(result.IsValid);
        Assert.Equal("error", submission.Status);
        Assert.Equal("error", animal.Status);
        Assert.Contains(animal.Errors, e => e.ErrorCode == ValidationRuleCodes.CTWS003);
    }

    [Fact]
    public async Task ValidateSubmissionAsync_InvalidEarTagFormat_TriggersCTWS004()
    {
        var (dbContext, service) = CreateService(nameof(ValidateSubmissionAsync_InvalidEarTagFormat_TriggersCTWS004));

        var submission = new Submission("REF1", "12/345/6789", "USER1");
        var animal = submission.AddAnimal(
            earTag: "INVALID_TAG_FORMAT",
            dateBirth: DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-5)));

        var result = await service.ValidateSubmissionAsync(submission, TestContext.Current.CancellationToken);

        Assert.False(result.IsValid);
        Assert.Equal("error", animal.Status);
        Assert.Contains(animal.Errors, e => e.ErrorCode == ValidationRuleCodes.CTWS004);
    }

    [Fact]
    public async Task ValidateSubmissionAsync_BirthDateInFuture_TriggersCTWS023()
    {
        var (dbContext, service) = CreateService(nameof(ValidateSubmissionAsync_BirthDateInFuture_TriggersCTWS023));

        var submission = new Submission("REF1", "12/345/6789", "USER1");
        var animal = submission.AddAnimal(
            earTag: "UK123456789012",
            dateBirth: DateOnly.FromDateTime(DateTime.UtcNow.AddDays(5)));

        var result = await service.ValidateSubmissionAsync(submission, TestContext.Current.CancellationToken);

        Assert.False(result.IsValid);
        Assert.Contains(animal.Errors, e => e.ErrorCode == ValidationRuleCodes.CTWS023);
    }

    [Fact]
    public async Task ValidateSubmissionAsync_ApplicationIsLate_TriggersCTWS203()
    {
        var (dbContext, service) = CreateService(nameof(ValidateSubmissionAsync_ApplicationIsLate_TriggersCTWS203));

        var submission = new Submission("REF1", "12/345/6789", "USER1");
        var animal = submission.AddAnimal(
            earTag: "UK123456789012",
            dateBirth: DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-40)));

        var result = await service.ValidateSubmissionAsync(submission, TestContext.Current.CancellationToken);

        Assert.False(result.IsValid);
        Assert.Contains(animal.Errors, e => e.ErrorCode == ValidationRuleCodes.CTWS203);
    }

    [Fact]
    public async Task ValidateSubmissionAsync_DuplicateEarTagInFile_TriggersCTWS204()
    {
        var (dbContext, service) = CreateService(nameof(ValidateSubmissionAsync_DuplicateEarTagInFile_TriggersCTWS204));

        var submission = new Submission("REF1", "12/345/6789", "USER1");
        var animal1 = submission.AddAnimal("UK123456789012", dateBirth: DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-5)));
        var animal2 = submission.AddAnimal("UK123456789012", dateBirth: DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-5)));

        var result = await service.ValidateSubmissionAsync(submission, TestContext.Current.CancellationToken);

        Assert.False(result.IsValid);
        Assert.Contains(animal1.Errors, e => e.ErrorCode == ValidationRuleCodes.CTWS204);
        Assert.Contains(animal2.Errors, e => e.ErrorCode == ValidationRuleCodes.CTWS204);
    }

    [Fact]
    public async Task ValidateSubmissionAsync_GeneticDamMatchesAnimalEarTag_TriggersCTWS034()
    {
        var (dbContext, service) = CreateService(nameof(ValidateSubmissionAsync_GeneticDamMatchesAnimalEarTag_TriggersCTWS034));

        var submission = new Submission("REF1", "12/345/6789", "USER1");
        var animal = submission.AddAnimal(
            earTag: "UK123456789012",
            dateBirth: DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-5)),
            damGeneticEarTag: "UK123456789012");

        var result = await service.ValidateSubmissionAsync(submission, TestContext.Current.CancellationToken);

        Assert.False(result.IsValid);
        Assert.Contains(animal.Errors, e => e.ErrorCode == ValidationRuleCodes.CTWS034);
    }

    [Fact]
    public async Task ValidateSubmissionAsync_SurrogateDamMatchesAnimalEarTag_TriggersCTWS042()
    {
        var (dbContext, service) = CreateService(nameof(ValidateSubmissionAsync_SurrogateDamMatchesAnimalEarTag_TriggersCTWS042));

        var submission = new Submission("REF1", "12/345/6789", "USER1");
        var animal = submission.AddAnimal(
            earTag: "UK123456789012",
            dateBirth: DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-5)),
            damSurrogateEarTag: "UK123456789012");

        var result = await service.ValidateSubmissionAsync(submission, TestContext.Current.CancellationToken);

        Assert.False(result.IsValid);
        Assert.Contains(animal.Errors, e => e.ErrorCode == ValidationRuleCodes.CTWS042);
    }

    [Fact]
    public async Task ValidateSubmissionAsync_SurrogateAndGeneticDamTagsMatch_TriggersCTWS043()
    {
        var (dbContext, service) = CreateService(nameof(ValidateSubmissionAsync_SurrogateAndGeneticDamTagsMatch_TriggersCTWS043));

        var submission = new Submission("REF1", "12/345/6789", "USER1");
        var animal = submission.AddAnimal(
            earTag: "UK123456789012",
            dateBirth: DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-5)),
            damGeneticEarTag: "UK987654321098",
            damSurrogateEarTag: "UK987654321098");

        var result = await service.ValidateSubmissionAsync(submission, TestContext.Current.CancellationToken);

        Assert.False(result.IsValid);
        Assert.Contains(animal.Errors, e => e.ErrorCode == ValidationRuleCodes.CTWS043);
    }

    [Fact]
    public async Task ValidateSubmissionAsync_InvalidSireEarTag_TriggersCTWS044()
    {
        var (dbContext, service) = CreateService(nameof(ValidateSubmissionAsync_InvalidSireEarTag_TriggersCTWS044));

        var submission = new Submission("REF1", "12/345/6789", "USER1");
        var animal = submission.AddAnimal(
            earTag: "UK123456789012",
            dateBirth: DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-5)),
            sireEarTag: "INVALID_SIRE_TAG");

        var result = await service.ValidateSubmissionAsync(submission, TestContext.Current.CancellationToken);

        Assert.False(result.IsValid);
        Assert.Contains(animal.Errors, e => e.ErrorCode == ValidationRuleCodes.CTWS044);
    }

    [Fact]
    public async Task ValidateSubmissionAsync_SireAndAnimalEarTagsMatch_TriggersCTWS050()
    {
        var (dbContext, service) = CreateService(nameof(ValidateSubmissionAsync_SireAndAnimalEarTagsMatch_TriggersCTWS050));

        var submission = new Submission("REF1", "12/345/6789", "USER1");
        var animal = submission.AddAnimal(
            earTag: "UK123456789012",
            dateBirth: DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-5)),
            sireEarTag: "UK123456789012");

        var result = await service.ValidateSubmissionAsync(submission, TestContext.Current.CancellationToken);

        Assert.False(result.IsValid);
        Assert.Contains(animal.Errors, e => e.ErrorCode == ValidationRuleCodes.CTWS050);
    }

    [Fact]
    public async Task ValidateSubmissionAsync_SireAndGeneticDamMatch_TriggersCTWS051()
    {
        var (dbContext, service) = CreateService(nameof(ValidateSubmissionAsync_SireAndGeneticDamMatch_TriggersCTWS051));

        var submission = new Submission("REF1", "12/345/6789", "USER1");
        var animal = submission.AddAnimal(
            earTag: "UK123456789012",
            dateBirth: DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-5)),
            damGeneticEarTag: "UK999999888888",
            sireEarTag: "UK999999888888");

        var result = await service.ValidateSubmissionAsync(submission, TestContext.Current.CancellationToken);

        Assert.False(result.IsValid);
        Assert.Contains(animal.Errors, e => e.ErrorCode == ValidationRuleCodes.CTWS051);
    }

    [Fact]
    public async Task ValidateSubmissionAsync_SireAndSurrogateDamMatch_TriggersCTWS052()
    {
        var (dbContext, service) = CreateService(nameof(ValidateSubmissionAsync_SireAndSurrogateDamMatch_TriggersCTWS052));

        var submission = new Submission("REF1", "12/345/6789", "USER1");
        var animal = submission.AddAnimal(
            earTag: "UK123456789012",
            dateBirth: DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-5)),
            damSurrogateEarTag: "UK999999888888",
            sireEarTag: "UK999999888888");

        var result = await service.ValidateSubmissionAsync(submission, TestContext.Current.CancellationToken);

        Assert.False(result.IsValid);
        Assert.Contains(animal.Errors, e => e.ErrorCode == ValidationRuleCodes.CTWS052);
    }

    [Fact]
    public async Task ValidateSubmissionAsync_InvalidBirthLocation_TriggersCTWS079()
    {
        var (dbContext, service) = CreateService(nameof(ValidateSubmissionAsync_InvalidBirthLocation_TriggersCTWS079));

        var submission = new Submission("REF1", "INVALID_CPH", "USER1");
        var animal = submission.AddAnimal("UK123456789012", dateBirth: DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-5)));

        var result = await service.ValidateSubmissionAsync(submission, TestContext.Current.CancellationToken);

        Assert.False(result.IsValid);
        Assert.Contains(animal.Errors, e => e.ErrorCode == ValidationRuleCodes.CTWS079);
    }

    [Fact]
    public async Task ValidateSubmissionAsync_EarTagAlreadyUsedInCADS_TriggersCTWS192()
    {
        var (dbContext, service) = CreateService(nameof(ValidateSubmissionAsync_EarTagAlreadyUsedInCADS_TriggersCTWS192));

        _mockCadsService.Setup(c => c.GetCattleByCphAsync("12/345/6789"))
            .ReturnsAsync([
                new CattleResponse
                {
                    EarTag = "UK123456789012",
                    Sex = "F"
                }
            ]);

        var submission = new Submission("REF1", "12/345/6789", "USER1");
        var animal = submission.AddAnimal("UK123456789012", dateBirth: DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-5)));

        var result = await service.ValidateSubmissionAsync(submission, TestContext.Current.CancellationToken);

        Assert.False(result.IsValid);
        Assert.Contains(animal.Errors, e => e.ErrorCode == ValidationRuleCodes.CTWS192);
    }

    [Fact]
    public async Task ValidateSubmissionAsync_DamTooYoung_TriggersCTWS202()
    {
        var (dbContext, service) = CreateService(nameof(ValidateSubmissionAsync_DamTooYoung_TriggersCTWS202));

        var calfBirthDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-10));
        var damBirthDate = calfBirthDate.AddMonths(-12); // Dam is 12 months old (< 15 months)

        _mockCadsService.Setup(c => c.GetCattleByCphAsync("12/345/6789"))
            .ReturnsAsync([
                new CattleResponse
                {
                    EarTag = "UK999999888888",
                    Sex = "F",
                    DateBirth = damBirthDate
                }
            ]);

        var submission = new Submission("REF1", "12/345/6789", "USER1");
        var animal = submission.AddAnimal(
            earTag: "UK123456789012",
            dateBirth: calfBirthDate,
            damGeneticEarTag: "UK999999888888");

        var result = await service.ValidateSubmissionAsync(submission, TestContext.Current.CancellationToken);

        Assert.False(result.IsValid);
        Assert.Contains(animal.Errors, e => e.ErrorCode == ValidationRuleCodes.CTWS202);
    }

    [Fact]
    public async Task ValidateSubmissionAsync_DamTooOld_TriggersCTWS202()
    {
        var (dbContext, service) = CreateService(nameof(ValidateSubmissionAsync_DamTooOld_TriggersCTWS202));

        var calfBirthDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-10));
        var damBirthDate = calfBirthDate.AddYears(-22); // Dam is 22 years old (> 20 years)

        _mockCadsService.Setup(c => c.GetCattleByCphAsync("12/345/6789"))
            .ReturnsAsync([
                new CattleResponse
                {
                    EarTag = "UK999999888888",
                    Sex = "F",
                    DateBirth = damBirthDate
                }
            ]);

        var submission = new Submission("REF1", "12/345/6789", "USER1");
        var animal = submission.AddAnimal(
            earTag: "UK123456789012",
            dateBirth: calfBirthDate,
            damGeneticEarTag: "UK999999888888");

        var result = await service.ValidateSubmissionAsync(submission, TestContext.Current.CancellationToken);

        Assert.False(result.IsValid);
        Assert.Contains(animal.Errors, e => e.ErrorCode == ValidationRuleCodes.CTWS202);
    }

    [Fact]
    public async Task ValidateSubmissionAsync_DamCalvedWithinCalvingInterval_TriggersCTWS200()
    {
        var (dbContext, service) = CreateService(nameof(ValidateSubmissionAsync_DamCalvedWithinCalvingInterval_TriggersCTWS200));

        var calf1Birth = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-200));
        var calf2Birth = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-10)); // 190 days apart (< 240 days)

        var submission = new Submission("REF1", "12/345/6789", "USER1");
        var calf1 = submission.AddAnimal("UK123456789001", dateBirth: calf1Birth, damGeneticEarTag: "UK999999888888");
        var calf2 = submission.AddAnimal("UK123456789002", dateBirth: calf2Birth, damGeneticEarTag: "UK999999888888");

        var result = await service.ValidateSubmissionAsync(submission, TestContext.Current.CancellationToken);

        Assert.False(result.IsValid);
        Assert.Contains(calf1.Errors, e => e.ErrorCode == ValidationRuleCodes.CTWS200);
        Assert.Contains(calf2.Errors, e => e.ErrorCode == ValidationRuleCodes.CTWS200);
    }

    [Fact]
    public async Task ValidateSubmissionByIdAsync_ValidatesAndPersistsChanges()
    {
        var (dbContext, service) = CreateService(nameof(ValidateSubmissionByIdAsync_ValidatesAndPersistsChanges));

        var submission = new Submission("REF1", "12/345/6789", "USER1");
        submission.AddAnimal("UK123456789012", dateBirth: DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-5)));

        dbContext.Set<Submission>().Add(submission);
        await dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        var result = await service.ValidateSubmissionByIdAsync(submission.Id, TestContext.Current.CancellationToken);

        Assert.True(result.IsValid);
        Assert.Equal("complete", result.Status);

        var persisted = await dbContext.Set<Submission>()
            .Include(s => s.Animals)
            .FirstOrDefaultAsync(s => s.Id == submission.Id, TestContext.Current.CancellationToken);

        Assert.NotNull(persisted);
        Assert.Equal("complete", persisted.Status);
        Assert.Equal("complete", persisted.Animals.First().Status);
    }
}
