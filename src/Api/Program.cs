// <copyright file="Program.cs" company="Defra">
// Copyright (c) Defra. All rights reserved.
// </copyright>

using System.Text.Json;
using Defra.Database.Postgres;
using Defra.Lis.Api.Configurations;
using Defra.Lis.Api.Endpoints;
using Defra.Lis.Api.Interfaces;
using Defra.Lis.Api.Services;
using Defra.Lis.Database;
using Lis.Cattle;

#pragma warning disable S1075 // Using http protocol is insecure. Use https instead
#pragma warning disable S5332 // Using http protocol is insecure. Use https instead

var builder = WebApplication.CreateBuilder(args);

builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
    options.SerializerOptions.DictionaryKeyPolicy = JsonNamingPolicy.CamelCase;
});

// Add services to the container.
builder.Services.AddPostgresDatabase(builder.Configuration);
builder.Services.AddCattleDatabaseConfigurations();

builder.Services.AddHttpClient<ICadsService, CadsService>(client =>
{
    client.BaseAddress = new Uri(builder.Configuration["CadsApi:BaseUrl"] ?? "http://cads-api/");
});

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

app.UsePostgresDatabase();

if (app.Environment.IsDevelopment())
{
    await app.SeedDevelopmentDatabaseAsync();
}

app.MapCattleEndpoints();
app.MapRegistrationEndpoints();

await app.RunAsync();
