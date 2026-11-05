// <copyright file="SubjectLoggingTests.cs" company="Defra">
// Copyright (c) Defra. All rights reserved.
// </copyright>

namespace Defra.Lis.Api.Tests.Logging;

using System.Collections.Concurrent;
using System.Net;
using System.Text;
using Asp.Versioning;
using Defra.Lis.Api.Configurations;
using Defra.Lis.Api.Correlation;
using Defra.Lis.Api.Endpoints.Users;
using Defra.Lis.Api.Exceptions;
using Defra.Lis.Api.Interfaces;
using Defra.Lis.Api.Models.Requests;
using Defra.Lis.Api.Services;
using Defra.Lis.Api.Tests.Authentication;
using Defra.Lis.Api.Tests.Upstream;
using Defra.Lis.Api.Validation;
using Defra.Livestock.Sdk.Api.Strategies;
using FluentValidation;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Serilog;
using Serilog.Core;
using Serilog.Events;

/// <summary>
/// Runs the user endpoints through the logging pipeline from <c>Program</c>, against a real <see cref="KrdsService"/>
/// over a stub KRDS, and checks that no log event carries the Defra ID subject (LREG-751).
/// </summary>
[Collection(SerilogStaticLoggerCollection.Name)]
public class SubjectLoggingTests
{
    private const string Subject = "5e0c8f8a-4b1d-4c7e-9a3f-6d2b8e1f0a47";

    private const string UserAccount = $$"""
                                       {
                                         "subject": "{{Subject}}",
                                         "email": "oakfield.farmer@oakhill-farms.co.uk",
                                         "cphAssociations": [{ "cphNumber": "22/001/0001", "role": "Keeper" }]
                                       }
                                       """;

    private readonly StubHttpMessageHandler krds = new();
    private readonly CapturingSink sink = new();

    public static TheoryData<HttpStatusCode, HttpStatusCode> GetUserDetailsOutcomes => new()
    {
        { HttpStatusCode.OK, HttpStatusCode.OK },
        { HttpStatusCode.NotFound, HttpStatusCode.NotFound },
        { HttpStatusCode.UnprocessableEntity, HttpStatusCode.BadRequest },
        { HttpStatusCode.ServiceUnavailable, HttpStatusCode.ServiceUnavailable },
        { HttpStatusCode.GatewayTimeout, HttpStatusCode.ServiceUnavailable },
    };

    public static TheoryData<HttpStatusCode, HttpStatusCode> CreateUserOutcomes => new()
    {
        { HttpStatusCode.OK, HttpStatusCode.OK },
        { HttpStatusCode.BadRequest, HttpStatusCode.BadRequest },
        { HttpStatusCode.Conflict, HttpStatusCode.Conflict },
        { HttpStatusCode.ServiceUnavailable, HttpStatusCode.ServiceUnavailable },
        { HttpStatusCode.GatewayTimeout, HttpStatusCode.ServiceUnavailable },
    };

    [Theory]
    [MemberData(nameof(GetUserDetailsOutcomes))]
    public async Task GetUserDetails_LogsNoSubject(HttpStatusCode krdsStatus, HttpStatusCode expectedStatus)
    {
        krds.RespondWith(krdsStatus, krdsStatus == HttpStatusCode.OK ? UserAccount : """{"status":400}""");
        await using var app = await StartAppAsync();

        var response = await AuthenticatedClient(app).GetAsync($"/v1/users/{Subject}", TestContext.Current.CancellationToken);

        Assert.Equal(expectedStatus, response.StatusCode);
        AssertRequestLoggedWithRouteTemplate("GET", "/v{version:apiVersion}/users/{subject}");
        AssertNoEventContainsTheSubject();
    }

    [Theory]
    [MemberData(nameof(CreateUserOutcomes))]
    public async Task CreateUser_LogsNoSubject(HttpStatusCode krdsStatus, HttpStatusCode expectedStatus)
    {
        krds.RespondWith(krdsStatus, krdsStatus == HttpStatusCode.OK ? UserAccount : """{"status":400}""");
        await using var app = await StartAppAsync();

        var response = await AuthenticatedClient(app).PostAsync("/v1/users", ClaimsBody(), TestContext.Current.CancellationToken);

        Assert.Equal(expectedStatus, response.StatusCode);
        AssertNoEventContainsTheSubject();
    }

    [Fact]
    public async Task GetUserDetails_LogsNoSubject_WhenKrdsTimesOut()
    {
        krds.Hang();
        await using var app = await StartAppAsync();

        var response = await AuthenticatedClient(app).GetAsync($"/v1/users/{Subject}", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        AssertNoEventContainsTheSubject();
    }

    [Fact]
    public async Task CreateUser_LogsNoSubject_WhenKrdsTimesOut()
    {
        krds.Hang();
        await using var app = await StartAppAsync();

        var response = await AuthenticatedClient(app).PostAsync("/v1/users", ClaimsBody(), TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        AssertNoEventContainsTheSubject();
    }

    [Fact]
    public async Task GetUserDetails_LogsNoSubject_WhenTheKrdsCircuitOpens()
    {
        krds.FailWith(new HttpRequestException("Connection refused"))
            .FailWith(new HttpRequestException("Connection refused"));
        await using var app = await StartAppAsync();
        var client = AuthenticatedClient(app);

        for (var call = 0; call < 3; call++)
        {
            var response = await client.GetAsync($"/v1/users/{Subject}", TestContext.Current.CancellationToken);
            Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        }

        // Polly runs the transition callback off the request path, so the log can land just after the response.
        var deadline = DateTime.UtcNow.AddSeconds(5);
        LogEvent? opened;
        while ((opened = sink.Events.FirstOrDefault(e => e.RenderMessage().Contains("circuit opened", StringComparison.Ordinal))) is null &&
               DateTime.UtcNow < deadline)
        {
            await Task.Delay(20, TestContext.Current.CancellationToken);
        }

        Assert.NotNull(opened);
        Assert.Equal(2, krds.Requests.Count);
        Assert.False(opened.Properties.ContainsKey("CorrelationId"), "The transition carried a stale correlation ID.");
        Assert.False(opened.Properties.ContainsKey("RequestPath"), "The transition carried a stale request path.");
        AssertNoEventContainsTheSubject();
    }

    [Fact]
    public async Task GetUserDetails_LogsNoSubject_WhenTheCorrelationIdIsMissing()
    {
        await using var app = await StartAppAsync();
        var client = app.GetAuthenticatedTestClient();

        var response = await client.GetAsync($"/v1/users/{Subject}", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Empty(krds.Requests);
        Assert.Contains(sink.Events, e => e.MessageTemplate.Text.Contains(TraceHeaders.CdpRequestId, StringComparison.Ordinal));
        AssertNoEventContainsTheSubject();
    }

    [Fact]
    public async Task GetUserDetails_LogsNoSubject_WhenTheApiKeyIsMissing()
    {
        await using var app = await StartAppAsync();
        var client = app.GetTestClient();
        client.DefaultRequestHeaders.Add(TraceHeaders.CdpRequestId, "corr-751");

        var response = await client.GetAsync($"/v1/users/{Subject}", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Contains(sink.Events, e => e.MessageTemplate.Text.Contains("API key", StringComparison.Ordinal));
        AssertNoEventContainsTheSubject();
    }

    [Fact]
    public async Task UnmatchedRoute_LogsNoRawPath()
    {
        await using var app = await StartAppAsync();

        var response = await AuthenticatedClient(app).GetAsync($"/v1/user/{Subject}", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        AssertRequestLoggedWithRouteTemplate("GET", Api.Logging.LoggableRequestPath.UnmatchedRoute);
        AssertNoEventContainsTheSubject();
    }

    private static StringContent ClaimsBody() => new(
        $$"""{"sub":"{{Subject}}","email":"oakfield.farmer@oakhill-farms.co.uk","given_name":"Oakfield","family_name":"Farmer"}""",
        Encoding.UTF8,
        "application/json");

    private static HttpClient AuthenticatedClient(WebApplication app)
    {
        var client = app.GetAuthenticatedTestClient();
        client.DefaultRequestHeaders.Add(TraceHeaders.CdpRequestId, "corr-751");
        return client;
    }

    private static string Describe(LogEvent logEvent)
    {
        var text = new StringBuilder(logEvent.RenderMessage());
        foreach (var property in logEvent.Properties)
        {
            text.Append(' ').Append(property.Key).Append('=').Append(property.Value);
        }

        return text.Append(' ').Append(logEvent.Exception).ToString();
    }

    private void AssertRequestLoggedWithRouteTemplate(string method, string template)
    {
        var request = Assert.Single(sink.Events, e => e.Properties.ContainsKey("Elapsed"));
        Assert.Equal(method, ((ScalarValue)request.Properties["RequestMethod"]).Value);
        Assert.Equal(template, ((ScalarValue)request.Properties["RequestPath"]).Value);
    }

    private void AssertNoEventContainsTheSubject()
    {
        Assert.NotEmpty(sink.Events);
        Assert.All(sink.Events, e => Assert.DoesNotContain(Subject, Describe(e), StringComparison.OrdinalIgnoreCase));
    }

    private async Task<WebApplication> StartAppAsync()
    {
        var builder = WebApplication.CreateEmptyBuilder(new WebApplicationOptions());
        builder.WebHost.UseTestServer();

        // Information and above, as in production, but without the Microsoft.AspNetCore override from appsettings,
        // and everything from this service and the SDK. The overrides CdpLogging enforces in code still apply.
        builder.Services.AddHttpContextAccessor();
        builder.Host.UseSerilog((context, services, configuration) =>
        {
            CdpLogging.Configuration(context, services, configuration);
            configuration.MinimumLevel.Information()
                .MinimumLevel.Override("Defra", LogEventLevel.Verbose)
                .WriteTo.Sink(sink);
        });

        builder.Services.AddRouting();
        builder.Services.AddTestServiceToServiceAuthentication();
        builder.Services.AddApiVersioning(options => options.ApiVersionReader = new UrlSegmentApiVersionReader());
        builder.Services.AddProblemDetails();
        builder.Services.AddExceptionHandler<ApiExceptionHandler>();
        builder.Services.AddCorrelationId();

        // The KRDS client is registered as in Program, so the SDK loggers and the resilience pipeline run too.
        builder.Services.AddStrategyFramework();
        builder.Services.AddUpstreamHttpClient().ConfigurePrimaryHttpMessageHandler(() => krds);
        builder.Services.AddRestStrategyFactory<KrdsService>();
        builder.Services.AddSingleton<IValidator<HoldingSearchRequest>, HoldingSearchRequestValidator>();
        builder.Services.AddSingleton(Microsoft.Extensions.Options.Options.Create(new KrdsApiOptions
        {
            BaseUrl = "http://fake-service.test/krds",
            ClientId = nameof(SubjectLoggingTests),
            ClientSecret = Guid.NewGuid().ToString("N"),
            TimeoutSeconds = 1,
            CircuitBreakerFailureThreshold = 2,
        }));
        builder.Services.AddScoped<IKrdsService, KrdsService>();

        var app = builder.Build();
        app.UseCdpRequestLogging();
        app.UseExceptionHandler();
        app.UseCorrelationId();
        app.UseTestServiceToServiceAuthentication();
        app.MapUserEndpoints();
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
