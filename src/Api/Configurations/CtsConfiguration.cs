// <copyright file="CtsConfiguration.cs" company="Defra">
// Copyright (c) Defra. All rights reserved.
// </copyright>

namespace Defra.Lis.Api.Configurations;

using Defra.Lis.Api.Cts;
using Defra.Lis.Api.Interfaces;
using Defra.Lis.Api.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

public static class CtsConfiguration
{
    public const string UseFakeKey = "CtsApi:UseFake";

    /// <summary>
    /// Registers the CTS clients. The per-animal status client (<see cref="ICtsService"/>) only has a fake so far,
    /// so the service refuses to start when <c>CtsApi:UseFake</c> is false rather than use the fake without saying
    /// so. Birth requests go through <see cref="ICtsWebClient"/> to the endpoint in <c>CtsWeb:Url</c>.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configuration">The application configuration.</param>
    /// <returns>The service collection.</returns>
    public static IServiceCollection AddCtsServices(this IServiceCollection services, IConfiguration configuration)
    {
        if (!configuration.GetValue(UseFakeKey, true))
        {
            throw new InvalidOperationException(
                $"{UseFakeKey} is false, but no real CTS client is implemented yet. Set {UseFakeKey} to true.");
        }

        services.AddSingleton<ICtsService, FakeCtsService>();

        // The CTSWeb client that sends Birth requests (LREG-925). Its settings are validated when it is first used,
        // so the service can boot before the CTS secrets are configured. The client applies its own timeout.
        services.AddOptions<CtsWebOptions>()
            .Bind(configuration.GetSection(CtsWebOptions.SectionName))
            .ValidateDataAnnotations();
        services.AddHttpClient<ICtsWebClient, CtsWebClient>(client => client.Timeout = Timeout.InfiniteTimeSpan);

        return services;
    }
}
