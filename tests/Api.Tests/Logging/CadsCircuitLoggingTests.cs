// <copyright file="CadsCircuitLoggingTests.cs" company="Defra">
// Copyright (c) Defra. All rights reserved.
// </copyright>

namespace Defra.Lis.Api.Tests.Logging;

using System.Collections.Concurrent;
using System.Net;
using Defra.Lis.Api.Configurations;
using Defra.Lis.Api.Correlation;
using Defra.Lis.Api.Exceptions;
using Defra.Lis.Api.Interfaces;
using Defra.Lis.Api.Services;
using Defra.Lis.Api.Tests.Upstream;
using Defra.Livestock.Sdk.Api.Strategies;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Serilog;
using Serilog.Core;
using Serilog.Events;

/// <summary>
/// Opens the CADS circuit through requests to a test endpoint, with the logging pipeline from <c>Program</c>, and checks
/// that the circuit transitions are logged without the correlation ID or path of the request that first used the
/// pipeline (LREG-509). Polly runs the callbacks on a task that captured that request's execution context.
/// </summary>
[Collection(SerilogStaticLoggerCollection.Name)]
public class CadsCircuitLoggingTests
{
    private const string EarTag = "UK200000000001";
    private const string FirstCorrelationId = "corr-509-first";

    private readonly StubHttpMessageHandler cads = new();
    private readonly CapturingSink sink = new();

    [Fact]
    public async Task CircuitOpened_IsLoggedWithoutAnyRequestContext()
    {
        cads.FailWith(new HttpRequestException("Connection refused"))
            .FailWith(new HttpRequestException("Connection refused"));
        await using var app = await StartAppAsync();
        var client = app.GetTestClient();

        foreach (var correlationId in new[] { FirstCorrelationId, "corr-509-second", "corr-509-third" })
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, $"/v1/cattle/{EarTag}");
            request.Headers.Add(TraceHeaders.CdpRequestId, correlationId);
            using var response = await client.SendAsync(request, TestContext.Current.CancellationToken);
            Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        }

        var opened = await LoggedAsync("circuit opened");

        Assert.Equal(2, cads.Requests.Count);
        Assert.Equal(LogEventLevel.Warning, opened.Level);
        Assert.False(opened.Properties.ContainsKey("CorrelationId"), "The transition carried a stale correlation ID.");
        Assert.False(opened.Properties.ContainsKey("RequestPath"), "The transition carried a stale request path.");
    }

    private async Task<LogEvent> LoggedAsync(string text)
    {
        var deadline = DateTime.UtcNow.AddSeconds(5);
        LogEvent? found;

        while ((found = sink.Events.FirstOrDefault(e => e.RenderMessage().Contains(text, StringComparison.Ordinal))) is null &&
               DateTime.UtcNow < deadline)
        {
            await Task.Delay(20, TestContext.Current.CancellationToken);
        }

        Assert.True(found is not null, $"Not logged: {text}");
        return found;
    }

    private async Task<WebApplication> StartAppAsync()
    {
        var builder = WebApplication.CreateEmptyBuilder(new WebApplicationOptions());
        builder.WebHost.UseTestServer();

        builder.Services.AddHttpContextAccessor();
        builder.Host.UseSerilog((context, services, configuration) =>
        {
            CdpLogging.Configuration(context, services, configuration);
            configuration.MinimumLevel.Information().WriteTo.Sink(sink);
        });

        builder.Services.AddRouting();
        builder.Services.AddProblemDetails();
        builder.Services.AddExceptionHandler<ApiExceptionHandler>();
        builder.Services.AddCorrelationId();

        // The CADS client is registered as in Program, so the resilience pipeline and its callbacks run too.
        builder.Services.AddStrategyFramework();
        builder.Services.AddUpstreamHttpClient().ConfigurePrimaryHttpMessageHandler(() => cads);
        builder.Services.AddRestStrategyFactory<CadsService>();
        builder.Services.AddSingleton(Microsoft.Extensions.Options.Options.Create(new CadsApiOptions
        {
            BaseUrl = "http://fake-service.test/cads/",
            ClientId = nameof(CadsCircuitLoggingTests),
            ClientSecret = Guid.NewGuid().ToString("N"),
            CircuitBreakerFailureThreshold = 2,
        }));
        builder.Services.AddSingleton(Microsoft.Extensions.Options.Options.Create(new CadsDataPatchOptions()));
        builder.Services.AddSingleton(Mock.Of<ICadsDataPatchStore>());
        builder.Services.AddScoped<ICadsService, CadsService>();

        var app = builder.Build();
        app.UseCdpRequestLogging();
        app.UseExceptionHandler();
        app.UseCorrelationId();
        app.MapGet(
            "/v1/cattle/{earTag}",
            async (string earTag, ICadsService service, CancellationToken cancellationToken) =>
                Results.Ok(await service.GetAnimalDetailsAsync(earTag, cancellationToken)));
        await app.StartAsync(TestContext.Current.CancellationToken);
        return app;
    }

    private sealed class CapturingSink : ILogEventSink
    {
        private readonly ConcurrentQueue<LogEvent> events = new();

        public IReadOnlyCollection<LogEvent> Events => events;

        public void Emit(LogEvent logEvent) => events.Enqueue(logEvent);
    }
}
