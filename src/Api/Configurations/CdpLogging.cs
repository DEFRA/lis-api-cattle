// <copyright file="CdpLogging.cs" company="Defra">
// Copyright (c) Defra. All rights reserved.
// </copyright>

namespace Defra.Lis.Api.Configurations;

using System.Diagnostics.CodeAnalysis;
using Serilog;

/// <summary>
/// Configures Serilog to the CDP platform standard: ECS console output (from the <c>Serilog</c> settings section)
/// with every line carrying the request's <c>x-cdp-request-id</c> as <c>CorrelationId</c>.
/// </summary>
public static class CdpLogging
{
    /// <summary>
    /// Applies the configuration; pass to <c>UseSerilog</c>.
    /// </summary>
    /// <param name="context">The host builder context.</param>
    /// <param name="configuration">The logger configuration.</param>
    [ExcludeFromCodeCoverage]
    public static void Configuration(HostBuilderContext context, LoggerConfiguration configuration)
    {
        configuration
            .ReadFrom.Configuration(context.Configuration)
            .Enrich.FromLogContext()
            .Enrich.WithCorrelationId(TraceHeaders.CdpRequestId);
    }
}
