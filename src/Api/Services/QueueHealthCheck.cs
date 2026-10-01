// <copyright file="QueueHealthCheck.cs" company="Defra">
// Copyright (c) Defra. All rights reserved.
// </copyright>

namespace Defra.Lis.Api.Services;

using Defra.Lis.Api.Interfaces;
using Microsoft.Extensions.Diagnostics.HealthChecks;

public class QueueHealthCheck : IHealthCheck
{
    private readonly IHealthService healthService;

    public QueueHealthCheck(IHealthService healthService)
    {
        this.healthService = healthService ?? throw new ArgumentNullException(nameof(healthService));
    }

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        var result = await this.healthService.CheckQueueHealthAsync(cancellationToken);

        if (result.Status == HealthStatus.Healthy.ToString())
        {
            return HealthCheckResult.Healthy(result.Description, result.Data);
        }

        if (result.Status == HealthStatus.Degraded.ToString())
        {
            return HealthCheckResult.Degraded(result.Description, data: result.Data);
        }

        return HealthCheckResult.Unhealthy(result.Description, data: result.Data);
    }
}
