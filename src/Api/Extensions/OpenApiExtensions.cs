// <copyright file="OpenApiExtensions.cs" company="Defra">
// Copyright (c) Defra. All rights reserved.
// </copyright>

namespace Microsoft.AspNetCore.Builder;

using Defra.Lis.Api.Configurations.Metadata;

public static class OpenApiExtensions
{
    /// <summary>
    /// Lists the endpoints in the public OpenAPI document as well as the internal one. Endpoints are internal-only
    /// unless marked.
    /// </summary>
    /// <typeparam name="TBuilder">The endpoint convention builder type.</typeparam>
    /// <param name="builder">The endpoint or group builder.</param>
    /// <returns>The supplied builder.</returns>
    public static TBuilder WithPublicApi<TBuilder>(this TBuilder builder)
        where TBuilder : IEndpointConventionBuilder =>
        builder.WithMetadata(PublicApiMetadata.Instance);
}
