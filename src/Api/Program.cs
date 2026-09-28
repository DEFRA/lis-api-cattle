// <copyright file="Program.cs" company="Defra">
// Copyright (c) Defra. All rights reserved.
// </copyright>

using System.Reflection;
using System.Text.Json;
using Asp.Versioning;
using Defra.Database.Postgres;
using Defra.Lis.Api.Configurations;
using Defra.Lis.Api.Endpoints.Cattle;
using Defra.Lis.Api.Endpoints.Holding;
using Defra.Lis.Api.Endpoints.Registration;
using Defra.Lis.Api.Endpoints.Users;
using Defra.Lis.Api.Exceptions;
using Defra.Lis.Api.Interfaces;
using Defra.Lis.Api.Services;
using Defra.Lis.Database;
using Defra.Livestock.Sdk.Api.Strategies;
using Defra.Livestock.Sdk.Api.Strategies.Abstractions.Operations.Http.Rest.Client;
using Defra.Livestock.Sdk.Api.Strategies.Operations.Http.Rest.Client;
using Scalar.AspNetCore;

#pragma warning disable S1075 // Using http protocol is insecure. Use https instead
#pragma warning disable S5332 // Using http protocol is insecure. Use https instead

var builder = WebApplication.CreateBuilder(args);
var isGeneratingOpenApi = Assembly.GetEntryAssembly()?.GetName().Name == "GetDocument.Insider";

builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
    options.SerializerOptions.DictionaryKeyPolicy = JsonNamingPolicy.CamelCase;
});

// Add services to the container.
builder.Services.AddHealthChecks();
builder.Services.AddPostgresDatabase(builder.Configuration);
builder.Services.AddCattleDatabaseConfigurations();

// Problem-details responses for NotFound / validation failures raised by services.
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<ApiExceptionHandler>();
builder.Services.AddApiVersioning(options =>
    {
        options.DefaultApiVersion = new ApiVersion(1.0);
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
    });

// Propagate the CDP correlation header to every outbound call (Correlation ID standard).
builder.Services.AddHeaderPropagation(options => options.Headers.Add(TraceHeaders.CdpRequestId));
builder.Services.AddApiVersioning();

// Upstream connections (CADS animals, KRDS holdings and user accounts, both faked by lis-fake-service for now).
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

var app = builder.Build();
app.UseExceptionHandler();
app.UseHeaderPropagation();
app.UseHealthChecks("/health");

if (!isGeneratingOpenApi)
{
    app.UsePostgresDatabase();
}

if (app.Environment.IsDevelopment())
{
    if (!isGeneratingOpenApi)
    {
        await app.SeedDevelopmentDatabaseAsync();
    }

    app.MapOpenApi().WithDocumentPerVersion();
    app.MapScalarApiReference(options =>
    {
        foreach (var groupName in app.DescribeApiVersions().Select(description => description.GroupName))
        {
            options.AddDocument(groupName, groupName);
        }
    });
}

app.MapCattleEndpoints();
app.MapHoldingEndpoints();
app.MapRegistrationEndpoints();
app.MapUserEndpoints();

await app.RunAsync();
