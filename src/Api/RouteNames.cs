// <copyright file="RouteNames.cs" company="Defra">
// Copyright (c) Defra. All rights reserved.
// </copyright>

namespace Defra.Lis.Api;

/// <summary>
/// Provides route segment and path constants for API endpoints.
/// </summary>
public static class RouteNames
{
    /// <summary>
    /// The route template prefix for API versioning.
    /// </summary>
    public const string ApiVersionRoot = "/v{version:apiVersion}";

    /// <summary>
    /// The route segment for cattle endpoint.
    /// </summary>
    public const string Cattle = "cattle";

    /// <summary>
    /// The route segment for health check endpoint.
    /// </summary>
    public const string Health = "health";

    /// <summary>
    /// The route segment for holdings endpoint.
    /// </summary>
    public const string Holdings = "holdings";

    /// <summary>
    /// The route segment for registrations endpoint.
    /// </summary>
    public const string Registrations = "registrations";
}
