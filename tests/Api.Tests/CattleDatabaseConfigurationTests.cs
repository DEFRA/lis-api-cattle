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
        var configuration = new ConfigurationBuilder()
            .AddJsonFile("appsettings.json")
            .Build();

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddPostgresDatabase(configuration);

        using var serviceProvider = services.BuildServiceProvider();
        var postgresConfig = serviceProvider.GetRequiredService<PostgresConfiguration>();

        Assert.False(postgresConfig.UseIamAuthentication);
        Assert.Equal("identity-service-helper.cluster-cpiiyum4wb06.eu-west-2.rds.amazonaws.com", postgresConfig.ReadWriteHost);
        Assert.Equal("identity-service-helper.cluster-ro-cpiiyum4wb06.eu-west-2.rds.amazonaws.com", postgresConfig.ReadOnlyHost);
        Assert.Equal(5432, postgresConfig.Port);
    }

    [Fact]
    public void PostgresDbContext_WithCattleDatabaseConfigurations_IncludesSubmissionEntitiesInModel()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDbContext<PostgresDbContext>(options =>
        {
            options.UseInMemoryDatabase("TestDb_" + Guid.NewGuid());
            options.ReplaceService<IModelCustomizer, CattleModelCustomizer>();
        });

        using var serviceProvider = services.BuildServiceProvider();
        using var context = serviceProvider.GetRequiredService<PostgresDbContext>();

        var model = context.Model;

        var submissionType = model.FindEntityType(typeof(Submission));
        var animalType = model.FindEntityType(typeof(SubmissionAnimal));
        var errorType = model.FindEntityType(typeof(SubmissionAnimalError));

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
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDbContext<PostgresDbContext>(options =>
        {
            options.UseInMemoryDatabase("TestDb_" + Guid.NewGuid());
        });
        services.AddCattleDatabaseConfigurations();

        using var serviceProvider = services.BuildServiceProvider();
        using var context = serviceProvider.GetRequiredService<PostgresDbContext>();

        var model = context.Model;

        Assert.NotNull(model.FindEntityType(typeof(Submission)));
        Assert.NotNull(model.FindEntityType(typeof(SubmissionAnimal)));
        Assert.NotNull(model.FindEntityType(typeof(SubmissionAnimalError)));
    }
}
