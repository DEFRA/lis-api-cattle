// <copyright file="OpenApiMetadata.cs" company="Defra">
// Copyright (c) Defra. All rights reserved.
// </copyright>

namespace Defra.Lis.Api.Endpoints.Health;

/// <summary>
/// Contains OpenAPI metadata for health endpoints.
/// </summary>
public static class OpenApiMetadata
{
    /// <summary>Gets the OpenAPI tag applied to health endpoints.</summary>
    public const string Tag = "Health";

    /// <summary>Contains metadata for the basic health check endpoint.</summary>
    public static class GetHealthRoute
    {
        /// <summary>Gets the endpoint route name.</summary>
        public const string Name = "GetHealth";

        /// <summary>Gets the endpoint summary.</summary>
        public const string Summary = "Get the health status";

        /// <summary>Gets the endpoint description.</summary>
        public const string Description = "Checks the overall health status of the application and its dependencies.";
    }

    /// <summary>Contains metadata for the detailed health check endpoint.</summary>
    public static class GetDetailedHealthRoute
    {
        /// <summary>Gets the endpoint route name.</summary>
        public const string Name = "GetDetailedHealth";

        /// <summary>Gets the endpoint summary.</summary>
        public const string Summary = "Get detailed health status";

        /// <summary>Gets the endpoint description.</summary>
        public const string Description = "Performs and returns a detailed health check report for all downstream dependencies including database, queue, and background jobs.";
    }
}
