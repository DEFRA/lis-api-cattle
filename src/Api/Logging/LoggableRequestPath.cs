// <copyright file="LoggableRequestPath.cs" company="Defra">
// Copyright (c) Defra. All rights reserved.
// </copyright>

namespace Defra.Lis.Api.Logging;

using Microsoft.AspNetCore.Diagnostics;

/// <summary>
/// Gives the request path in a form that is safe to log. Raw paths can carry personal data, such as the Defra ID
/// subject in <c>/v1/users/{subject}</c>, so logs use the matched route template instead.
/// </summary>
public static class LoggableRequestPath
{
    /// <summary>The value logged when the request matched no route, as its raw path could still carry personal data.</summary>
    public const string UnmatchedRoute = "(unmatched route)";

    /// <summary>
    /// Returns the route template of the endpoint that matched the request, for example
    /// <c>/v{version:apiVersion}/users/{subject}</c>.
    /// </summary>
    /// <param name="context">The HTTP context of the request.</param>
    /// <returns>The route template, or <see cref="UnmatchedRoute"/> when no route matched.</returns>
    public static string For(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        // The exception handler middleware clears the endpoint before its handlers run, but keeps it on the feature.
        var endpoint = context.GetEndpoint() ?? context.Features.Get<IExceptionHandlerFeature>()?.Endpoint;

        return endpoint is RouteEndpoint { RoutePattern.RawText: { Length: > 0 } template }
            ? template
            : UnmatchedRoute;
    }
}
