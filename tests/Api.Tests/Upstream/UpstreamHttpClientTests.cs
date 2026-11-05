// <copyright file="UpstreamHttpClientTests.cs" company="Defra">
// Copyright (c) Defra. All rights reserved.
// </copyright>

namespace Defra.Lis.Api.Tests.Upstream;

using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net;
using Defra.Lis.Api.Configurations;
using Defra.Lis.Api.Exceptions;
using Defra.Lis.Api.Interfaces;
using Defra.Lis.Api.Services;
using Defra.Livestock.Sdk.Api.Strategies.Abstractions.Exceptions;
using Defra.Livestock.Sdk.Api.Strategies.Abstractions.Operations.Http.Rest.Client;
using Defra.Livestock.Sdk.Api.Strategies.Operations;
using Microsoft.AspNetCore.HeaderPropagation;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Primitives;
using Moq;
using Polly.CircuitBreaker;
using Polly.Timeout;

/// <summary>
/// Runs <see cref="CadsService"/> over the HTTP client <see cref="UpstreamHttpClient"/> registers, as Program does, to
/// prove the CADS timeout and circuit breaker (LREG-509): a hung or unreachable CADS becomes a 503 within the budget,
/// repeated failures open the circuit so calls fail fast, a successful probe closes it again, and KRDS is unaffected.
/// </summary>
public sealed class UpstreamHttpClientTests : IDisposable
{
    private const string Cph = "22/001/0001";
    private const string CorrelationId = "corr-resilience";
    private const string CadsBaseUrl = "http://fake-service.test/cads/";
    private const string KrdsUrl = "http://fake-service.test/krds/api/v2/holdings";
    private const string EmptyPage = """
        {"resourceType":"AnimalCollection","animals":[],"totalRecords":0,"page":1,"pageSize":2}
        """;

    // Rows are keyed by name, not by exception instance, so xUnit can serialise and list each one (xUnit1045).
    private static readonly Dictionary<string, Func<Exception>> Failures = new()
    {
        [nameof(HttpRequestException)] = () => new HttpRequestException("Connection refused (fake-service.test:80)"),
        [nameof(TimeoutRejectedException)] = () => new TimeoutRejectedException(),
        [nameof(OperationCanceledException)] = () => new OperationCanceledException(),
        [nameof(BrokenCircuitException)] = () => new BrokenCircuitException(),
        [nameof(InvalidOperationException)] = () => new InvalidOperationException(),
    };

    private readonly StubHttpMessageHandler handler = new();
    private readonly CapturingLoggerProvider logs = new();
    private ServiceProvider? provider;

    public static TheoryData<string> FailuresWithoutResponse =>
        [nameof(HttpRequestException), nameof(TimeoutRejectedException)];

    public static TheoryData<string> FailuresThatDoNotCount =>
        [nameof(OperationCanceledException), nameof(BrokenCircuitException), nameof(InvalidOperationException)];

    public void Dispose() => provider?.Dispose();

    [Fact]
    public async Task HungCadsShouldTimeOutWithinTheBudgetAsServiceUnavailable()
    {
        handler.Hang();
        var cads = CreateCads(new CadsApiOptions { TimeoutSeconds = 1 });
        var stopwatch = Stopwatch.StartNew();

        var exception = await Should.ThrowAsync<ServiceUnavailableException>(
            () => cads.GetCattleByCphAsync(Cph, TestContext.Current.CancellationToken));

        stopwatch.Elapsed.ShouldBeLessThan(TimeSpan.FromSeconds(1) + UpstreamHttpClient.BodyGrace);
        exception.InnerException.ShouldBeOfType<RestRequestException>().InnerException.ShouldBeOfType<TimeoutRejectedException>();
    }

    [Fact]
    public async Task StalledCadsBodyShouldBeCutOffAfterTheBodyGraceAsServiceUnavailable()
    {
        // The headers arrive in time, so the pipeline timeout does not apply; the HttpClient timeout CadsService sets
        // on its own calls, one second later, cuts the stalled body off.
        handler.StallBody();
        var cads = CreateCads(new CadsApiOptions { TimeoutSeconds = 1 });
        var stopwatch = Stopwatch.StartNew();

        var exception = await Should.ThrowAsync<ServiceUnavailableException>(
            () => cads.GetCattleByCphAsync(Cph, TestContext.Current.CancellationToken));

        stopwatch.Elapsed.ShouldBeGreaterThan(TimeSpan.FromSeconds(1.5));
        stopwatch.Elapsed.ShouldBeLessThan(TimeSpan.FromSeconds(4));
        exception.InnerException.ShouldBeOfType<RestRequestException>();
    }

    [Fact]
    public async Task RepeatedTransportFailuresShouldOpenTheCircuitAndFailFast()
    {
        handler.FailWith(new HttpRequestException("Connection refused"))
            .FailWith(new HttpRequestException("Connection refused"));
        var cads = CreateCads(new CadsApiOptions { CircuitBreakerFailureThreshold = 2 });

        await Should.ThrowAsync<ServiceUnavailableException>(() => cads.GetCattleByCphAsync(Cph, TestContext.Current.CancellationToken));
        await Should.ThrowAsync<ServiceUnavailableException>(() => cads.GetCattleByCphAsync(Cph, TestContext.Current.CancellationToken));
        var exception = await Should.ThrowAsync<ServiceUnavailableException>(
            () => cads.GetCattleByCphAsync(Cph, TestContext.Current.CancellationToken));

        exception.InnerException.ShouldBeOfType<RestRequestException>().InnerException.ShouldBeAssignableTo<BrokenCircuitException>();
        handler.Requests.Count.ShouldBe(2);
        await LoggedAsync(LogLevel.Warning, "The CADS circuit opened: at least 2 calls");
    }

    [Fact]
    public async Task SuccessfulProbeAfterTheBreakShouldCloseTheCircuit()
    {
        handler.FailWith(new HttpRequestException("Connection refused"))
            .FailWith(new HttpRequestException("Connection refused"))
            .RespondWith(HttpStatusCode.OK, EmptyPage)
            .RespondWith(HttpStatusCode.OK, EmptyPage);
        var cads = CreateCads(new CadsApiOptions { CircuitBreakerFailureThreshold = 2, CircuitBreakerBreakSeconds = 1 });

        await Should.ThrowAsync<ServiceUnavailableException>(() => cads.GetCattleByCphAsync(Cph, TestContext.Current.CancellationToken));
        await Should.ThrowAsync<ServiceUnavailableException>(() => cads.GetCattleByCphAsync(Cph, TestContext.Current.CancellationToken));
        await Task.Delay(TimeSpan.FromSeconds(1.2), TestContext.Current.CancellationToken);

        (await cads.GetCattleByCphAsync(Cph, TestContext.Current.CancellationToken)).ShouldBeEmpty();
        (await cads.GetCattleByCphAsync(Cph, TestContext.Current.CancellationToken)).ShouldBeEmpty();

        handler.Requests.Count.ShouldBe(4);
        await LoggedAsync(LogLevel.Information, "The CADS circuit is half-open");
        await LoggedAsync(LogLevel.Information, "The CADS circuit closed");
    }

    [Fact]
    public async Task FailedProbeAfterTheBreakShouldReopenTheCircuit()
    {
        handler.FailWith(new HttpRequestException("Connection refused"))
            .FailWith(new HttpRequestException("Connection refused"))
            .FailWith(new HttpRequestException("Connection refused"));
        var cads = CreateCads(new CadsApiOptions { CircuitBreakerFailureThreshold = 2, CircuitBreakerBreakSeconds = 1 });

        await Should.ThrowAsync<ServiceUnavailableException>(() => cads.GetCattleByCphAsync(Cph, TestContext.Current.CancellationToken));
        await Should.ThrowAsync<ServiceUnavailableException>(() => cads.GetCattleByCphAsync(Cph, TestContext.Current.CancellationToken));
        await Task.Delay(TimeSpan.FromSeconds(1.2), TestContext.Current.CancellationToken);

        await Should.ThrowAsync<ServiceUnavailableException>(() => cads.GetCattleByCphAsync(Cph, TestContext.Current.CancellationToken));
        var exception = await Should.ThrowAsync<ServiceUnavailableException>(
            () => cads.GetCattleByCphAsync(Cph, TestContext.Current.CancellationToken));

        exception.InnerException.ShouldBeOfType<RestRequestException>().InnerException.ShouldBeAssignableTo<BrokenCircuitException>();
        handler.Requests.Count.ShouldBe(3);
        await LoggedAsync(LogLevel.Warning, "The CADS circuit reopened: the probe call failed");
        logs.Messages.Count(m => m.Text.Contains("circuit opened", StringComparison.Ordinal)).ShouldBe(1);
    }

    [Fact]
    public async Task ErrorResponsesShouldNotOpenTheCircuit()
    {
        // Any response proves CADS is up: the calls fail as they did, and the next call still reaches CADS.
        handler.RespondWith(HttpStatusCode.InternalServerError, """{"status":500}""")
            .RespondWith(HttpStatusCode.ServiceUnavailable, """{"status":503}""")
            .RespondWith(HttpStatusCode.OK, EmptyPage);
        var cads = CreateCads(new CadsApiOptions { CircuitBreakerFailureThreshold = 2 });

        await Should.ThrowAsync<RestResponseException>(() => cads.GetCattleByCphAsync(Cph, TestContext.Current.CancellationToken));
        await Should.ThrowAsync<ServiceUnavailableException>(() => cads.GetCattleByCphAsync(Cph, TestContext.Current.CancellationToken));

        (await cads.GetCattleByCphAsync(Cph, TestContext.Current.CancellationToken)).ShouldBeEmpty();
        handler.Requests.Count.ShouldBe(3);
    }

    [Fact]
    public async Task CallerCancellationShouldNotOpenTheCircuit()
    {
        // Each call is cancelled once its request reaches CADS, so the cancellation never races the request.
        using var first = new CancellationTokenSource();
        using var second = new CancellationTokenSource();
        handler.Hang(first.Cancel).Hang(second.Cancel).RespondWith(HttpStatusCode.OK, EmptyPage);
        var cads = CreateCads(new CadsApiOptions { CircuitBreakerFailureThreshold = 2 });

        foreach (var cancellation in new[] { first, second })
        {
            var exception = await Should.ThrowAsync<Exception>(() => cads.GetCattleByCphAsync(Cph, cancellation.Token));
            exception.ShouldNotBeOfType<ServiceUnavailableException>();
        }

        (await cads.GetCattleByCphAsync(Cph, TestContext.Current.CancellationToken)).ShouldBeEmpty();
        handler.Requests.Count.ShouldBe(3);
    }

    [Fact]
    public async Task OpenCadsCircuitShouldNotStopKrdsCalls()
    {
        // CADS and KRDS share a host in lis-dev, so the pipeline is chosen by base URL, not by host.
        handler.FailWith(new HttpRequestException("Connection refused"))
            .FailWith(new HttpRequestException("Connection refused"))
            .RespondWith(HttpStatusCode.OK, "{}");
        var cads = CreateCads(new CadsApiOptions { CircuitBreakerFailureThreshold = 2 });
        await Should.ThrowAsync<ServiceUnavailableException>(() => cads.GetCattleByCphAsync(Cph, TestContext.Current.CancellationToken));
        await Should.ThrowAsync<ServiceUnavailableException>(() => cads.GetCattleByCphAsync(Cph, TestContext.Current.CancellationToken));
        await Should.ThrowAsync<ServiceUnavailableException>(() => cads.GetCattleByCphAsync(Cph, TestContext.Current.CancellationToken));

        using var response = await SharedHttpClient().GetAsync(KrdsUrl, TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        handler.Requests.Count.ShouldBe(3);
        handler.Requests[^1].RequestUri!.AbsoluteUri.ShouldBe(KrdsUrl);
    }

    [Fact]
    public async Task KrdsCallsShouldNotBeTimedOutByTheCadsPipeline()
    {
        using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        handler.Hang();
        CreateCads(new CadsApiOptions { TimeoutSeconds = 1 });

        // Only the caller's own cancellation, after 2 s, ends the hung KRDS call; the CADS 1 s timeout does not apply.
        var stopwatch = Stopwatch.StartNew();
        await Should.ThrowAsync<OperationCanceledException>(() => SharedHttpClient().GetAsync(KrdsUrl, cancellation.Token));

        stopwatch.Elapsed.ShouldBeGreaterThan(TimeSpan.FromSeconds(1.5));
    }

    [Fact]
    public async Task CorrelationIdShouldStillReachCadsThroughThePipeline()
    {
        handler.RespondWith(HttpStatusCode.OK, EmptyPage);
        var cads = CreateCads(new CadsApiOptions());

        await cads.GetCattleByCphAsync(Cph, TestContext.Current.CancellationToken);

        handler.Requests.ShouldHaveSingleItem().Headers.GetValues("x-cdp-request-id").ShouldBe([CorrelationId]);
    }

    [Theory]
    [InlineData("http://fake-service.test/cads/api/v1/bovine/animals?CPH=1", "http://fake-service.test/cads/", UpstreamHttpClient.CadsPipeline)]
    [InlineData("http://FAKE-service.test/CADS/api/v1/bovine/animals", "http://fake-service.test/cads/", UpstreamHttpClient.CadsPipeline)]
    [InlineData("http://fake-service.test/krds/api/v2/holdings", "http://fake-service.test/cads/", UpstreamHttpClient.OtherPipeline)]
    [InlineData("http://fake-service.test/cadsx/api", "http://fake-service.test/cads/", UpstreamHttpClient.OtherPipeline)]
    [InlineData("http://fake-service.test/cads/api", null, UpstreamHttpClient.OtherPipeline)]
    public void PipelineForShouldChooseByBaseUrl(string requestUri, string? cadsBaseUrl, string expected) =>
        UpstreamHttpClient.PipelineFor(new Uri(requestUri), cadsBaseUrl is null ? null : new Uri(cadsBaseUrl)).ShouldBe(expected);

    [Fact]
    public async Task CadsBaseUrlWithoutATrailingSlashShouldStillChooseTheCadsPipeline()
    {
        handler.Hang();
        var cads = CreateCads(new CadsApiOptions { BaseUrl = CadsBaseUrl.TrimEnd('/'), TimeoutSeconds = 1 });

        var exception = await Should.ThrowAsync<ServiceUnavailableException>(
            () => cads.GetCattleByCphAsync(Cph, TestContext.Current.CancellationToken));

        exception.InnerException.ShouldBeOfType<RestRequestException>().InnerException.ShouldBeOfType<TimeoutRejectedException>();
    }

    [Fact]
    public async Task InvalidCadsOptionsShouldLeaveEveryCallOnTheEmptyPipeline()
    {
        // CADS options are validated on first use, so the API can boot before the CADS secrets are set; KRDS must work.
        handler.RespondWith(HttpStatusCode.OK, "{}");
        var services = BaseServices();
        services.AddOptions<CadsApiOptions>().ValidateDataAnnotations();
        Build(services);

        using var response = await SharedHttpClient().GetAsync(KrdsUrl, TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Theory]
    [MemberData(nameof(FailuresWithoutResponse))]
    public void FailuresWithoutAResponseShouldCountTowardsOpeningTheCircuit(string failure) =>
        UpstreamHttpClient.IsFailureWithoutResponse(Polly.Outcome.FromException<HttpResponseMessage>(Failures[failure]())).ShouldBeTrue();

    [Theory]
    [MemberData(nameof(FailuresThatDoNotCount))]
    public void OtherFailuresShouldNotCountTowardsOpeningTheCircuit(string failure) =>
        UpstreamHttpClient.IsFailureWithoutResponse(Polly.Outcome.FromException<HttpResponseMessage>(Failures[failure]())).ShouldBeFalse();

    [Theory]
    [InlineData(HttpStatusCode.InternalServerError)]
    [InlineData(HttpStatusCode.ServiceUnavailable)]
    [InlineData(HttpStatusCode.GatewayTimeout)]
    public void ResponsesShouldNotCountTowardsOpeningTheCircuit(HttpStatusCode statusCode)
    {
        using var response = new HttpResponseMessage(statusCode);

        UpstreamHttpClient.IsFailureWithoutResponse(Polly.Outcome.FromResult(response)).ShouldBeFalse();
    }

    // Polly runs the circuit callbacks off the request path, so a transition log can land just after the call returns.
    private async Task LoggedAsync(LogLevel level, string text)
    {
        var deadline = DateTime.UtcNow.AddSeconds(5);

        while (!logs.Messages.Any(m => m.Level == level && m.Text.Contains(text, StringComparison.Ordinal)) && DateTime.UtcNow < deadline)
        {
            await Task.Delay(20, TestContext.Current.CancellationToken);
        }

        logs.Messages.ShouldContain(m => m.Level == level && m.Text.Contains(text, StringComparison.Ordinal));
    }

    private HttpClient SharedHttpClient() =>

        // A typed client is named after its service type.
        provider!.GetRequiredService<IHttpClientFactory>().CreateClient(nameof(IRestHttpClient));

    private ServiceCollection BaseServices()
    {
        var services = new ServiceCollection();
        services.AddLogging(logging => logging.AddProvider(logs));
        services.AddHeaderPropagation(propagation => propagation.Headers.Add("x-cdp-request-id"));
        services.AddUpstreamHttpClient().ConfigurePrimaryHttpMessageHandler(() => handler);
        return services;
    }

    private ServiceProvider Build(ServiceCollection services)
    {
        provider = services.BuildServiceProvider();

        // Header propagation reads the inbound headers the middleware captured for this request.
        provider.GetRequiredService<HeaderPropagationValues>().Headers =
            new Dictionary<string, StringValues>(StringComparer.OrdinalIgnoreCase) { ["x-cdp-request-id"] = CorrelationId };

        return provider;
    }

    private CadsService CreateCads(CadsApiOptions overrides)
    {
        var options = new CadsApiOptions
        {
            BaseUrl = overrides.BaseUrl is { Length: > 0 } baseUrl ? baseUrl : CadsBaseUrl,
            ClientId = nameof(UpstreamHttpClientTests),
            ClientSecret = Guid.NewGuid().ToString("N"),
            PageSize = 2,
            TimeoutSeconds = overrides.TimeoutSeconds,
            CircuitBreakerFailureThreshold = overrides.CircuitBreakerFailureThreshold,
            CircuitBreakerSamplingSeconds = overrides.CircuitBreakerSamplingSeconds,
            CircuitBreakerBreakSeconds = overrides.CircuitBreakerBreakSeconds,
        };

        var services = BaseServices();
        services.AddSingleton(Microsoft.Extensions.Options.Options.Create(options));

        return new CadsService(
            new RestStrategyFactory<CadsService>(Build(services)),
            Microsoft.Extensions.Options.Options.Create(options),
            Microsoft.Extensions.Options.Options.Create(new CadsDataPatchOptions()),
            Mock.Of<ICadsDataPatchStore>(),
            NullLogger<CadsService>.Instance);
    }

    private sealed record LogMessage(LogLevel Level, string Text);

    private sealed class CapturingLoggerProvider : ILoggerProvider
    {
        public ConcurrentQueue<LogMessage> Messages { get; } = new();

        public ILogger CreateLogger(string categoryName) => new CapturingLogger(Messages);

        public void Dispose()
        {
        }

        private sealed class CapturingLogger(ConcurrentQueue<LogMessage> messages) : ILogger
        {
            public IDisposable? BeginScope<TState>(TState state)
                where TState : notnull => null;

            public bool IsEnabled(LogLevel logLevel) => true;

            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) =>
                messages.Enqueue(new LogMessage(logLevel, formatter(state, exception)));
        }
    }
}
