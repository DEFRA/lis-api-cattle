using Defra.Database.Postgres;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace Lis.Cattle;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddCattleDatabaseConfigurations(this IServiceCollection services)
    {
        services.ConfigureDbContext<PostgresDbContext>((_, options) =>
        {
            options.ReplaceService<IModelCustomizer, CattleModelCustomizer>();
        });

        services.ConfigureDbContext<ReadOnlyPostgresDbContext>((_, options) =>
        {
            options.ReplaceService<IModelCustomizer, CattleModelCustomizer>();
        });

        return services;
    }
}