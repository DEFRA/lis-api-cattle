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
using Moq;

public class DatabaseSeederTests
{
    [Fact]
    public async Task SeedDevelopmentDataAsync_WhenDatabaseIsEmpty_SeedsTestSubmissions()
    {
        // Arrange
        var dbName = "TestDb_Empty_" + Guid.NewGuid();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDbContext<PostgresDbContext>(options =>
        {
            options.UseInMemoryDatabase(dbName);
            options.ReplaceService<IModelCustomizer, CattleModelCustomizer>();
        });

        using var serviceProvider = services.BuildServiceProvider();
        using var context = serviceProvider.GetRequiredService<PostgresDbContext>();

        // Act
        await DatabaseSeeder.SeedDevelopmentDataAsync(context, cancellationToken: TestContext.Current.CancellationToken);

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
    }

    [Fact]
    public async Task SeedDevelopmentDataAsync_WhenDatabaseHasExistingData_DoesNotDuplicate()
    {
        // Arrange
        var dbName = "TestDb_NotEmpty_" + Guid.NewGuid();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDbContext<PostgresDbContext>(options =>
        {
            options.UseInMemoryDatabase(dbName);
            options.ReplaceService<IModelCustomizer, CattleModelCustomizer>();
        });

        using var serviceProvider = services.BuildServiceProvider();
        using var context = serviceProvider.GetRequiredService<PostgresDbContext>();

        var existing = new Submission("EXISTING-01", "99/999/9999", "TEST-USER", Statuses.Complete);
        await context.Set<Submission>().AddAsync(existing, TestContext.Current.CancellationToken);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        await DatabaseSeeder.SeedDevelopmentDataAsync(context, cancellationToken: TestContext.Current.CancellationToken);

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
        using var context = hostServices.GetRequiredService<PostgresDbContext>();
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
        using var context = hostServices.GetRequiredService<PostgresDbContext>();
        var submissions = await context.Set<Submission>().ToListAsync(TestContext.Current.CancellationToken);
        Assert.Empty(submissions);
    }
}
