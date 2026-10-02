// <copyright file="ServiceCollectionExtensions.cs" company="Defra">
// Copyright (c) Defra. All rights reserved.
// </copyright>

namespace Defra.Lis.Database;

using Defra.Database.Postgres;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddCattleDatabaseConfigurations(this IServiceCollection services)
    {
        services.ConfigureDbContext<PostgresDbContext>((_, options) =>
        {
            options.ReplaceService<IModelCustomizer, CattleModelCustomizer>();
            options.EnableSensitiveDataLogging(false);
        });

        services.ConfigureDbContext<ReadOnlyPostgresDbContext>((_, options) =>
        {
            options.ReplaceService<IModelCustomizer, CattleModelCustomizer>();
            options.EnableSensitiveDataLogging(false);
        });

        return services;
    }
}
