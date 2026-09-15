// <copyright file="Program.cs" company="Defra">
// Copyright (c) Defra. All rights reserved.
// </copyright>

using System.Text.Json;
using Defra.Database.Postgres;
using Defra.Lis.Api.Configurations;
using Defra.Lis.Api.Endpoints;
using Defra.Lis.Api.Exceptions;
using Defra.Lis.Api.Interfaces;
using Defra.Lis.Api.Services;
using Defra.Lis.Database;
using Defra.Livestock.Sdk.Api.Strategies;
using Defra.Livestock.Sdk.Api.Strategies.Abstractions.Operations.Http.Rest.Client;
using Defra.Livestock.Sdk.Api.Strategies.Operations.Http.Rest.Client;

#pragma warning disable S1075 // Using http protocol is insecure. Use https instead
#pragma warning disable S5332 // Using http protocol is insecure. Use https instead

var builder = WebApplication.CreateBuilder(args);

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

// Propagate the CDP correlation header to every outbound call (Correlation ID standard).
builder.Services.AddHeaderPropagation(options => options.Headers.Add(TraceHeaders.CdpRequestId));

// Upstream connections (CADS animals and KRDS holdings, both faked by lis-fake-service for now).
builder.Services.AddOptions<CadsApiOptions>()
    .Bind(builder.Configuration.GetSection(CadsApiOptions.SectionName))
    .ValidateDataAnnotations()
    .ValidateOnStart();
builder.Services.AddOptions<KrdsApiOptions>()
    .Bind(builder.Configuration.GetSection(KrdsApiOptions.SectionName))
    .ValidateDataAnnotations()
    .ValidateOnStart();

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

builder.Services.AddAwsMessagingServices(builder.Configuration);
builder.Services.AddQuartzServices(builder.Configuration);

var app = builder.Build();
app.UseExceptionHandler();
app.UseHeaderPropagation();
app.UseHealthChecks("/health");
app.UsePostgresDatabase();

if (app.Environment.IsDevelopment())
{
    await app.SeedDevelopmentDatabaseAsync();
}

app.MapCattleEndpoints();
app.MapRegistrationEndpoints();

await app.RunAsync();
