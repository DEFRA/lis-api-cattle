// <copyright file="CtsConfiguration.cs" company="Defra">
// Copyright (c) Defra. All rights reserved.
// </copyright>

namespace Defra.Lis.Api.Configurations;

using Defra.Lis.Api.Cts;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

public static class CtsConfiguration
{
    /// <summary>
    /// Registers the CTSWeb client (<see cref="ICtsWebClient"/>), which sends Birth requests (LREG-925) and fetches
    /// their validation results (LREG-926) through the endpoint in <c>CtsWeb:Url</c>. Its settings are validated
    /// when it is first used, so the service can boot before the CTS secrets are configured.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configuration">The application configuration.</param>
    /// <returns>The same collection, so calls can be chained.</returns>
    public static IServiceCollection AddCtsServices(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<CtsWebOptions>()
            .Bind(configuration.GetSection(CtsWebOptions.SectionName))
            .ValidateDataAnnotations();

        // The client applies its own timeout.
        services.AddHttpClient<ICtsWebClient, CtsWebClient>(client => client.Timeout = Timeout.InfiniteTimeSpan);
        services.TryAddSingleton(TimeProvider.System);

        return services;
    }
}
