// <copyright file="DatabaseSeederTests.cs" company="Defra">
// Copyright (c) Defra. All rights reserved.
// </copyright>

namespace Defra.Lis.Api.Tests;

using Defra.Database.Postgres;
using Defra.Lis.Database;
using Defra.Lis.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

public class DatabaseSeederTests
{
    [Fact]
    public async Task SeedDevelopmentDatabaseAsync_WhenHostIsNull_ThrowsArgumentNullException()
    {
        IHost host = null!;
        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            host.SeedDevelopmentDatabaseAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task SeedDevelopmentDataAsync_WhenDbContextIsNull_ThrowsArgumentNullException()
    {
        var logger = NullLogger<DatabaseSeederTests>.Instance;
        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            DatabaseSeeder.SeedDevelopmentDataAsync(null!, logger, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task SeedDevelopmentDatabaseAsync_WhenPostgresDbContextNotRegistered_LogsAndReturnsGracefully()
    {
        // Arrange
        var mockEnv = new Mock<IHostEnvironment>();
        mockEnv.Setup(e => e.EnvironmentName).Returns("Development");

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton(mockEnv.Object);

        // Do not register PostgresDbContext in DI
        var hostServices = services.BuildServiceProvider();
        var mockHost = new Mock<IHost>();
        mockHost.Setup(h => h.Services).Returns(hostServices);

        // Act & Assert (Should complete cleanly without throwing)
        var exception = await Record.ExceptionAsync(() =>
            mockHost.Object.SeedDevelopmentDatabaseAsync(TestContext.Current.CancellationToken));
        Assert.Null(exception);
    }

    [Fact]
    public async Task SeedDevelopmentDataAsync_WhenDatabaseIsEmpty_SeedsTestSubmissions()
    {
        // Arrange
        var logger = NullLogger<DatabaseSeederTests>.Instance;
        var dbName = "TestDb_Empty_" + Guid.NewGuid();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDbContext<PostgresDbContext>(options =>
        {
            options.UseInMemoryDatabase(dbName);
            options.ReplaceService<IModelCustomizer, CattleModelCustomizer>();
        });

        await using var serviceProvider = services.BuildServiceProvider();
        await using var context = serviceProvider.GetRequiredService<PostgresDbContext>();

        // Act
        await DatabaseSeeder.SeedDevelopmentDataAsync(
            context,
            logger,
            cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        var submissions = await context.Set<Submission>()
            .Include(s => s.Animals)
            .ThenInclude(a => a.Errors)
            .ToListAsync(TestContext.Current.CancellationToken);

        Assert.NotEmpty(submissions);
        Assert.Equal(4, submissions.Count);

        // Check CPH 12/345/6789
        var cph12Submissions = submissions.Where(s => s.CountyParishHolding == "12/345/6789").ToList();
        Assert.Equal(3, cph12Submissions.Count);

        // Check errors seeded
        var errorSub = cph12Submissions.Single(s => s.Status == Statuses.Error);
        Assert.Single(errorSub.Animals);
        Assert.Equal(2, errorSub.Animals.First().Errors.Count);

        // Check CPH 10/081/1234
        var cph10Sub = submissions.Single(s => s.CountyParishHolding == "10/081/1234");
        Assert.Equal(Statuses.Submitted, cph10Sub.Status);
        Assert.Equal("REG-MNBX4Q2A", cph10Sub.ClientReference);

        // Check sub1 details
        var sub1 = submissions.Single(s => s.ClientReference == "DEV-SUB-001");
        Assert.Equal(Statuses.Complete, sub1.Status);
        Assert.Equal("DEV-USER", sub1.SubmittedBy);
        Assert.Equal(2, sub1.Animals.Count);
        var animal1 = sub1.Animals.First(a => a.EarTag == "UK 12 3456 000001");
        Assert.Equal("Limousin", animal1.Breed);
        Assert.Equal("female", animal1.Sex);
        Assert.Equal("natural", animal1.DamType);
        Assert.Equal("UK 12 3456 000099", animal1.DamGeneticEarTag);
        Assert.Equal("UK 12 3456 000088", animal1.SireEarTag);
        Assert.Equal("Highland Bull", animal1.SireName);
        Assert.Equal(new DateOnly(2025, 3, 15), animal1.DateBirth);

        // Check sub2 details
        var sub2 = submissions.Single(s => s.ClientReference == "DEV-SUB-002");
        Assert.Equal(Statuses.Submitted, sub2.Status);
        Assert.Single(sub2.Animals);
        var animal3 = sub2.Animals.First();
        Assert.Equal("surrogate", animal3.DamType);
        Assert.Equal("UK 12 3456 000090", animal3.DamGeneticEarTag);
        Assert.Equal("UK 12 3456 000091", animal3.DamSurrogateEarTag);
        Assert.Equal("Angus Premier", animal3.SireName);

        // Check sub3 errors
        var sub3 = submissions.Single(s => s.ClientReference == "DEV-SUB-003");
        var errorAnimal = sub3.Animals.First();
        Assert.Contains(errorAnimal.Errors, e => e.ErrorCode == "ERR_DAM_NOT_FOUND" && e.ErrorText.Contains("CTS"));
        Assert.Contains(
            errorAnimal.Errors,
            e => e.ErrorCode == "ERR_INVALID_DOB" && e.ErrorText.Contains("current date"));
    }

    [Fact]
    public async Task SeedDevelopmentDataAsync_WhenDatabaseHasExistingData_DoesNotDuplicate()
    {
        // Arrange
        var logger = NullLogger<DatabaseSeederTests>.Instance;
        var dbName = "TestDb_NotEmpty_" + Guid.NewGuid();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDbContext<PostgresDbContext>(options =>
        {
            options.UseInMemoryDatabase(dbName);
            options.ReplaceService<IModelCustomizer, CattleModelCustomizer>();
        });

        await using var serviceProvider = services.BuildServiceProvider();
        await using var context = serviceProvider.GetRequiredService<PostgresDbContext>();

        var existing = new Submission("EXISTING-01", "99/999/9999", "TEST-USER", Statuses.Complete);
        await context.Set<Submission>().AddAsync(existing, TestContext.Current.CancellationToken);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        await DatabaseSeeder.SeedDevelopmentDataAsync(
            context,
            logger,
            cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        var submissions = await context.Set<Submission>().ToListAsync(TestContext.Current.CancellationToken);
        Assert.Single(submissions);
        Assert.Equal("EXISTING-01", submissions[0].ClientReference);
    }

    [Fact]
    public async Task SeedDevelopmentDatabaseAsync_WhenEnvironmentIsDevelopment_SeedsDatabase()
    {
        // Arrange
        var dbName = "TestDb_HostDev_" + Guid.NewGuid();
        var mockEnv = new Mock<IHostEnvironment>();
        mockEnv.Setup(e => e.EnvironmentName).Returns("Development");

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton(mockEnv.Object);
        services.AddDbContext<PostgresDbContext>(options =>
        {
            options.UseInMemoryDatabase(dbName);
            options.ReplaceService<IModelCustomizer, CattleModelCustomizer>();
        });

        var hostServices = services.BuildServiceProvider();
        var mockHost = new Mock<IHost>();
        mockHost.Setup(h => h.Services).Returns(hostServices);

        // Act
        await mockHost.Object.SeedDevelopmentDatabaseAsync(TestContext.Current.CancellationToken);

        // Assert
        await using var context = hostServices.GetRequiredService<PostgresDbContext>();
        var submissions = await context.Set<Submission>().ToListAsync(TestContext.Current.CancellationToken);
        Assert.NotEmpty(submissions);
    }

    [Theory]
    [InlineData("Production")]
    [InlineData("Staging")]
    [InlineData("dev")]
    [InlineData("Release")]
    public async Task SeedDevelopmentDatabaseAsync_WhenEnvironmentIsNotDevelopment_DoesNotSeed(string envName)
    {
        // Arrange
        var dbName = "TestDb_HostNonDev_" + Guid.NewGuid();
        var mockEnv = new Mock<IHostEnvironment>();
        mockEnv.Setup(e => e.EnvironmentName).Returns(envName);

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton(mockEnv.Object);
        services.AddDbContext<PostgresDbContext>(options =>
        {
            options.UseInMemoryDatabase(dbName);
            options.ReplaceService<IModelCustomizer, CattleModelCustomizer>();
        });

        var hostServices = services.BuildServiceProvider();
        var mockHost = new Mock<IHost>();
        mockHost.Setup(h => h.Services).Returns(hostServices);

        // Act
        await mockHost.Object.SeedDevelopmentDatabaseAsync(TestContext.Current.CancellationToken);

        // Assert
        await using var context = hostServices.GetRequiredService<PostgresDbContext>();
        var submissions = await context.Set<Submission>().ToListAsync(TestContext.Current.CancellationToken);
        Assert.Empty(submissions);
    }

    [Fact]
    public async Task SeedDevelopmentDatabaseAsync_WhenDatabaseConnectionFails_DoesNotThrowAndAllowsStartup()
    {
        // Arrange
        var mockEnv = new Mock<IHostEnvironment>();
        mockEnv.Setup(e => e.EnvironmentName).Returns("Development");

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton(mockEnv.Object);

        // Configure with a non-existent connection string that will fail to connect
        services.AddDbContext<PostgresDbContext>(options =>
        {
            options.UseNpgsql(
                "Host=nonexistent-host;Database=lis_test;Username=user;Password=pass;Timeout=1;CommandTimeout=1");
            options.ReplaceService<IModelCustomizer, CattleModelCustomizer>();
        });

        var hostServices = services.BuildServiceProvider();
        var mockHost = new Mock<IHost>();
        mockHost.Setup(h => h.Services).Returns(hostServices);

        // Act & Assert (Should complete without throwing exception)
        var exception = await Record.ExceptionAsync(() =>
            mockHost.Object.SeedDevelopmentDatabaseAsync(TestContext.Current.CancellationToken));
        Assert.Null(exception);
    }
}
