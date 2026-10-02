// <copyright file="IHealthService.cs" company="Defra">
// Copyright (c) Defra. All rights reserved.
// </copyright>

namespace Defra.Lis.Api.Interfaces;

using Defra.Lis.Api.Models;

public interface IHealthService
{
    Task<HealthCheckResponse> CheckHealthAsync(CancellationToken cancellationToken = default);

    Task<ComponentHealthResult> CheckDatabaseHealthAsync(CancellationToken cancellationToken = default);

    Task<ComponentHealthResult> CheckQueueHealthAsync(CancellationToken cancellationToken = default);

    Task<ComponentHealthResult> CheckQuartzHealthAsync(CancellationToken cancellationToken = default);
}
