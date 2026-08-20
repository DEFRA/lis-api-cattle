using Defra.Database.Postgres;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Lis.Cattle;

public static class DatabaseSeeder
{
    public static async Task SeedDevelopmentDatabaseAsync(this IHost host, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(host);

        var env = host.Services.GetRequiredService<IHostEnvironment>();
        if (!env.IsDevelopment())
        {
            return;
        }

        using var scope = host.Services.CreateScope();
        var logger = scope.ServiceProvider.GetService<ILoggerFactory>()?.CreateLogger(typeof(DatabaseSeeder));
        var dbContext = scope.ServiceProvider.GetService<PostgresDbContext>();

        if (dbContext is null)
        {
            logger?.LogWarning("PostgresDbContext not found in service provider. Skipping development database seeding.");
            return;
        }

        await SeedDevelopmentDataAsync(dbContext, logger, cancellationToken);
    }

    public static async Task SeedDevelopmentDataAsync(
        DbContext dbContext,
        ILogger? logger = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dbContext);

        var submissionsSet = dbContext.Set<Submission>();

        if (await submissionsSet.AnyAsync(cancellationToken))
        {
            logger?.LogInformation("Database already contains submission records. Skipping development seeding.");
            return;
        }

        logger?.LogInformation("Seeding development test data...");

        var testSubmissions = CreateDevelopmentSubmissions();

        await submissionsSet.AddRangeAsync(testSubmissions, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);

        logger?.LogInformation("Development test data successfully seeded ({Count} submissions).", testSubmissions.Count);
    }

    public static IReadOnlyList<Submission> CreateDevelopmentSubmissions()
    {
        var submissions = new List<Submission>();

        // Submission 1: Completed batch for holding 12/345/6789
        var sub1 = new Submission("DEV-SUB-001", "12/345/6789", "DEV-USER", "complete");
        var animal1 = sub1.AddAnimal(
            earTag: "UK 12 3456 000001",
            status: "complete",
            dateBirth: new DateOnly(2025, 3, 15),
            sex: "female",
            breed: "Limousin",
            damType: "natural",
            damGeneticEarTag: "UK 12 3456 000099",
            sireEarTag: "UK 12 3456 000088",
            sireName: "Highland Bull");

        var animal2 = sub1.AddAnimal(
            earTag: "UK 12 3456 000002",
            status: "complete",
            dateBirth: new DateOnly(2025, 4, 10),
            sex: "male",
            breed: "Hereford");
        submissions.Add(sub1);

        // Submission 2: Submitted / In-progress batch for holding 12/345/6789
        var sub2 = new Submission("DEV-SUB-002", "12/345/6789", "DEV-USER", "submitted");
        var animal3 = sub2.AddAnimal(
            earTag: "UK 12 3456 000003",
            status: "submitted",
            dateBirth: new DateOnly(2026, 1, 15),
            sex: "female",
            breed: "Aberdeen Angus",
            damType: "surrogate",
            damGeneticEarTag: "UK 12 3456 000090",
            damSurrogateEarTag: "UK 12 3456 000091",
            sireEarTag: "UK 12 3456 000080",
            sireName: "Angus Premier");
        submissions.Add(sub2);

        // Submission 3: Submission with errors for holding 12/345/6789
        var sub3 = new Submission("DEV-SUB-003", "12/345/6789", "DEV-USER", "error");
        var animal4 = sub3.AddAnimal(
            earTag: "UK 12 3456 000004",
            status: "error",
            dateBirth: new DateOnly(2026, 2, 1),
            sex: "female",
            breed: "British Blue");
        animal4.AddError("ERR_DAM_NOT_FOUND", "Genetic dam ear tag is not registered in CTS.");
        animal4.AddError("ERR_INVALID_DOB", "Date of birth cannot be later than current date.");
        submissions.Add(sub3);

        // Submission 4: Submitted registration for holding 10/081/1234
        var sub4 = new Submission("REG-MNBX4Q2A", "10/081/1234", "BE4FE", "submitted");
        sub4.AddAnimal(
            earTag: "UK 12 3456 100003",
            status: "submitted",
            dateBirth: new DateOnly(2026, 2, 1),
            sex: "female",
            breed: "Aberdeen Angus",
            damType: "surrogate",
            damGeneticEarTag: "UK 12 3456 000002",
            damSurrogateEarTag: "UK 12 3456 000003",
            sireEarTag: "UK 12 3456 000010",
            sireName: "Example sire");
        submissions.Add(sub4);

        return submissions;
    }
}
