// <copyright file="DatabaseHealthCheck.cs" company="Defra">
// Copyright (c) Defra. All rights reserved.
// </copyright>

namespace Defra.Lis.Api.Services.Health;

using Defra.Lis.Api.Interfaces;
using Microsoft.Extensions.Diagnostics.HealthChecks;

public class DatabaseHealthCheck : IHealthCheck
{
    private readonly IHealthService healthService;

    public DatabaseHealthCheck(IHealthService healthService)
    {
        this.healthService = healthService ?? throw new ArgumentNullException(nameof(healthService));
    }

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        var result = await healthService.CheckDatabaseHealthAsync(cancellationToken);

        return result.Status switch
        {
            nameof(HealthStatus.Healthy) => HealthCheckResult.Healthy(result.Description, result.Data),
            nameof(HealthStatus.Degraded) => HealthCheckResult.Degraded(result.Description, data: result.Data),
            _ => HealthCheckResult.Unhealthy(result.Description, data: result.Data),
        };
    }
}
