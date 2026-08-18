using Defra.Database.Postgres;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace Lis.Cattle;

public class CattleDatabaseConfigurationTests
{
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

        Assert.NotNull(model.FindEntityType(typeof(Submission)));
        Assert.NotNull(model.FindEntityType(typeof(SubmissionAnimal)));
        Assert.NotNull(model.FindEntityType(typeof(SubmissionAnimalError)));

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