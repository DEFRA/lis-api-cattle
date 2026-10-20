// <copyright file="LoggableRequestPathEnricher.cs" company="Defra">
// Copyright (c) Defra. All rights reserved.
// </copyright>

namespace Defra.Lis.Api.Logging;

using Serilog.Core;
using Serilog.Events;

/// <summary>
/// Sets <c>RequestPath</c> on every event logged during a request to the matched route template. ASP.NET Core's
/// hosting log scope adds the raw path as <c>RequestPath</c> to each of those events, and the raw path can carry
/// personal data such as the Defra ID subject.
/// </summary>
/// <param name="httpContextAccessor">Gives the current request, if any.</param>
public sealed class LoggableRequestPathEnricher(IHttpContextAccessor httpContextAccessor) : ILogEventEnricher
{
    /// <summary>The property the hosting log scope and Serilog request logging write the path to.</summary>
    public const string PropertyName = "RequestPath";

    /// <inheritdoc />
    public void Enrich(LogEvent logEvent, ILogEventPropertyFactory propertyFactory)
    {
        ArgumentNullException.ThrowIfNull(logEvent);
        ArgumentNullException.ThrowIfNull(propertyFactory);

        var httpContext = httpContextAccessor.HttpContext;
        if (httpContext is null)
        {
            return;
        }

        // Overwrites the scope's value whichever runs first: the scope only adds the property when it is absent.
        logEvent.AddOrUpdateProperty(propertyFactory.CreateProperty(PropertyName, LoggableRequestPath.For(httpContext)));
    }
}
