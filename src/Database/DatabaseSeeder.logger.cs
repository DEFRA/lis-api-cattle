// <copyright file="DatabaseSeeder.logger.cs" company="Defra">
// Copyright (c) Defra. All rights reserved.
// </copyright>

namespace Defra.Lis.Database;

using Microsoft.Extensions.Logging;

public static partial class DatabaseSeeder
{
    [LoggerMessage(LogLevel.Warning, "PostgresDbContext not found in service provider. Skipping development database seeding.")]
    static partial void LogPostgresdbcontextNotFoundInServiceProviderSkippingDevelopmentDatabaseSeeding(ILogger logger);

    [LoggerMessage(LogLevel.Information, "Database already contains submission records. Skipping development seeding.")]
    static partial void LogDatabaseAlreadyContainsSubmissionRecordsSkippingDevelopmentSeeding(ILogger logger);

    [LoggerMessage(LogLevel.Information, "Seeding development test data...")]
    static partial void LogSeedingDevelopmentTestData(ILogger logger);

    [LoggerMessage(LogLevel.Information, "Development test data successfully seeded ({Count} submissions).")]
    static partial void LogDevelopmentTestDataSuccessfullySeededCountSubmissions(ILogger logger, int count);
}
