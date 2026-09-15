// <copyright file="RestStrategyTestFactory.cs" company="Defra">
// Copyright (c) Defra. All rights reserved.
// </copyright>

namespace Defra.Lis.Api.Tests.Upstream;

using Defra.Livestock.Sdk.Api.Strategies;
using Defra.Livestock.Sdk.Api.Strategies.Abstractions.Operations;
using Defra.Livestock.Sdk.Api.Strategies.Abstractions.Operations.Http.Rest.Client;
using Defra.Livestock.Sdk.Api.Strategies.Operations.Http.Rest.Client;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

/// <summary>
/// Builds a real strategies-SDK REST factory whose HTTP transport is a <see cref="StubHttpMessageHandler"/>.
/// </summary>
public static class RestStrategyTestFactory
{
    public static IRestStrategyFactory<TService> Create<TService>(StubHttpMessageHandler handler)
        where TService : class
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddTransient<IRestHttpClient>(_ => new RestHttpClient(new HttpClient(handler), NullLogger<RestHttpClient>.Instance));
        services.AddRestStrategyFactory<TService>();

        return services.BuildServiceProvider().GetRequiredService<IRestStrategyFactory<TService>>();
    }
}
