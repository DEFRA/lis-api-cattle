// <copyright file="StrategyConfiguration.cs" company="Defra">
// Copyright (c) Defra. All rights reserved.
// </copyright>

namespace Defra.Lis.Api.Configurations;

using Defra.Lis.Api.Services;
using Defra.Lis.Api.Services.Upstream;
using Defra.Livestock.Sdk.Api.Strategies;

public static class StrategyConfiguration
{
    public static IServiceCollection AddStrategies(this IServiceCollection services)
    {
        services.AddStrategyFramework();

        services
            .AddRestStrategyFactory<CadsService, CadsApiOptions>((factory, context, options) =>
            {
                factory
                    .WithDefaultLogger(context.Logger)
                    .WithDefaultApiDescription("CADS bovine animals API")
                    .WithDefaultBaseUrl(options.Value.BaseUrl)
                    .WithDefaultApiKey(
                        () => !string.IsNullOrWhiteSpace(options.Value.GatewayApiKey),
                        options.Value.GatewayApiKey!)
                    .WithDefaultBasicAuth(options.Value.ClientId, options.Value.ClientSecret)
                    .WithDefaultJsonSerializerOptions(UpstreamJson.Options);
            })
            .AddRestStrategyFactory<KrdsService, KrdsApiOptions>((factory, context, options) =>
            {
                factory.WithDefaultLogger(context.Logger)
                    .WithDefaultApiDescription("KRDS API")
                    .WithDefaultBaseUrl(options.Value.BaseUrl)
                    .WithDefaultApiKey(
                        () => !string.IsNullOrWhiteSpace(options.Value.GatewayApiKey),
                        options.Value.GatewayApiKey!)
                    .WithDefaultBasicAuth(options.Value.ClientId, options.Value.ClientSecret)
                    .WithDefaultJsonSerializerOptions(UpstreamJson.Options);
            });

        services
            .AddCircuitBreakerStrategyFactory<CadsService, CadsApiOptions>((factory, context, policyFactory, options) =>
            {
                factory
                    .WithDefaultLogger(context.Logger)
                    .WithDefaultPolicy(policyFactory.CreateHttpPolicy())
                    .WithDefaultSamplingDuration(options.Value.CircuitBreakerSamplingDuration)
                    .WithDefaultDurationOfBreak(options.Value.CircuitBreakerDurationOfBreak)
                    .WithDefaultMinimumThroughput(options.Value.CircuitBreakerMinimumThroughput)
                    .When(
                        options.Value.CircuitBreakerConsecutiveFailureThreshold is > 0,
                        sf => sf.WithDefaultConsecutiveFailureThreshold(
                            options.Value.CircuitBreakerConsecutiveFailureThreshold!.Value))
                    .When(
                        options.Value.CircuitBreakerFailureRatio is > 0,
                        f => f.WithDefaultFailureRatio(
                            options.Value.CircuitBreakerFailureRatio!.Value));
            })
            .AddCircuitBreakerStrategyFactory<KrdsService, KrdsApiOptions>((factory, context, policyFactory, options) =>
            {
                factory
                    .WithDefaultLogger(context.Logger)
                    .WithDefaultPolicy(policyFactory.CreateHttpPolicy())
                    .WithDefaultSamplingDuration(options.Value.CircuitBreakerSamplingDuration)
                    .WithDefaultDurationOfBreak(options.Value.CircuitBreakerDurationOfBreak)
                    .WithDefaultMinimumThroughput(options.Value.CircuitBreakerMinimumThroughput)
                    .When(
                        options.Value.CircuitBreakerConsecutiveFailureThreshold is > 0,
                        sf => sf.WithDefaultConsecutiveFailureThreshold(
                            options.Value.CircuitBreakerConsecutiveFailureThreshold!.Value))
                    .When(
                        options.Value.CircuitBreakerFailureRatio is > 0,
                        f => f.WithDefaultFailureRatio(
                            options.Value.CircuitBreakerFailureRatio!.Value));
            });

        return services;
    }
}
