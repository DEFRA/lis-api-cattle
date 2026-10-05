// <copyright file="CattleDatabaseConfigurationTests.cs" company="Defra">
// Copyright (c) Defra. All rights reserved.
// </copyright>

namespace Defra.Lis.Api.Tests;

using Defra.Database.Postgres;
using Defra.Lis.Database;
using Defra.Lis.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

public class CattleDatabaseConfigurationTests
{
    [Fact]
    public void AppSettings_PostgresConfiguration_BindsCorrectly()
    {
        // Arrange
        var configuration = new ConfigurationBuilder()
            .AddJsonFile("appsettings.json")
            .Build();

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddPostgresDatabase(configuration);

        // Act
        using var serviceProvider = services.BuildServiceProvider();
        var postgresConfig = serviceProvider.GetRequiredService<PostgresConfiguration>();

        // Assert
        Assert.True(postgresConfig.UseIamAuthentication);
        Assert.Equal("identity-service-helper.cluster-cpiiyum4wb06.eu-west-2.rds.amazonaws.com", postgresConfig.ReadWriteHost);
        Assert.Equal("identity-service-helper.cluster-ro-cpiiyum4wb06.eu-west-2.rds.amazonaws.com", postgresConfig.ReadOnlyHost);
        Assert.Equal(5432, postgresConfig.Port);
    }

    [Fact]
    public void AppSettingsDevelopment_PostgresConfiguration_DisablesIamAuth()
    {
        // Arrange
        var configuration = new ConfigurationBuilder()
            .AddJsonFile("appsettings.json")
            .AddJsonFile("appsettings.Development.json")
            .Build();

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddPostgresDatabase(configuration);

        // Act
        using var serviceProvider = services.BuildServiceProvider();
        var postgresConfig = serviceProvider.GetRequiredService<PostgresConfiguration>();

        // Assert
        Assert.False(postgresConfig.UseIamAuthentication);
    }

    [Fact]
    public void PostgresDbContext_WithCattleDatabaseConfigurations_IncludesSubmissionEntitiesInModel()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDbContext<PostgresDbContext>(options =>
        {
            options.UseInMemoryDatabase("TestDb_" + Guid.NewGuid());
            options.ReplaceService<IModelCustomizer, CattleModelCustomizer>();
        });

        // Act
        using var serviceProvider = services.BuildServiceProvider();
        using var context = serviceProvider.GetRequiredService<PostgresDbContext>();

        var model = context.Model;

        var submissionType = model.FindEntityType(typeof(Submission));
        var animalType = model.FindEntityType(typeof(SubmissionAnimal));
        var errorType = model.FindEntityType(typeof(SubmissionAnimalError));

        // Assert
        Assert.NotNull(submissionType);
        Assert.Equal("submissions", submissionType.GetTableName());
        Assert.Equal("public", submissionType.GetSchema());

        Assert.NotNull(animalType);
        Assert.Equal("submission_animals", animalType.GetTableName());
        Assert.Equal("public", animalType.GetSchema());

        Assert.NotNull(errorType);
        Assert.Equal("submission_animal_errors", errorType.GetTableName());
        Assert.Equal("public", errorType.GetSchema());
        Assert.Equal("id", errorType.FindProperty(nameof(SubmissionAnimalError.Id))?.GetColumnName());
        Assert.Equal("animal_id", errorType.FindProperty(nameof(SubmissionAnimalError.AnimalId))?.GetColumnName());
        Assert.Equal("error_code", errorType.FindProperty(nameof(SubmissionAnimalError.ErrorCode))?.GetColumnName());
        Assert.Equal("error_text", errorType.FindProperty(nameof(SubmissionAnimalError.ErrorText))?.GetColumnName());
        Assert.Equal("created_at", errorType.FindProperty(nameof(SubmissionAnimalError.CreatedAt))?.GetColumnName());
        Assert.Null(errorType.FindProperty("CreatedById"));
        Assert.Null(errorType.FindProperty("DeletedAt"));

        // Verifies DbSet<Submission> does not throw InvalidOperationException
        var submissionSet = context.Set<Submission>();
        Assert.NotNull(submissionSet);
    }

    [Fact]
    public void AddCattleDatabaseConfigurations_RegistersModelCustomizerForPostgresDbContext()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDbContext<PostgresDbContext>(options =>
        {
            options.UseInMemoryDatabase("TestDb_" + Guid.NewGuid());
        });
        services.AddCattleDatabaseConfigurations();

        // Act
        using var serviceProvider = services.BuildServiceProvider();
        using var context = serviceProvider.GetRequiredService<PostgresDbContext>();

        var model = context.Model;

        // Assert
        Assert.NotNull(model.FindEntityType(typeof(Submission)));
        Assert.NotNull(model.FindEntityType(typeof(SubmissionAnimal)));
        Assert.NotNull(model.FindEntityType(typeof(SubmissionAnimalError)));
    }

    [Fact]
    public void AddCattleDatabaseConfigurations_DisablesSensitiveDataLogging_ForPostgresDbContext()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDbContext<PostgresDbContext>(options =>
        {
            options.UseInMemoryDatabase("TestDb_" + Guid.NewGuid());
            options.EnableSensitiveDataLogging();
        });
        services.AddCattleDatabaseConfigurations();

        // Act
        using var serviceProvider = services.BuildServiceProvider();
        using var context = serviceProvider.GetRequiredService<PostgresDbContext>();
        var dbOptions = context.GetService<IDbContextOptions>();
        var coreOptions = dbOptions.FindExtension<CoreOptionsExtension>();

        // Assert
        Assert.NotNull(coreOptions);
        Assert.False(coreOptions.IsSensitiveDataLoggingEnabled);
    }

    [Fact]
    public void AddCattleDatabaseConfigurations_DisablesSensitiveDataLogging_ForReadOnlyPostgresDbContext()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDbContext<ReadOnlyPostgresDbContext>(options =>
        {
            options.UseInMemoryDatabase("TestDb_" + Guid.NewGuid());
            options.EnableSensitiveDataLogging();
        });
        services.AddCattleDatabaseConfigurations();

        // Act
        using var serviceProvider = services.BuildServiceProvider();
        using var context = serviceProvider.GetRequiredService<ReadOnlyPostgresDbContext>();
        var dbOptions = context.GetService<IDbContextOptions>();
        var coreOptions = dbOptions.FindExtension<CoreOptionsExtension>();

        // Assert
        Assert.NotNull(coreOptions);
        Assert.False(coreOptions.IsSensitiveDataLoggingEnabled);
        Assert.NotNull(context.Model.FindEntityType(typeof(Submission)));
    }
}
