// <copyright file="PostgresFixture.cs" company="Defra">
// Copyright (c) Defra. All rights reserved.
// </copyright>

namespace Defra.Lis.Api.Tests.Integration;

using Defra.Lis.Database.Configurations;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Testcontainers.PostgreSql;

/// <summary>
/// A throwaway Postgres with the service's Liquibase schema applied. Needs Docker, so tests that use it carry
/// <c>Category=Integration</c> and are filtered out of the Docker image build.
/// </summary>
public sealed class PostgresFixture : IAsyncLifetime
{
    private static readonly string[] ChangelogFiles = ["schema/01/initial.sql", "schema/02/update_status_check.sql"];

    private readonly PostgreSqlContainer container = new PostgreSqlBuilder("postgres:16-alpine").Build();

    public string ConnectionString => container.GetConnectionString();

    public async ValueTask InitializeAsync()
    {
        await container.StartAsync();

        await using var connection = new NpgsqlConnection(ConnectionString);
        await connection.OpenAsync();

        // The changelog hands the tables to the DDL role that CDP creates.
        await using (var role = new NpgsqlCommand("CREATE ROLE lis_api_cattle_ddl", connection))
        {
            await role.ExecuteNonQueryAsync();
        }

        foreach (var file in ChangelogFiles)
        {
            var sql = await File.ReadAllTextAsync(Path.Combine(AppContext.BaseDirectory, "changelog", file));
            await using var command = new NpgsqlCommand(sql, connection);
            await command.ExecuteNonQueryAsync();
        }
    }

    public ValueTask DisposeAsync() => container.DisposeAsync();

    public CattleDbContext CreateDbContext() => new(
        new DbContextOptionsBuilder<CattleDbContext>().UseNpgsql(ConnectionString).Options);

    /// <summary>
    /// The production entity configurations on a plain context, standing in for PostgresDbContext (which
    /// signs in with IAM).
    /// </summary>
    public sealed class CattleDbContext(DbContextOptions<CattleDbContext> options) : DbContext(options)
    {
        protected override void OnModelCreating(ModelBuilder modelBuilder) =>
            modelBuilder.ApplyConfigurationsFromAssembly(typeof(SubmissionConfiguration).Assembly);
    }
}
