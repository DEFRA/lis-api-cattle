// <copyright file="HealthService.logger.cs" company="Defra">
// Copyright (c) Defra. All rights reserved.
// </copyright>

namespace Defra.Lis.Api.Services;

using Microsoft.Extensions.Logging;

public partial class HealthService
{
    [LoggerMessage(LogLevel.Warning, "Database health check timed out after {Timeout}")]
    partial void LogDatabaseHealthCheckTimedOut(TimeSpan timeout, Exception exception);

    [LoggerMessage(LogLevel.Error, "Database health check failed")]
    partial void LogDatabaseHealthCheckFailed(Exception exception);

    [LoggerMessage(LogLevel.Warning, "Queue health check timed out after {Timeout}")]
    partial void LogQueueHealthCheckTimedOut(TimeSpan timeout, Exception exception);

    [LoggerMessage(LogLevel.Error, "Queue health check failed for SQS")]
    partial void LogQueueHealthCheckFailed(Exception exception);

    [LoggerMessage(LogLevel.Warning, "Quartz health check timed out after {Timeout}")]
    partial void LogQuartzHealthCheckTimedOut(TimeSpan timeout, Exception exception);

    [LoggerMessage(LogLevel.Error, "Quartz health check failed")]
    partial void LogQuartzHealthCheckFailed(Exception exception);
}
