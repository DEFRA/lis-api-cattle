// <copyright file="CattleModelCustomizer.cs" company="Defra">
// Copyright (c) Defra. All rights reserved.
// </copyright>

namespace Defra.Lis.Database;

using Defra.Database.Postgres;
using Defra.Lis.Database.Configurations;
using Microsoft.EntityFrameworkCore.Infrastructure;

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
