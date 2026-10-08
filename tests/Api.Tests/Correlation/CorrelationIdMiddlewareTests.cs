// <copyright file="CorrelationIdMiddlewareTests.cs" company="Defra">
// Copyright (c) Defra. All rights reserved.
// </copyright>

namespace Defra.Lis.Api.Tests.Correlation;

using System.Net;
using System.Text.Json;
using Defra.Lis.Api.Configurations;
using Defra.Lis.Api.Correlation;
using Defra.Lis.Api.Endpoints.Health;
using Defra.Lis.Api.Interfaces;
using Defra.Lis.Api.Services;
using Defra.Lis.Api.Tests.Upstream;
using Defra.Lis.Core.Exceptions;
using Defra.Livestock.Sdk.Api.Strategies;
using Defra.Livestock.Sdk.Api.Strategies.Abstractions.Operations.Http.Rest.Client;
using Defra.Livestock.Sdk.Api.Strategies.Operations.Http.Rest.Client;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Moq;

public class CorrelationIdMiddlewareTests
{
    private const string CorrelationId = "corr-598";
    private const string TestClient = "krds-client";
    private const string TestPassphrase = "krds-passphrase";

    [Fact]
    public async Task Request_WithoutTheHeader_IsRejectedWith400BeforeReachingTheEndpoint()
    {
        var reached = false;
        await using var app = await StartAppAsync(endpoints => endpoints.MapGet("/probe", () =>
        {
            reached = true;
            return "ok";
        }));

        var response = await app.GetTestClient().GetAsync("/probe", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.False(reached);
        using var problem = JsonDocument.Parse(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        Assert.Equal(CorrelationIdMiddleware.MissingHeaderCode, problem.RootElement.GetProperty("code").GetString());
        Assert.Contains(TraceHeaders.CdpRequestId, problem.RootElement.GetProperty("detail").GetString());
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Request_WithABlankHeader_IsRejectedWith400(string value)
    {
        await using var app = await StartAppAsync(endpoints => endpoints.MapGet("/probe", () => "ok"));
        using var request = new HttpRequestMessage(HttpMethod.Get, "/probe");
        request.Headers.TryAddWithoutValidation(TraceHeaders.CdpRequestId, value);

        var response = await app.GetTestClient().SendAsync(request, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Request_WithMoreThanOneHeaderValue_IsRejectedWith400()
    {
        await using var app = await StartAppAsync(endpoints => endpoints.MapGet("/probe", () => "ok"));
        using var request = new HttpRequestMessage(HttpMethod.Get, "/probe");
        request.Headers.Add(TraceHeaders.CdpRequestId, ["first", "second"]);

        var response = await app.GetTestClient().SendAsync(request, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Request_WithTheHeader_ReachesTheEndpointWithTheContextSetAndEchoesTheHeader()
    {
        string? seen = null;
        await using var app = await StartAppAsync(endpoints => endpoints.MapGet("/probe", () =>
        {
            seen = CorrelationIdContext.Value;
            return "ok";
        }));
        using var request = new HttpRequestMessage(HttpMethod.Get, "/probe");
        request.Headers.Add(TraceHeaders.CdpRequestId, CorrelationId);

        var response = await app.GetTestClient().SendAsync(request, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(CorrelationId, seen);
        Assert.Equal([CorrelationId], response.Headers.GetValues(TraceHeaders.CdpRequestId));
    }

    [Fact]
    public async Task ExemptEndpoint_DoesNotNeedTheHeader()
    {
        await using var app = await StartAppAsync(endpoints => endpoints.MapGet("/exempt", () => "ok").WithoutCorrelationIdCheck());

        var response = await app.GetTestClient().GetAsync("/exempt", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task UnknownRoute_IsANotFoundRatherThanAMissingHeader()
    {
        await using var app = await StartAppAsync(endpoints => endpoints.MapGet("/probe", () => "ok"));

        var response = await app.GetTestClient().GetAsync("/unknown", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task HealthEndpoints_AreExemptFromTheHeader()
    {
        await using var app = await StartAppAsync(endpoints => endpoints.MapHealthEndpoints());

        var endpoints = app.Services.GetRequiredService<EndpointDataSource>().Endpoints.OfType<RouteEndpoint>()
            .Where(endpoint => endpoint.RoutePattern.RawText!.StartsWith("/health", StringComparison.Ordinal))
            .ToList();

        Assert.Equal(2, endpoints.Count);
        Assert.All(endpoints, endpoint => Assert.NotNull(endpoint.Metadata.GetMetadata<IgnoreCorrelationIdCheck>()));
        var response = await app.GetTestClient().GetAsync("/health", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task InboundHeader_IsPropagatedToTheKrdsCall()
    {
        var upstream = new StubHttpMessageHandler().RespondWith(HttpStatusCode.NotFound);
        await using var app = await StartAppAsync(
            endpoints => endpoints.MapGet("/probe", async (IKrdsService krds) =>
            {
                try
                {
                    await krds.GetHoldingAsync("22", "001", "0001");
                    return Results.Ok();
                }
                catch (NotFoundException)
                {
                    return Results.NotFound();
                }
            }),
            services =>
            {
                // Mirrors Program: the REST client is registered with header propagation before the strategy factory.
                services.AddStrategyFramework();
                services.AddHttpClient<IRestHttpClient, RestHttpClient>()
                    .AddHeaderPropagation()
                    .ConfigurePrimaryHttpMessageHandler(() => upstream);
                services.AddRestStrategyFactory<KrdsService>();
                services.AddSingleton(Microsoft.Extensions.Options.Options.Create(new KrdsApiOptions
                {
                    BaseUrl = "http://krds.test/",
                    ClientId = TestClient,
                    ClientSecret = TestPassphrase,
                }));
                services.AddScoped<IKrdsService, KrdsService>();
            });
        using var request = new HttpRequestMessage(HttpMethod.Get, "/probe");
        request.Headers.Add(TraceHeaders.CdpRequestId, CorrelationId);

        var response = await app.GetTestClient().SendAsync(request, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        var outbound = Assert.Single(upstream.Requests);
        Assert.Equal([CorrelationId], outbound.Headers.GetValues(TraceHeaders.CdpRequestId));
    }

    private static async Task<WebApplication> StartAppAsync(
        Action<WebApplication> mapEndpoints,
        Action<IServiceCollection>? configureServices = null)
    {
        var builder = WebApplication.CreateEmptyBuilder(new WebApplicationOptions());
        builder.WebHost.UseTestServer();
        builder.Services.AddRouting();
        builder.Services.AddLogging();
        builder.Services.AddHealthChecks();
        builder.Services.AddSingleton(Mock.Of<IHealthService>());
        builder.Services.AddCorrelationId();
        configureServices?.Invoke(builder.Services);
        var app = builder.Build();
        app.UseRouting();
        app.UseCorrelationId();
        mapEndpoints(app);
        await app.StartAsync(TestContext.Current.CancellationToken);
        return app;
    }
}
