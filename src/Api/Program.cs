// <copyright file="Program.cs" company="Defra">
// Copyright (c) Defra. All rights reserved.
// </copyright>

namespace Defra.Lis.Api;

using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Text.Json;
using Asp.Versioning;
using Defra.Database.Postgres;
using Defra.Lis.Api.Authentication;
using Defra.Lis.Api.Configurations;
using Defra.Lis.Api.Endpoints;
using Defra.Lis.Api.Endpoints.Cattle;
using Defra.Lis.Api.Endpoints.Health;
using Defra.Lis.Api.Endpoints.Holding;
using Defra.Lis.Api.Endpoints.Registration;
using Defra.Lis.Api.Endpoints.Users;
using Defra.Lis.Api.Exceptions;
using Defra.Lis.Api.Interfaces;
using Defra.Lis.Api.Services;
using Defra.Lis.Api.Services.Health;
using Defra.Lis.Database;
using Defra.Livestock.Sdk.Api.Strategies;
using Defra.Livestock.Sdk.Api.Strategies.Abstractions.Operations.Http.Rest.Client;
using Defra.Livestock.Sdk.Api.Strategies.Operations.Http.Rest.Client;
using Scalar.AspNetCore;

#pragma warning disable S1075 // Using http protocol is insecure. Use https instead
#pragma warning disable S5332 // Using http protocol is insecure. Use https instead

/// <summary>
/// Entry point for the application.
/// </summary>
[ExcludeFromCodeCoverage]
public static class Program
{
    /// <summary>
    /// Application main entry method.
    /// </summary>
    /// <param name="args">Command-line arguments.</param>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    public static async Task Main(string[] args)
    {
        var app = CreateWebApplication(args);
        await app.RunAsync();
    }

    private static WebApplication CreateWebApplication(string[] args)
    {
        var isGeneratingOpenApi =
            Assembly.GetEntryAssembly()?.GetName().Name == "GetDocument.Insider";
        var builder = WebApplication.CreateBuilder(args);

        var configuration = builder.Configuration
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
            .AddJsonFile(
                $"appsettings.{builder.Environment.EnvironmentName}.json",
                optional: true,
                reloadOnChange: true)
            .AddEnvironmentVariables()
            .AddCommandLine(args);

        ConfigureBuilder(builder, isGeneratingOpenApi, configuration.Build());

        var app = builder.Build();
        return SetupApplication(app, isGeneratingOpenApi);
    }

    private static void ConfigureBuilder(
        WebApplicationBuilder builder,
        bool isGeneratingOpenApi,
        IConfigurationRoot configuration)
    {
        builder.Services.ConfigureHttpJsonOptions(options =>
        {
            options.SerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
            options.SerializerOptions.DictionaryKeyPolicy = JsonNamingPolicy.CamelCase;
        });

        // Add services to the container.
        builder.Services.AddScoped<IHealthService, HealthService>();
        builder.Services.AddHealthChecks()
            .AddCheck<DatabaseHealthCheck>("database", timeout: HealthService.DefaultCheckTimeout)
            .AddCheck<QueueHealthCheck>("queue", timeout: HealthService.DefaultCheckTimeout)
            .AddCheck<QuartzHealthCheck>("quartz", timeout: HealthService.DefaultCheckTimeout);

        builder.Services.AddPostgresDatabase(configuration);
        builder.Services.AddCattleDatabaseConfigurations();

        // Problem-details responses for NotFound / validation failures raised by services.
        builder.Services.AddProblemDetails();
        builder.Services.AddExceptionHandler<ApiExceptionHandler>();
        builder.Services.AddApiVersioning(options =>
            {
                options.ApiVersionReader = new UrlSegmentApiVersionReader();
                options.ReportApiVersions = true;
            })
            .AddApiExplorer(options =>
            {
                options.GroupNameFormat = "'v'VVV";
                options.SubstituteApiVersionInUrl = true;
            })
            .AddOpenApi(options =>
            {
                options.Document.AddDocumentTransformer((document, _, _) =>
                {
                    document.Info.Title = "LIS Cattle API";
                    document.Info.Description = "API for cattle holdings, registrations and submission processing.";
                    return Task.CompletedTask;
                });
                options.Document.AddApiKeySecurity();
            });

        // Service-to-service authentication: an API key for now, AWS STS later (LREG-560).
        builder.Services.AddServiceToServiceAuthentication(builder.Configuration);

        // Propagate the CDP correlation header to every outbound call (Correlation ID standard).
        builder.Services.AddHeaderPropagation(options => options.Headers.Add(TraceHeaders.CdpRequestId));

        // Upstream connections (CADS animals and KRDS holdings, both faked by lis-fake-service for now).
        // Validated when first used rather than on start-up so the service can boot (and answer /health)
        // before the upstream secrets are configured.
        builder.Services.AddOptions<CadsApiOptions>()
            .Bind(builder.Configuration.GetSection(CadsApiOptions.SectionName))
            .ValidateDataAnnotations();
        builder.Services.AddOptions<KrdsApiOptions>()
            .Bind(builder.Configuration.GetSection(KrdsApiOptions.SectionName))
            .ValidateDataAnnotations();

        // The REST client must be registered before the strategy factories so it carries header propagation.
        builder.Services.AddStrategyFramework();
        builder.Services.AddHttpClient<IRestHttpClient, RestHttpClient>().AddHeaderPropagation();
        builder.Services.AddRestStrategyFactory<CadsService>();
        builder.Services.AddRestStrategyFactory<KrdsService>();
        builder.Services.AddScoped<ICadsService, CadsService>();
        builder.Services.AddScoped<IKrdsService, KrdsService>();

        if (builder.Configuration.GetValue<bool>("CtsApi:UseFake", true))
        {
            builder.Services.AddSingleton<ICtsService, FakeCtsService>();
        }
        else
        {
            builder.Services.AddHttpClient<ICtsService, FakeCtsService>(client =>
            {
                client.BaseAddress = new Uri(builder.Configuration["CtsApi:BaseUrl"] ?? "http://cts-api/");
            });
        }

        builder.Services.AddScoped<ICattleService, CattleService>();
        builder.Services.AddScoped<ICtsBundleProcessorService, CtsBundleProcessorService>();
        if (!isGeneratingOpenApi)
        {
            builder.Services.AddAwsMessagingServices(builder.Configuration);
            builder.Services.AddQuartzServices(builder.Configuration);
        }
    }

    private static WebApplication SetupApplication(
        WebApplication app,
        bool isGeneratingOpenApi)
    {
        app.UseExceptionHandler();
        app.UseHeaderPropagation();
        app.UseAuthentication();
        app.UseAuthorization();

        if (!isGeneratingOpenApi)
        {
            app.UsePostgresDatabase();
        }

        app.MapOpenApi().WithDocumentPerVersion();
        app.MapScalarApiReference(options =>
        {
            foreach (var groupName in app.DescribeApiVersions().Select(description => description.GroupName))
            {
                options.AddDocument(groupName, groupName);
            }
        });

        // Every versioned endpoint requires AuthPolicies.ServiceToService; health and OpenAPI stay anonymous.
        app.MapCattleEndpoints();
        app.MapHoldingEndpoints();
        app.MapRegistrationEndpoints();
        app.MapUserEndpoints();
        app.MapHealthEndpoints();

        return app;
    }
}
