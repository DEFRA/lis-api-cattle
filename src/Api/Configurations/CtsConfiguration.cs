// <copyright file="CtsConfiguration.cs" company="Defra">
// Copyright (c) Defra. All rights reserved.
// </copyright>

namespace Defra.Lis.Api.Configurations;

using Defra.Lis.Api.Interfaces;
using Defra.Lis.Api.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

public static class CtsConfiguration
{
    public const string UseFakeKey = "CtsApi:UseFake";

    /// <summary>
    /// Registers the CTS client. Only the fake exists so far, so the service refuses to start when
    /// <c>CtsApi:UseFake</c> is false rather than send submissions to the fake without saying so.
    /// </summary>
    /// <returns>The service collection.</returns>
    public static IServiceCollection AddCtsServices(this IServiceCollection services, IConfiguration configuration)
    {
        if (!configuration.GetValue(UseFakeKey, true))
        {
            throw new InvalidOperationException(
                $"{UseFakeKey} is false, but no real CTS client is implemented yet. Set {UseFakeKey} to true.");
        }

        services.AddSingleton<ICtsService, FakeCtsService>();

        return services;
    }
}
