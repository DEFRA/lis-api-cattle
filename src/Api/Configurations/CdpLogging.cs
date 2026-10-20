// <copyright file="CdpLogging.cs" company="Defra">
// Copyright (c) Defra. All rights reserved.
// </copyright>

namespace Defra.Lis.Api.Configurations;

using Defra.Lis.Api.Logging;
using Defra.Livestock.Sdk.Api.Strategies.Operations.Http.Rest.Client;
using Serilog;
using Serilog.Events;

/// <summary>
/// Configures Serilog to the CDP platform standard: ECS console output (from the <c>Serilog</c> settings section)
/// with every line carrying the request's <c>x-cdp-request-id</c> as <c>CorrelationId</c>.
/// </summary>
public static class CdpLogging
{
    /// <summary>
    /// The loggers that write raw URLs at Information: inbound requests, and outbound calls to upstream APIs. These
    /// URLs carry personal data, such as the Defra ID subject in <c>/v1/users/{subject}</c> and
    /// <c>api/v2/user-accounts/{subject}</c>, so these loggers only log warnings and above.
    /// </summary>
    public static readonly IReadOnlyList<string> RawUrlLoggers =
    [
        "Microsoft.AspNetCore.Hosting.Diagnostics",
        "System.Net.Http.HttpClient",
        typeof(RestHttpClient).FullName!,
    ];

    /// <summary>
    /// Applies the configuration; pass to <c>UseSerilog</c>. Needs <c>AddHttpContextAccessor()</c>.
    /// </summary>
    /// <param name="context">The host builder context.</param>
    /// <param name="services">The application services.</param>
    /// <param name="configuration">The logger configuration.</param>
    public static void Configuration(HostBuilderContext context, IServiceProvider services, LoggerConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        configuration
            .ReadFrom.Configuration(context.Configuration)
            .Enrich.FromLogContext()
            .Enrich.WithCorrelationId(TraceHeaders.CdpRequestId)
            .Enrich.With(new LoggableRequestPathEnricher(services.GetRequiredService<IHttpContextAccessor>()));

        // Applied after the settings section so configuration cannot turn the URL logging back on.
        foreach (var source in RawUrlLoggers)
        {
            configuration.MinimumLevel.Override(source, LogEventLevel.Warning);
        }
    }

    /// <summary>
    /// Adds Serilog request logging that records the matched route template as <c>RequestPath</c>, never the raw
    /// path, which can carry personal data.
    /// </summary>
    /// <param name="app">The application builder.</param>
    /// <returns>The same application builder.</returns>
    public static IApplicationBuilder UseCdpRequestLogging(this IApplicationBuilder app) =>
        app.UseSerilogRequestLogging(options =>
            options.GetMessageTemplateProperties = (httpContext, _, elapsedMs, statusCode) =>
            [
                new LogEventProperty("RequestMethod", new ScalarValue(httpContext.Request.Method)),
                new LogEventProperty("RequestPath", new ScalarValue(LoggableRequestPath.For(httpContext))),
                new LogEventProperty("StatusCode", new ScalarValue(statusCode)),
                new LogEventProperty("Elapsed", new ScalarValue(elapsedMs)),
            ]);
}
