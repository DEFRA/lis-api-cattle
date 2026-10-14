// <copyright file="GatewayApiKey.cs" company="Defra">
// Copyright (c) Defra. All rights reserved.
// </copyright>

namespace Defra.Lis.Api.Services.Upstream;

using Defra.Livestock.Sdk.Api.Strategies.Abstractions.Operations.Http.Rest;

/// <summary>
/// Adds the CDP developer API key needed to reach KRDS and CADS through the ephemeral-protected gateway
/// from a developer machine. Inside CDP the services call each other directly and no key is configured.
/// </summary>
public static class GatewayApiKey
{
    public const string HeaderName = "x-api-key";

    /// <summary>
    /// Sends <see cref="HeaderName"/> when a gateway key is configured; otherwise leaves the request unchanged.
    /// </summary>
    /// <typeparam name="TService">The service the strategy belongs to.</typeparam>
    /// <param name="strategy">The REST strategy being built.</param>
    /// <param name="apiKey">The configured gateway key, if any.</param>
    /// <returns>The same strategy, for chaining.</returns>
    public static IRestStrategy<TService> WithGatewayApiKey<TService>(this IRestStrategy<TService> strategy, string? apiKey)
        where TService : class
    {
        ArgumentNullException.ThrowIfNull(strategy);

        return string.IsNullOrWhiteSpace(apiKey) ? strategy : strategy.WithHeader(HeaderName, apiKey);
    }
}
