// <copyright file="CorrelationIdExtensions.cs" company="Defra">
// Copyright (c) Defra. All rights reserved.
// </copyright>

namespace Defra.Lis.Api.Correlation;

using Defra.Lis.Api.Configurations;

/// <summary>
/// Registers correlation ID enforcement and propagation (Correlation ID standard).
/// </summary>
public static class CorrelationIdExtensions
{
    /// <summary>
    /// Adds the correlation middleware and propagation of <c>x-cdp-request-id</c> to outbound HTTP clients that opt in
    /// with <c>AddHeaderPropagation()</c>.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The supplied service collection.</returns>
    public static IServiceCollection AddCorrelationId(this IServiceCollection services)
    {
        services.AddProblemDetails();
        services.AddTransient<CorrelationIdMiddleware>();
        services.AddHeaderPropagation(options => options.Headers.Add(TraceHeaders.CdpRequestId));
        return services;
    }

    /// <summary>
    /// Enforces the correlation header and then captures it for propagation. Must run after routing (implicit in a
    /// minimal API host) so endpoint metadata is available, and before authentication so its logs carry the ID.
    /// </summary>
    /// <param name="app">The application builder.</param>
    /// <returns>The supplied application builder.</returns>
    public static IApplicationBuilder UseCorrelationId(this IApplicationBuilder app)
    {
        app.UseMiddleware<CorrelationIdMiddleware>();
        app.UseHeaderPropagation();
        return app;
    }

    /// <summary>
    /// Exempts the endpoints from requiring the correlation header (health checks, OpenAPI documents).
    /// </summary>
    /// <typeparam name="TBuilder">The endpoint convention builder type.</typeparam>
    /// <param name="builder">The endpoint or group builder.</param>
    /// <returns>The supplied builder.</returns>
    public static TBuilder WithoutCorrelationIdCheck<TBuilder>(this TBuilder builder)
        where TBuilder : IEndpointConventionBuilder
    {
        builder.Add(endpoint => endpoint.Metadata.Add(new IgnoreCorrelationIdCheck()));
        return builder;
    }
}
