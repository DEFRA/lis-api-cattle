using Defra.Database.Postgres;
using Lis.Cattle.Configurations;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace Lis.Cattle;

public class CattleModelCustomizer : ModelCustomizer
{
    public CattleModelCustomizer(ModelCustomizerDependencies dependencies)
        : base(dependencies)
    {
    }

    public override void Customize(ModelBuilder modelBuilder, DbContext context)
    {
        base.Customize(modelBuilder, context);

        if (context is PostgresDbContext or ReadOnlyPostgresDbContext)
        {
            modelBuilder.ApplyConfigurationsFromAssembly(typeof(SubmissionConfiguration).Assembly);
        }
    }
}