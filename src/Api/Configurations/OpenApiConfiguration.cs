// <copyright file="OpenApiConfiguration.cs" company="Defra">
// Copyright (c) Defra. All rights reserved.
// </copyright>

namespace Defra.Lis.Api.Configurations;

using System.Globalization;
using Asp.Versioning;
using Defra.Lis.Api.Authentication;
using Defra.Lis.Api.Configurations.Metadata;
using Defra.Lis.Api.Correlation;
using Microsoft.AspNetCore.Mvc.ApiExplorer;
using Scalar.AspNetCore;

/// <summary>
/// Publishes two OpenAPI documents per API version: the public one (<c>/openapi/v1.json</c>), listing only the
/// endpoints marked with <see cref="WithPublicApi{TBuilder}"/>, and the internal one
/// (<c>/openapi/v1-internal.json</c>), listing every endpoint and readable only with the service-to-service key.
/// Both describe the same routes; only the set of operations differs.
/// </summary>
public static class OpenApiConfiguration
{
    /// <summary>Gets the suffix that names the internal document of an API version, such as <c>v1-internal</c>.</summary>
    public const string InternalSuffix = "-internal";

    private const string GroupNameFormat = "'v'VVV";
    private const string Title = "LIS Cattle API";
    private const string Description = "API for cattle holdings, registrations and submission processing.";

    // The internal documents are registered before the app is built, so the versions cannot be discovered from the
    // endpoints and must be listed here.
    private static readonly ApiVersion[] Versions = [new(1.0)];

    /// <summary>
    /// Adds URL-segment API versioning and the public and internal OpenAPI documents for every API version.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The supplied service collection.</returns>
    public static IServiceCollection AddVersionedOpenApi(this IServiceCollection services)
    {
        services.AddApiVersioning(options =>
            {
                options.ApiVersionReader = new UrlSegmentApiVersionReader();
                options.ReportApiVersions = true;
            })
            .AddApiExplorer(options =>
            {
                options.GroupNameFormat = GroupNameFormat;
                options.SubstituteApiVersionInUrl = true;
            })
            .AddOpenApi(options =>
            {
                options.Document.AddDocumentTransformer((document, _, _) =>
                {
                    document.Info.Title = Title;
                    document.Info.Description = Description;
                    return Task.CompletedTask;
                });
                options.Document.AddApiKeySecurity();

                var isInVersion = options.Document.ShouldInclude;
                options.Document.ShouldInclude = description => isInVersion(description) && IsPublic(description);
            });

        // Register plain documents last so the build-time provider discovers both public and internal documents.
        foreach (var version in Versions)
        {
            var groupName = version.ToString(GroupNameFormat, CultureInfo.InvariantCulture);
            services.AddOpenApi(groupName, options =>
            {
                options.ShouldInclude = description =>
                    (description.GroupName is null || description.GroupName == groupName) && IsPublic(description);
            });
            services.AddOpenApi(groupName + InternalSuffix, options =>
            {
                // Version-neutral endpoints (health) have no group name.
                options.ShouldInclude = description => description.GroupName is null || description.GroupName == groupName;
                options.AddDocumentTransformer((document, _, _) =>
                {
                    document.Info.Title = $"{Title} (internal)";
                    document.Info.Description = Description;
                    document.Info.Version = version.ToString();
                    return Task.CompletedTask;
                });
                options.AddApiKeySecurity();
            });
        }

        return services;
    }

    /// <summary>
    /// Maps the public documents anonymously, the internal documents behind the service-to-service policy, and Scalar
    /// over the public documents in Development only. None of them needs the correlation header.
    /// </summary>
    /// <param name="app">The web application.</param>
    /// <returns>The supplied web application.</returns>
    public static WebApplication MapVersionedOpenApi(this WebApplication app)
    {
        app.MapOpenApi("/openapi/{documentName:regex(^v\\d+$)}.json")
            .WithDocumentPerVersion()
            .WithoutCorrelationIdCheck();
        app.MapOpenApi($"/openapi/{{documentName:regex(^v\\d+{InternalSuffix}$)}}.json")
            .WithDocumentPerVersion()
            .WithoutCorrelationIdCheck()
            .RequireServiceToServiceAuthorization();

        if (app.Environment.IsDevelopment())
        {
            // Scalar fetches documents without the API key, so it only lists the public ones.
            app.MapScalarApiReference(options =>
                {
                    foreach (var groupName in app.DescribeApiVersions().Select(description => description.GroupName))
                    {
                        options.AddDocument(groupName, groupName);
                    }
                })
                .WithoutCorrelationIdCheck();
        }

        return app;
    }

    private static bool IsPublic(ApiDescription description) =>
        description.ActionDescriptor.EndpointMetadata.OfType<PublicApiMetadata>().Any();
}
