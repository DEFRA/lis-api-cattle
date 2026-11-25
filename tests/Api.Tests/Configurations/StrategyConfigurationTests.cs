// <copyright file="StrategyConfigurationTests.cs" company="Defra">
// Copyright (c) Defra. All rights reserved.
// </copyright>

namespace Defra.Lis.Api.Tests.Configurations;

using System.Net;
using System.Net.Http.Headers;
using System.Text;
using Defra.Lis.Api.Configurations;
using Defra.Lis.Api.Services;
using Defra.Lis.Api.Tests.Upstream;
using Defra.Livestock.Sdk.Api.Strategies.Abstractions.Operations;
using Defra.Livestock.Sdk.Api.Strategies.Abstractions.Operations.Http.Rest.Client;
using Defra.Livestock.Sdk.Api.Strategies.Operations.Http.Rest.Client;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

public class StrategyConfigurationTests
{
    private const string CadsTestClient = "cads-client";
    private const string CadsTestPassphrase = "cads-passphrase";
    private const string CadsTestGatewayKey = "cads-gateway-key";
    private const string KrdsTestClient = "krds-client";
    private const string KrdsTestPassphrase = "krds-passphrase";
    private const string KrdsTestGatewayKey = "krds-gateway-key";

    [Fact]
    public void AddStrategies_ReturnsSameServiceCollectionInstance()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        var returned = services.AddStrategies();

        // Assert
        Assert.Same(services, returned);
    }

    [Fact]
    public void AddStrategies_RegistersRestAndCircuitBreakerStrategyFactories()
    {
        // Arrange
        var services = CreateServices(
            new CadsApiOptions
            {
                BaseUrl = "http://cads.test/", ClientId = CadsTestClient, ClientSecret = CadsTestPassphrase,
            },
            new KrdsApiOptions
            {
                BaseUrl = "http://krds.test/", ClientId = KrdsTestClient, ClientSecret = KrdsTestPassphrase,
            },
            new StubHttpMessageHandler());

        // Act
        services.AddStrategies();

        using var provider = services.BuildServiceProvider();

        // Assert
        Assert.NotNull(provider.GetService<IRestStrategyFactory<CadsService>>());
        Assert.NotNull(provider.GetService<IRestStrategyFactory<KrdsService>>());
        Assert.NotNull(provider.GetService<ICircuitBreakerStrategyFactory<CadsService>>());
        Assert.NotNull(provider.GetService<ICircuitBreakerStrategyFactory<KrdsService>>());
    }

    [Fact]
    public async Task AddStrategies_ConfiguresCadsRestStrategyFactory_WithExpectedOptionsAndAuth()
    {
        // Arrange
        var handler = new StubHttpMessageHandler().RespondWith(HttpStatusCode.OK, """{"status":"ok"}""");

        var cadsOptions = new CadsApiOptions
        {
            BaseUrl = "http://cads-api.test/",
            ClientId = CadsTestClient,
            ClientSecret = CadsTestPassphrase,
            GatewayApiKey = CadsTestGatewayKey,
        };

        var services = CreateServices(cadsOptions, new KrdsApiOptions(), handler);

        services.AddStrategies();

        await using var provider = services.BuildServiceProvider();
        var factory = provider.GetRequiredService<IRestStrategyFactory<CadsService>>();

        var strategy = factory.BuildRestStrategy();

        // Act
        var result = await strategy
            .WithActionDescription("Test CADS")
            .WithResourceUrl("api/v1/test")
            .WithCancellationToken(CancellationToken.None)
            .WithGet()
            .Execute<object>();

        // Assert
        Assert.NotNull(result);

        var request = Assert.Single(handler.Requests);

        Assert.Equal("http://cads-api.test/api/v1/test", request.RequestUri!.ToString());

        var expectedAuth = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{CadsTestClient}:{CadsTestPassphrase}"));

        Assert.Equal(new AuthenticationHeaderValue("Basic", expectedAuth), request.Headers.Authorization);
        Assert.True(request.Headers.TryGetValues("x-api-key", out var apiKeyValues));
        Assert.Equal([CadsTestGatewayKey], apiKeyValues);
    }

    [Fact]
    public async Task AddStrategies_ConfiguresCadsRestStrategyFactory_WithoutApiKey_WhenGatewayKeyIsEmpty()
    {
        // Arrange
        var handler = new StubHttpMessageHandler().RespondWith(HttpStatusCode.OK, """{"status":"ok"}""");

        var cadsOptions = new CadsApiOptions
        {
            BaseUrl = "http://cads-api.test/",
            ClientId = "cads-client-id",
            ClientSecret = CadsTestPassphrase,
            GatewayApiKey = null,
        };

        var services = CreateServices(cadsOptions, new KrdsApiOptions(), handler);

        services.AddStrategies();

        await using var provider = services.BuildServiceProvider();

        var factory = provider.GetRequiredService<IRestStrategyFactory<CadsService>>();

        var strategy = factory.BuildRestStrategy();

        // Act
        await strategy
            .WithActionDescription("Test CADS without API key")
            .WithResourceUrl("api/v1/test")
            .WithCancellationToken(CancellationToken.None)
            .WithGet()
            .Execute<object>();

        // Assert
        var request = Assert.Single(handler.Requests);

        Assert.False(request.Headers.Contains("x-api-key"));
    }

    [Fact]
    public async Task AddStrategies_ConfiguresKrdsRestStrategyFactory_WithExpectedOptionsAndAuth()
    {
        // Arrange
        var handler = new StubHttpMessageHandler().RespondWith(HttpStatusCode.OK, """{"status":"ok"}""");

        var krdsOptions = new KrdsApiOptions
        {
            BaseUrl = "http://krds-api.test/",
            ClientId = KrdsTestClient,
            ClientSecret = KrdsTestPassphrase,
            GatewayApiKey = KrdsTestGatewayKey,
        };

        var services = CreateServices(new CadsApiOptions(), krdsOptions, handler);

        services.AddStrategies();

        await using var provider = services.BuildServiceProvider();

        var factory = provider.GetRequiredService<IRestStrategyFactory<KrdsService>>();

        var strategy = factory.BuildRestStrategy();

        // Act
        var result = await strategy
            .WithActionDescription("Test KRDS")
            .WithResourceUrl("api/v2/test")
            .WithCancellationToken(CancellationToken.None)
            .WithGet()
            .Execute<object>();

        // Assert
        Assert.NotNull(result);

        var request = Assert.Single(handler.Requests);

        Assert.Equal("http://krds-api.test/api/v2/test", request.RequestUri!.ToString());

        var expectedAuth = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{KrdsTestClient}:{KrdsTestPassphrase}"));

        Assert.Equal(new AuthenticationHeaderValue("Basic", expectedAuth), request.Headers.Authorization);
        Assert.True(request.Headers.TryGetValues("x-api-key", out var apiKeyValues));
        Assert.Equal([KrdsTestGatewayKey], apiKeyValues);
    }

    [Fact]
    public async Task AddStrategies_ConfiguresKrdsRestStrategyFactory_WithoutApiKey_WhenGatewayKeyIsEmpty()
    {
        // Arrange
        var handler = new StubHttpMessageHandler().RespondWith(HttpStatusCode.OK, """{"status":"ok"}""");

        var krdsOptions = new KrdsApiOptions
        {
            BaseUrl = "http://krds-api.test/",
            ClientId = KrdsTestClient,
            ClientSecret = KrdsTestPassphrase,
            GatewayApiKey = string.Empty,
        };

        var services = CreateServices(new CadsApiOptions(), krdsOptions, handler);

        services.AddStrategies();

        await using var provider = services.BuildServiceProvider();
        var factory = provider.GetRequiredService<IRestStrategyFactory<KrdsService>>();

        var strategy = factory.BuildRestStrategy();

        // Act
        await strategy
            .WithActionDescription("Test KRDS without API key")
            .WithResourceUrl("api/v2/test")
            .WithCancellationToken(CancellationToken.None)
            .WithGet()
            .Execute<object>();

        // Assert
        var request = Assert.Single(handler.Requests);

        Assert.False(request.Headers.Contains("x-api-key"));
    }

    [Fact]
    public void AddStrategies_ConfiguresCircuitBreakerFactory_ForCadsAndKrds_WithThresholds()
    {
        // Arrange
        var cadsOptions = new CadsApiOptions
        {
            BaseUrl = "http://cads.test/",
            ClientId = CadsTestClient,
            ClientSecret = CadsTestPassphrase,
            CircuitBreakerSamplingDuration = 15_000,
            CircuitBreakerDurationOfBreak = 10_000,
            CircuitBreakerMinimumThroughput = 4,
            CircuitBreakerConsecutiveFailureThreshold = 5,
            CircuitBreakerFailureRatio = 0.5,
        };

        var krdsOptions = new KrdsApiOptions
        {
            BaseUrl = "http://krds.test/",
            ClientId = KrdsTestClient,
            ClientSecret = KrdsTestPassphrase,
            CircuitBreakerSamplingDuration = 25_000,
            CircuitBreakerDurationOfBreak = 20_000,
            CircuitBreakerMinimumThroughput = 8,
            CircuitBreakerConsecutiveFailureThreshold = 4,
            CircuitBreakerFailureRatio = 0.6,
        };

        var services = CreateServices(cadsOptions, krdsOptions, new StubHttpMessageHandler());

        services.AddStrategies();

        using var provider = services.BuildServiceProvider();

        var cadsCbFactory = provider.GetRequiredService<ICircuitBreakerStrategyFactory<CadsService>>();
        var krdsCbFactory = provider.GetRequiredService<ICircuitBreakerStrategyFactory<KrdsService>>();

        // Act
        var cadsCbStrategy = cadsCbFactory.BuildCircuitBreakerStrategy();
        var krdsCbStrategy = krdsCbFactory.BuildCircuitBreakerStrategy();

        // Assert
        Assert.NotNull(cadsCbStrategy);
        Assert.NotNull(krdsCbStrategy);
    }

    [Fact]
    public void AddStrategies_ConfiguresCircuitBreakerFactory_WhenThresholdsAreZeroOrNull()
    {
        // Arrange
        var cadsOptions = new CadsApiOptions
        {
            BaseUrl = "http://cads.test/",
            ClientId = CadsTestClient,
            ClientSecret = CadsTestPassphrase,
            CircuitBreakerConsecutiveFailureThreshold = 0,
            CircuitBreakerFailureRatio = 0.0,
        };

        var krdsOptions = new KrdsApiOptions
        {
            BaseUrl = "http://krds.test/",
            ClientId = KrdsTestClient,
            ClientSecret = KrdsTestPassphrase,
            CircuitBreakerConsecutiveFailureThreshold = null,
            CircuitBreakerFailureRatio = null,
        };

        var services = CreateServices(cadsOptions, krdsOptions, new StubHttpMessageHandler());

        services.AddStrategies();

        using var provider = services.BuildServiceProvider();

        var cadsCbFactory = provider.GetRequiredService<ICircuitBreakerStrategyFactory<CadsService>>();
        var krdsCbFactory = provider.GetRequiredService<ICircuitBreakerStrategyFactory<KrdsService>>();

        // Act
        var cadsCbStrategy = cadsCbFactory.BuildCircuitBreakerStrategy();
        var krdsCbStrategy = krdsCbFactory.BuildCircuitBreakerStrategy();

        // Assert
        Assert.NotNull(cadsCbStrategy);
        Assert.NotNull(krdsCbStrategy);
    }

    private static ServiceCollection CreateServices(
        CadsApiOptions cadsOptions,
        KrdsApiOptions krdsOptions,
        StubHttpMessageHandler handler)
    {
        var services = new ServiceCollection();

        services.AddLogging();
        services.AddTransient<IRestHttpClient>(_ =>
            new RestHttpClient(new HttpClient(handler), NullLogger<RestHttpClient>.Instance));
        services.AddSingleton(Options.Create(cadsOptions));
        services.AddSingleton(Options.Create(krdsOptions));

        return services;
    }
}
