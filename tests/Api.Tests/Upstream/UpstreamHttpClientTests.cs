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
using Defra.Lis.Api.Validation;
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
    private const string KrdsBaseUrl = "http://fake-service.test/krds/";
    private const string Subject = "5e0c8f8a-4b1d-4c7e-9a3f-6d2b8e1f0a47";
    private const string UserAccount = $$"""
        {"subject":"{{Subject}}","email":"oakfield.farmer@oakhill-farms.co.uk","cphAssociations":[]}
        """;

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
    public async Task HungKrdsShouldTimeOutWithinTheBudgetAsServiceUnavailable()
    {
        handler.Hang();
        var krds = CreateUpstreams(krdsOverrides: new KrdsApiOptions { TimeoutSeconds = 1 }).Krds;
        var stopwatch = Stopwatch.StartNew();

        var exception = await Should.ThrowAsync<ServiceUnavailableException>(
            () => krds.GetUserAccountAsync(Subject, TestContext.Current.CancellationToken));

        stopwatch.Elapsed.ShouldBeLessThan(TimeSpan.FromSeconds(1) + UpstreamHttpClient.BodyGrace);
        exception.InnerException.ShouldBeOfType<RestRequestException>().InnerException.ShouldBeOfType<TimeoutRejectedException>();
    }

    [Fact]
    public async Task KrdsCircuitShouldOpenFailFastAndCloseAfterASuccessfulProbe()
    {
        handler.FailWith(new HttpRequestException("Connection refused"))
            .FailWith(new HttpRequestException("Connection refused"))
            .RespondWith(HttpStatusCode.OK, UserAccount);
        var krds = CreateUpstreams(krdsOverrides: new KrdsApiOptions { CircuitBreakerFailureThreshold = 2, CircuitBreakerBreakSeconds = 1 }).Krds;

        await Should.ThrowAsync<ServiceUnavailableException>(() => krds.GetUserAccountAsync(Subject, TestContext.Current.CancellationToken));
        await Should.ThrowAsync<ServiceUnavailableException>(() => krds.GetUserAccountAsync(Subject, TestContext.Current.CancellationToken));
        var failedFast = await Should.ThrowAsync<ServiceUnavailableException>(
            () => krds.GetUserAccountAsync(Subject, TestContext.Current.CancellationToken));
        failedFast.InnerException.ShouldBeOfType<RestRequestException>().InnerException.ShouldBeAssignableTo<BrokenCircuitException>();
        handler.Requests.Count.ShouldBe(2);
        await LoggedAsync(LogLevel.Warning, "The KRDS circuit opened: at least 2 calls");

        await Task.Delay(TimeSpan.FromSeconds(1.2), TestContext.Current.CancellationToken);

        (await krds.GetUserAccountAsync(Subject, TestContext.Current.CancellationToken)).ShouldNotBeNull();
        handler.Requests.Count.ShouldBe(3);
        await LoggedAsync(LogLevel.Information, "The KRDS circuit closed");
    }

    [Fact]
    public async Task OpenCadsCircuitShouldNotStopKrdsCalls()
    {
        // CADS and KRDS share a host in lis-dev and on the CDP dev gateway, so the pipeline is chosen by base URL.
        handler.FailWith(new HttpRequestException("Connection refused"))
            .FailWith(new HttpRequestException("Connection refused"))
            .RespondWith(HttpStatusCode.OK, UserAccount);
        var (cads, krds) = CreateUpstreams(new CadsApiOptions { CircuitBreakerFailureThreshold = 2 }, new KrdsApiOptions { CircuitBreakerFailureThreshold = 2 });
        await Should.ThrowAsync<ServiceUnavailableException>(() => cads.GetCattleByCphAsync(Cph, TestContext.Current.CancellationToken));
        await Should.ThrowAsync<ServiceUnavailableException>(() => cads.GetCattleByCphAsync(Cph, TestContext.Current.CancellationToken));
        await Should.ThrowAsync<ServiceUnavailableException>(() => cads.GetCattleByCphAsync(Cph, TestContext.Current.CancellationToken));

        (await krds.GetUserAccountAsync(Subject, TestContext.Current.CancellationToken)).ShouldNotBeNull();

        handler.Requests.Count.ShouldBe(3);
        handler.Requests[^1].RequestUri!.AbsoluteUri.ShouldStartWith(KrdsBaseUrl);
    }

    [Fact]
    public async Task OpenKrdsCircuitShouldNotStopCadsCalls()
    {
        handler.FailWith(new HttpRequestException("Connection refused"))
            .FailWith(new HttpRequestException("Connection refused"))
            .RespondWith(HttpStatusCode.OK, EmptyPage);
        var (cads, krds) = CreateUpstreams(new CadsApiOptions { CircuitBreakerFailureThreshold = 2 }, new KrdsApiOptions { CircuitBreakerFailureThreshold = 2 });
        await Should.ThrowAsync<ServiceUnavailableException>(() => krds.GetUserAccountAsync(Subject, TestContext.Current.CancellationToken));
        await Should.ThrowAsync<ServiceUnavailableException>(() => krds.GetUserAccountAsync(Subject, TestContext.Current.CancellationToken));
        await Should.ThrowAsync<ServiceUnavailableException>(() => krds.GetUserAccountAsync(Subject, TestContext.Current.CancellationToken));

        (await cads.GetCattleByCphAsync(Cph, TestContext.Current.CancellationToken)).ShouldBeEmpty();

        handler.Requests.Count.ShouldBe(3);
        handler.Requests[^1].RequestUri!.AbsoluteUri.ShouldStartWith(CadsBaseUrl);
    }

    [Fact]
    public async Task CallsToNeitherUpstreamShouldNotBeTimedOut()
    {
        using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        handler.Hang();
        CreateUpstreams(new CadsApiOptions { TimeoutSeconds = 1 }, new KrdsApiOptions { TimeoutSeconds = 1 });

        // Only the caller's own cancellation, after 2 s, ends the hung call; neither upstream's 1 s timeout applies.
        var stopwatch = Stopwatch.StartNew();
        await Should.ThrowAsync<OperationCanceledException>(
            () => SharedHttpClient().GetAsync("http://elsewhere.test/anything", cancellation.Token));

        stopwatch.Elapsed.ShouldBeGreaterThan(TimeSpan.FromSeconds(1.5));
    }

    [Fact]
    public async Task CorrelationIdShouldStillReachKrdsThroughThePipeline()
    {
        handler.RespondWith(HttpStatusCode.OK, UserAccount);
        var krds = CreateUpstreams().Krds;

        await krds.GetUserAccountAsync(Subject, TestContext.Current.CancellationToken);

        handler.Requests.ShouldHaveSingleItem().Headers.GetValues("x-cdp-request-id").ShouldBe([CorrelationId]);
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
    [InlineData("http://fake-service.test/cads/api/v1/bovine/animals?CPH=1", UpstreamHttpClient.CadsPipeline)]
    [InlineData("http://FAKE-service.test/CADS/api/v1/bovine/animals", UpstreamHttpClient.CadsPipeline)]
    [InlineData("http://fake-service.test/krds/api/v2/user-accounts/subject", UpstreamHttpClient.KrdsPipeline)]
    [InlineData("http://fake-service.test/KRDS/api/v2/holdings", UpstreamHttpClient.KrdsPipeline)]
    [InlineData("http://fake-service.test/cadsx/api", UpstreamHttpClient.OtherPipeline)]
    [InlineData("http://elsewhere.test/cads/api", UpstreamHttpClient.OtherPipeline)]
    public void PipelineForShouldChooseByBaseUrl(string requestUri, string expected)
    {
        var upstreams = new Dictionary<string, Uri>
        {
            [UpstreamHttpClient.CadsPipeline] = new(CadsBaseUrl),
            [UpstreamHttpClient.KrdsPipeline] = new(KrdsBaseUrl),
        };

        UpstreamHttpClient.PipelineFor(new Uri(requestUri), upstreams).ShouldBe(expected);
    }

    [Fact]
    public void PipelineForShouldChooseTheLongestMatchingBaseUrl()
    {
        var upstreams = new Dictionary<string, Uri>
        {
            [UpstreamHttpClient.CadsPipeline] = new("http://gateway.test/"),
            [UpstreamHttpClient.KrdsPipeline] = new("http://gateway.test/krds/"),
        };

        UpstreamHttpClient.PipelineFor(new Uri("http://gateway.test/krds/api"), upstreams).ShouldBe(UpstreamHttpClient.KrdsPipeline);
        UpstreamHttpClient.PipelineFor(new Uri("http://gateway.test/cads/api"), upstreams).ShouldBe(UpstreamHttpClient.CadsPipeline);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("api/v2/holdings")]
    public void PipelineForShouldChooseTheEmptyPipeline_WhenTheRequestHasNoAbsoluteUri(string? requestUri)
    {
        var upstreams = new Dictionary<string, Uri> { [UpstreamHttpClient.CadsPipeline] = new(CadsBaseUrl) };

        UpstreamHttpClient.PipelineFor(requestUri is null ? null : new Uri(requestUri, UriKind.Relative), upstreams)
            .ShouldBe(UpstreamHttpClient.OtherPipeline);
    }

    [Fact]
    public void PipelineForShouldChooseTheEmptyPipeline_WhenNoUpstreamIsConfigured() =>
        UpstreamHttpClient.PipelineFor(new Uri("http://fake-service.test/cads/api"), new Dictionary<string, Uri>()).ShouldBe(UpstreamHttpClient.OtherPipeline);

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
    public async Task InvalidCadsOptionsShouldLeaveKrdsOnItsOwnPipeline()
    {
        // Upstream options are validated on first use, so the API can boot before the secrets are set; one upstream's
        // missing secret must not change how calls to the other are handled.
        handler.Hang();
        var krds = CreateUpstreams(krdsOverrides: new KrdsApiOptions { TimeoutSeconds = 1 }, cadsValid: false).Krds;

        var exception = await Should.ThrowAsync<ServiceUnavailableException>(
            () => krds.GetUserAccountAsync(Subject, TestContext.Current.CancellationToken));

        exception.InnerException.ShouldBeOfType<RestRequestException>().InnerException.ShouldBeOfType<TimeoutRejectedException>();
    }

    [Fact]
    public async Task InvalidKrdsOptionsShouldLeaveCadsOnItsOwnPipeline()
    {
        handler.Hang();
        var cads = CreateUpstreams(new CadsApiOptions { TimeoutSeconds = 1 }, krdsValid: false).Cads;

        var exception = await Should.ThrowAsync<ServiceUnavailableException>(
            () => cads.GetCattleByCphAsync(Cph, TestContext.Current.CancellationToken));

        exception.InnerException.ShouldBeOfType<RestRequestException>().InnerException.ShouldBeOfType<TimeoutRejectedException>();
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

    private CadsService CreateCads(CadsApiOptions overrides) => CreateUpstreams(overrides).Cads;

    /// <summary>
    /// Builds CADS and KRDS clients over one HTTP client, as Program does. An upstream marked invalid is registered
    /// with validation and no settings, so reading its options fails as it would before its secrets are set.
    /// </summary>
    private (CadsService Cads, KrdsService Krds) CreateUpstreams(
        CadsApiOptions? cadsOverrides = null,
        KrdsApiOptions? krdsOverrides = null,
        bool cadsValid = true,
        bool krdsValid = true)
    {
        cadsOverrides ??= new CadsApiOptions();
        krdsOverrides ??= new KrdsApiOptions();

        var cadsOptions = new CadsApiOptions
        {
            BaseUrl = cadsOverrides.BaseUrl is { Length: > 0 } cadsBaseUrl ? cadsBaseUrl : CadsBaseUrl,
            ClientId = nameof(UpstreamHttpClientTests),
            ClientSecret = Guid.NewGuid().ToString("N"),
            PageSize = 2,
            TimeoutSeconds = cadsOverrides.TimeoutSeconds,
            CircuitBreakerFailureThreshold = cadsOverrides.CircuitBreakerFailureThreshold,
            CircuitBreakerSamplingSeconds = cadsOverrides.CircuitBreakerSamplingSeconds,
            CircuitBreakerBreakSeconds = cadsOverrides.CircuitBreakerBreakSeconds,
        };

        var krdsOptions = new KrdsApiOptions
        {
            BaseUrl = KrdsBaseUrl,
            ClientId = nameof(UpstreamHttpClientTests),
            ClientSecret = Guid.NewGuid().ToString("N"),
            TimeoutSeconds = krdsOverrides.TimeoutSeconds,
            CircuitBreakerFailureThreshold = krdsOverrides.CircuitBreakerFailureThreshold,
            CircuitBreakerSamplingSeconds = krdsOverrides.CircuitBreakerSamplingSeconds,
            CircuitBreakerBreakSeconds = krdsOverrides.CircuitBreakerBreakSeconds,
        };

        var services = BaseServices();
        if (cadsValid)
        {
            services.AddSingleton(Microsoft.Extensions.Options.Options.Create(cadsOptions));
        }
        else
        {
            services.AddOptions<CadsApiOptions>().ValidateDataAnnotations();
        }

        if (krdsValid)
        {
            services.AddSingleton(Microsoft.Extensions.Options.Options.Create(krdsOptions));
        }
        else
        {
            services.AddOptions<KrdsApiOptions>().ValidateDataAnnotations();
        }

        var built = Build(services);

        var cads = new CadsService(
            new RestStrategyFactory<CadsService>(built),
            Microsoft.Extensions.Options.Options.Create(cadsOptions),
            Microsoft.Extensions.Options.Options.Create(new CadsDataPatchOptions()),
            Mock.Of<ICadsDataPatchStore>(),
            NullLogger<CadsService>.Instance);

        var krds = new KrdsService(
            new RestStrategyFactory<KrdsService>(built),
            new HoldingSearchRequestValidator(),
            Microsoft.Extensions.Options.Options.Create(krdsOptions),
            NullLogger<KrdsService>.Instance);

        return (cads, krds);
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
