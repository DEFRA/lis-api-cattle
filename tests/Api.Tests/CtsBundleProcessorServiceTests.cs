using Lis.Cattle.Interfaces;
using Lis.Cattle.Models;
using Lis.Cattle.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Moq;

namespace Lis.Cattle;

public class CtsBundleProcessorServiceTests
{
    private readonly Mock<ICtsService> _mockCtsService;
    private readonly TestDbContext _context;

    public CtsBundleProcessorServiceTests()
    {
        _mockCtsService = new Mock<ICtsService>();

        var dbName = Guid.NewGuid().ToString();
        var options = new DbContextOptionsBuilder<TestDbContext>()
            .UseInMemoryDatabase(dbName)
            .Options;

        _context = new TestDbContext(options);
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

    [Fact]
    public async Task ProcessPendingBundlesAsync_WhenBundleIsSubmitted_SubmitsAnimalsAndMarksProcessing()
    {
        // Arrange
        var submission = new Submission("REF1", "10/100/1000", "testUser", status: "submitted");
        var animal1 = submission.AddAnimal("UK100001", status: "submitted");
        var animal2 = submission.AddAnimal("UK100002", status: "submitted");

        _context.Set<Submission>().Add(submission);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        _mockCtsService.Setup(s => s.SubmitAnimalRegistrationAsync(It.IsAny<SubmissionAnimal>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CtsAnimalStatusResponse { Status = "processing" });

        var options = Options.Create(new CtsPollingJobOptions { BatchSize = 10 });
        var service = new CtsBundleProcessorService(_mockCtsService.Object, _context, options);

        // Act
        await service.ProcessPendingBundlesAsync(TestContext.Current.CancellationToken);

        // Assert
        var updated = await _context.Set<Submission>().Include(s => s.Animals).FirstAsync(s => s.Id == submission.Id, TestContext.Current.CancellationToken);
        Assert.Equal("processing", updated.Status);
        Assert.All(updated.Animals, a => Assert.Equal("processing", a.Status));
        _mockCtsService.Verify(s => s.SubmitAnimalRegistrationAsync(It.IsAny<SubmissionAnimal>(), It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    [Fact]
    public async Task ProcessPendingBundlesAsync_WhenProcessingBundleAnimalsComeBackClean_CompletesBundle()
    {
        // Arrange
        var submission = new Submission("REF2", "10/100/1000", "testUser", status: "processing");
        var animal1 = submission.AddAnimal("UK100001", status: "processing");
        var animal2 = submission.AddAnimal("UK100002", status: "processing");

        _context.Set<Submission>().Add(submission);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        _mockCtsService.Setup(s => s.CheckAnimalStatusAsync(It.IsAny<string>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CtsAnimalStatusResponse { Status = "clean" });

        var options = Options.Create(new CtsPollingJobOptions { BatchSize = 10 });
        var service = new CtsBundleProcessorService(_mockCtsService.Object, _context, options);

        // Act
        await service.ProcessPendingBundlesAsync(TestContext.Current.CancellationToken);

        // Assert
        var updated = await _context.Set<Submission>().Include(s => s.Animals).FirstAsync(s => s.Id == submission.Id, TestContext.Current.CancellationToken);
        Assert.Equal("complete", updated.Status);
        Assert.All(updated.Animals, a => Assert.Equal("complete", a.Status));
    }

    [Fact]
    public async Task ProcessPendingBundlesAsync_WhenProcessingBundleHasErrors_SetsAnimalAndBundleError()
    {
        // Arrange
        var submission = new Submission("REF3", "10/100/1000", "testUser", status: "processing");
        var animal1 = submission.AddAnimal("UK100001", status: "processing");
        var animal2 = submission.AddAnimal("UK100002", status: "processing");

        _context.Set<Submission>().Add(submission);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        _mockCtsService.Setup(s => s.CheckAnimalStatusAsync("UK100001", animal1.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CtsAnimalStatusResponse { Status = "clean" });

        _mockCtsService.Setup(s => s.CheckAnimalStatusAsync("UK100002", animal2.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CtsAnimalStatusResponse
            {
                Status = "error",
                Errors = [new CtsErrorResponse { ErrorCode = "ERR_AGE", ErrorText = "Animal age exceeds maximum" }]
            });

        var options = Options.Create(new CtsPollingJobOptions { BatchSize = 10 });
        var service = new CtsBundleProcessorService(_mockCtsService.Object, _context, options);

        // Act
        await service.ProcessPendingBundlesAsync(TestContext.Current.CancellationToken);

        // Assert
        var updated = await _context.Set<Submission>().Include(s => s.Animals).ThenInclude(a => a.Errors).FirstAsync(s => s.Id == submission.Id, TestContext.Current.CancellationToken);
        Assert.Equal("error", updated.Status);

        var a1 = updated.Animals.First(a => a.EarTag == "UK100001");
        Assert.Equal("complete", a1.Status);

        var a2 = updated.Animals.First(a => a.EarTag == "UK100002");
        Assert.Equal("error", a2.Status);
        Assert.Single(a2.Errors);
        Assert.Equal("ERR_AGE", a2.Errors.First().ErrorCode);
    }

    [Fact]
    public async Task ProcessPendingBundlesAsync_WhenErrorBundleErrorsAreResolved_MarksBundleComplete()
    {
        // Arrange
        var submission = new Submission("REF4", "10/100/1000", "testUser", status: "error");
        var animal1 = submission.AddAnimal("UK100001", status: "error");
        animal1.AddError("OLD_ERR", "Previous error");

        _context.Set<Submission>().Add(submission);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        _mockCtsService.Setup(s => s.CheckAnimalStatusAsync("UK100001", animal1.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CtsAnimalStatusResponse { Status = "clean" });

        var options = Options.Create(new CtsPollingJobOptions { BatchSize = 10 });
        var service = new CtsBundleProcessorService(_mockCtsService.Object, _context, options);

        // Act
        await service.ProcessPendingBundlesAsync(TestContext.Current.CancellationToken);

        // Assert
        var updated = await _context.Set<Submission>().Include(s => s.Animals).ThenInclude(a => a.Errors).FirstAsync(s => s.Id == submission.Id, TestContext.Current.CancellationToken);
        Assert.Equal("complete", updated.Status);
        Assert.Equal("complete", updated.Animals.First().Status);
        Assert.Empty(updated.Animals.First().Errors);
    }

    [Fact]
    public async Task ProcessPendingBundlesAsync_WhenNoPendingBundles_CompletesGracefully()
    {
        var options = Options.Create(new CtsPollingJobOptions { BatchSize = 10 });
        var service = new CtsBundleProcessorService(_mockCtsService.Object, _context, options);

        await service.ProcessPendingBundlesAsync(TestContext.Current.CancellationToken);

        _mockCtsService.Verify(s => s.SubmitAnimalRegistrationAsync(It.IsAny<SubmissionAnimal>(), It.IsAny<CancellationToken>()), Times.Never);
        _mockCtsService.Verify(s => s.CheckAnimalStatusAsync(It.IsAny<string>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
