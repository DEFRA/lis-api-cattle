// <copyright file="UpstreamHttpClient.cs" company="Defra">
// Copyright (c) Defra. All rights reserved.
// </copyright>

namespace Defra.Lis.Api.Configurations;

using Defra.Livestock.Sdk.Api.Strategies.Abstractions.Operations.Http.Rest.Client;
using Defra.Livestock.Sdk.Api.Strategies.Operations.Http.Rest.Client;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http.Resilience;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Polly;
using Polly.CircuitBreaker;
using Polly.Timeout;

/// <summary>
/// Registers the strategies SDK's HTTP client, which carries every call to CADS and KRDS, with a resilience pipeline
/// per upstream (LREG-509, LREG-510), so a slow or unreachable upstream fails fast instead of holding the request.
/// </summary>
/// <remarks>
/// <para>
/// The SDK resolves one <see cref="IRestHttpClient"/> for every strategy factory, so CADS and KRDS share this client.
/// Each request is given a pipeline by the base URL it starts with. The host is not enough: CADS and KRDS share one
/// in lis-dev (<c>lis-fake-service/cads/</c> and <c>/krds/</c>) and locally.
/// </para>
/// <para>
/// CADS and KRDS each get their own pipeline and circuit, configured by <see cref="CadsApiOptions"/> and
/// <see cref="KrdsApiOptions"/>. A pipeline times out a call that has no response headers after <c>TimeoutSeconds</c>,
/// inside a circuit breaker. The circuit opens when every call in the sampling window, and at least
/// <c>CircuitBreakerFailureThreshold</c> of them, failed without a response: a transport failure or a timeout. Any HTTP
/// response, even a 5xx, proves the upstream is up and does not count. While open, calls fail at once with a
/// <see cref="BrokenCircuitException"/>. After <c>CircuitBreakerBreakSeconds</c> one probe is let through; its outcome
/// closes or reopens the circuit. There is no retry: the callers above decide whether to retry, and retrying at several
/// layers multiplies the load. A call to any other URL gets an empty pipeline.
/// </para>
/// </remarks>
public static partial class UpstreamHttpClient
{
    /// <summary>
    /// The resilience handler's name, which also prefixes Polly's own telemetry.
    /// </summary>
    public const string PipelineName = "upstream";

    /// <summary>
    /// The pipeline for calls to CADS.
    /// </summary>
    public const string CadsPipeline = "cads";

    /// <summary>
    /// The pipeline for calls to KRDS.
    /// </summary>
    public const string KrdsPipeline = "krds";

    /// <summary>
    /// The empty pipeline for every other call.
    /// </summary>
    public const string OtherPipeline = "other";

    /// <summary>
    /// The extra time the HttpClient timeout allows a response body after the pipeline timeout for the headers.
    /// </summary>
    public static readonly TimeSpan BodyGrace = TimeSpan.FromSeconds(1);

    private const char PathSeparator = '/';

    /// <summary>
    /// Registers the SDK's <see cref="IRestHttpClient"/> with header propagation and the per-upstream pipelines.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The client builder, so tests can replace the primary handler.</returns>
    public static IHttpClientBuilder AddUpstreamHttpClient(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        // Header propagation carries x-cdp-request-id to CADS and KRDS on every call.
        // The HttpClient factory's own loggers are removed: their "HTTP GET {Uri}" scope wraps the pipeline, so every
        // event logged inside it (Polly's telemetry, the circuit transitions) would carry the raw URL, and KRDS URLs hold
        // the Defra ID subject. CdpLogging already mutes those loggers below Warning, so nothing else is lost.
        var builder = services.AddHttpClient<IRestHttpClient, RestHttpClient>()
            .RemoveAllLoggers()
            .AddHeaderPropagation();

        builder.AddResilienceHandler(PipelineName, (pipeline, context) =>
            {
                var settings = context.InstanceName switch
                {
                    CadsPipeline => CadsSettings(context.ServiceProvider),
                    KrdsPipeline => KrdsSettings(context.ServiceProvider),
                    _ => null,
                };

                if (settings is null)
                {
                    return;
                }

                var logger = context.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger(typeof(UpstreamHttpClient));
                AddCircuitBreakerAndTimeout(pipeline, settings, logger);
            })
            .SelectPipelineBy(provider =>
            {
                var upstreams = UpstreamBaseUrls(provider);
                return request => PipelineFor(request.RequestUri, upstreams);
            });

        return builder;
    }

    /// <summary>
    /// Picks the pipeline for a request: the upstream whose base URL the request's URI starts with, the longest one
    /// when several do, otherwise <see cref="OtherPipeline"/>.
    /// </summary>
    /// <param name="requestUri">The request's absolute URI.</param>
    /// <param name="upstreamBaseUrls">Each configured upstream's pipeline name and base URL.</param>
    /// <returns>The pipeline's instance name.</returns>
    public static string PipelineFor(Uri? requestUri, IReadOnlyDictionary<string, Uri> upstreamBaseUrls)
    {
        ArgumentNullException.ThrowIfNull(upstreamBaseUrls);

        if (requestUri is not { IsAbsoluteUri: true })
        {
            return OtherPipeline;
        }

        return upstreamBaseUrls
            .Where(upstream => requestUri.AbsoluteUri.StartsWith(upstream.Value.AbsoluteUri, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(upstream => upstream.Value.AbsoluteUri.Length)
            .Select(upstream => upstream.Key)
            .FirstOrDefault() ?? OtherPipeline;
    }

    /// <summary>
    /// Whether a call failed without the upstream answering: a transport failure (connection refused, DNS failure) or
    /// the pipeline timeout. The caller cancelling is not a failure of the upstream, so it is not counted.
    /// </summary>
    /// <param name="outcome">The outcome of one call through the pipeline.</param>
    /// <returns><see langword="true"/> when the call counts towards opening the circuit.</returns>
    public static bool IsFailureWithoutResponse(Outcome<HttpResponseMessage> outcome) =>
        outcome.Exception is HttpRequestException or TimeoutRejectedException;

    /// <summary>
    /// Adds the circuit breaker, then the timeout inside it, so the breaker sees a timeout as a failure.
    /// </summary>
    /// <param name="pipeline">The pipeline being built.</param>
    /// <param name="settings">The upstream's name and settings.</param>
    /// <param name="logger">Logs the circuit transitions.</param>
    public static void AddCircuitBreakerAndTimeout(
        ResiliencePipelineBuilder<HttpResponseMessage> pipeline,
        UpstreamResilience settings,
        ILogger logger)
    {
        ArgumentNullException.ThrowIfNull(pipeline);
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(logger);

        // 1 while the half-open probe is in flight, so a failed probe is logged as a reopen. One per pipeline.
        var probing = 0;

        pipeline.AddCircuitBreaker(new CircuitBreakerStrategyOptions<HttpResponseMessage>
        {
            FailureRatio = 1.0,
            MinimumThroughput = settings.FailureThreshold,
            SamplingDuration = settings.SamplingDuration,
            BreakDuration = settings.BreakDuration,
            ShouldHandle = args => ValueTask.FromResult(IsFailureWithoutResponse(args.Outcome)),
            OnOpened = args =>
            {
                // Polly opens the circuit again, through the same callback, when the half-open probe fails.
                var reopened = Interlocked.Exchange(ref probing, 0) == 1;

                return OutsideAnyRequest(() =>
                {
                    if (reopened)
                    {
                        LogCircuitReopened(logger, settings.Upstream, args.BreakDuration.TotalSeconds);
                    }
                    else
                    {
                        LogCircuitOpened(logger, settings.Upstream, settings.FailureThreshold, args.BreakDuration.TotalSeconds);
                    }
                });
            },
            OnHalfOpened = _ =>
            {
                Interlocked.Exchange(ref probing, 1);
                return OutsideAnyRequest(() => LogCircuitHalfOpened(logger, settings.Upstream));
            },
            OnClosed = _ =>
            {
                Interlocked.Exchange(ref probing, 0);
                return OutsideAnyRequest(() => LogCircuitClosed(logger, settings.Upstream));
            },
        });

        pipeline.AddTimeout(settings.Timeout);
    }

    private static UpstreamResilience CadsSettings(IServiceProvider provider)
    {
        var options = provider.GetRequiredService<IOptions<CadsApiOptions>>().Value;
        return new UpstreamResilience(
            "CADS",
            TimeSpan.FromSeconds(options.TimeoutSeconds),
            options.CircuitBreakerFailureThreshold,
            TimeSpan.FromSeconds(options.CircuitBreakerSamplingSeconds),
            TimeSpan.FromSeconds(options.CircuitBreakerBreakSeconds));
    }

    private static UpstreamResilience KrdsSettings(IServiceProvider provider)
    {
        var options = provider.GetRequiredService<IOptions<KrdsApiOptions>>().Value;
        return new UpstreamResilience(
            "KRDS",
            TimeSpan.FromSeconds(options.TimeoutSeconds),
            options.CircuitBreakerFailureThreshold,
            TimeSpan.FromSeconds(options.CircuitBreakerSamplingSeconds),
            TimeSpan.FromSeconds(options.CircuitBreakerBreakSeconds));
    }

    private static Dictionary<string, Uri> UpstreamBaseUrls(IServiceProvider provider)
    {
        var upstreams = new Dictionary<string, Uri>(StringComparer.Ordinal);
        AddBaseUrl(upstreams, CadsPipeline, () => provider.GetRequiredService<IOptions<CadsApiOptions>>().Value.BaseUrl);
        AddBaseUrl(upstreams, KrdsPipeline, () => provider.GetRequiredService<IOptions<KrdsApiOptions>>().Value.BaseUrl);
        return upstreams;
    }

    private static void AddBaseUrl(Dictionary<string, Uri> upstreams, string pipeline, Func<string> baseUrl)
    {
        // Upstream options are validated when first used, so the API can start (and answer /health) before the secrets
        // are set. Until an upstream's options are valid no call is sent to it, so it simply gets no pipeline; and one
        // upstream's missing secret must not stop calls to the other.
        string configured;
        try
        {
            configured = baseUrl();
        }
        catch (OptionsValidationException)
        {
            return;
        }

        if (!Uri.TryCreate(configured, UriKind.Absolute, out var uri))
        {
            return;
        }

        // The SDK joins the base URL and the resource, so "…/cads" and "…/cads/" address the same calls.
        upstreams[pipeline] = uri.AbsoluteUri.EndsWith(PathSeparator) ? uri : new Uri(uri.AbsoluteUri + PathSeparator);
    }

    /// <summary>
    /// Runs a circuit transition log on a task that carries no request context. Polly runs the breaker's callbacks on
    /// its own task, which captured the execution context of the request that first used the pipeline, so without this
    /// every transition would be logged with that request's correlation ID and log scope, long after it ended.
    /// </summary>
    private static ValueTask OutsideAnyRequest(Action log)
    {
        using (ExecutionContext.SuppressFlow())
        {
            return new ValueTask(Task.Run(log));
        }
    }
}
