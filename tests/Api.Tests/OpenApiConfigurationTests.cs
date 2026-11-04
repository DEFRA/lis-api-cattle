// <copyright file="OpenApiConfigurationTests.cs" company="Defra">
// Copyright (c) Defra. All rights reserved.
// </copyright>

namespace Defra.Lis.Api.Tests;

using System.Net;
using System.Text.Json;
using Defra.Lis.Api.Configurations;
using Defra.Lis.Api.Endpoints.Cattle;
using Defra.Lis.Api.Endpoints.Health;
using Defra.Lis.Api.Endpoints.Holding;
using Defra.Lis.Api.Endpoints.Registration;
using Defra.Lis.Api.Endpoints.S3;
using Defra.Lis.Api.Endpoints.Users;
using Defra.Lis.Api.Interfaces;
using Defra.Lis.Api.Tests.Authentication;
using Defra.Lis.Api.Validation;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Moq;

public class OpenApiConfigurationTests
{
    // The operations deliberately published to external consumers. Add an operationId here when an endpoint is
    // marked WithPublicApi(); anything else appearing in v1.json is a leak.
    private static readonly string[] PublicCattleApiOperations =
    [
        "CreateRegistrationBundle",
        "GetBundlesForHolding",
        "GetBundlesForHoldingMultiSegment",
        "GetCattleDetails",
        "GetCattleForHolding",
        "GetCattleForHoldingMultiSegment",
        "GetDetailedHealth",
        "GetHolding",
        "SearchHoldings",
    ];

    [Fact]
    public async Task PublicDocument_ListsOnlyTheEndpointsMarkedPublic()
    {
        await using var app = await StartTestAppAsync();

        using var document = await GetDocumentAsync(app.GetTestClient(), "v1");

        Assert.Equal(["/v1/public"], PathsOf(document));
        Assert.Equal("LIS Cattle API", document.RootElement.GetProperty("info").GetProperty("title").GetString());
    }

    [Fact]
    public async Task InternalDocument_ListsEveryEndpointOfTheVersionAndTheVersionNeutralOnes()
    {
        await using var app = await StartTestAppAsync();

        using var document = await GetDocumentAsync(app.GetAuthenticatedTestClient(), "v1-internal");

        Assert.Equal(["/neutral", "/v1/internal", "/v1/public"], PathsOf(document).Order());
        var info = document.RootElement.GetProperty("info");
        Assert.Equal("LIS Cattle API (internal)", info.GetProperty("title").GetString());
        Assert.Equal("1.0", info.GetProperty("version").GetString());
        Assert.True(document.RootElement.GetProperty("components").GetProperty("securitySchemes").TryGetProperty("ApiKey", out _));
    }

    [Fact]
    public async Task InternalDocument_WithoutTheKey_IsUnauthorized()
    {
        await using var app = await StartTestAppAsync();

        var response = await app.GetTestClient().GetAsync("/openapi/v1-internal.json", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Theory]
    [InlineData("v2")]
    [InlineData("v2-internal")]
    [InlineData("internal")]
    public async Task UnknownDocument_IsNotFound(string documentName)
    {
        await using var app = await StartTestAppAsync();

        var response = await app.GetAuthenticatedTestClient()
            .GetAsync($"/openapi/{documentName}.json", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task PublicDocument_ForTheCattleApi_ListsOnlyTheAgreedOperations()
    {
        await using var app = await StartCattleApiAsync();

        using var publicDocument = await GetDocumentAsync(app.GetTestClient(), "v1");
        using var internalDocument = await GetDocumentAsync(app.GetAuthenticatedTestClient(), "v1-internal");

        Assert.Equal(PublicCattleApiOperations.Order(), OperationIdsOf(publicDocument).Order());
        Assert.Subset(OperationIdsOf(internalDocument).ToHashSet(), PublicCattleApiOperations.ToHashSet());
    }

    [Theory]
    [InlineData("Development", HttpStatusCode.OK)]
    [InlineData("Staging", HttpStatusCode.NotFound)]
    [InlineData("Production", HttpStatusCode.NotFound)]
    public async Task Scalar_IsAvailableOnlyInDevelopment(string environment, HttpStatusCode expectedStatus)
    {
        await using var app = await StartTestAppAsync(environment);
        var client = app.GetTestClient();

        var response = await client.GetAsync("/scalar/v1", TestContext.Current.CancellationToken);

        Assert.Equal(expectedStatus, response.StatusCode);
        using var publicDocument = await GetDocumentAsync(client, "v1");
        using var internalDocument = await GetDocumentAsync(app.GetAuthenticatedTestClient(), "v1-internal");
        Assert.NotEmpty(PathsOf(publicDocument));
        Assert.NotEmpty(PathsOf(internalDocument));
    }

    private static async Task<WebApplication> StartTestAppAsync(string environment = "Production")
    {
        var builder = CreateBuilder(environment);
        var app = builder.Build();
        app.UseTestServiceToServiceAuthentication();
        app.MapVersionedOpenApi();
        var versionOne = app.NewVersionedApi().MapGroup(RouteNames.ApiVersionRoot).HasApiVersion(1.0);
        versionOne.MapGet("/public", () => "ok").WithName("Public").WithPublicApi();
        versionOne.MapGet("/internal", () => "ok").WithName("Internal");
        app.MapGet("/neutral", () => "ok").WithName("Neutral");
        await app.StartAsync(TestContext.Current.CancellationToken);
        return app;
    }

    private static async Task<WebApplication> StartCattleApiAsync()
    {
        var builder = CreateBuilder();
        builder.Services.AddSingleton(Mock.Of<ICattleService>());
        builder.Services.AddSingleton(Mock.Of<IKrdsService>());
        builder.Services.AddSingleton(Mock.Of<ISubmissionValidationService>());
        builder.Services.AddSingleton(Mock.Of<ICadsDataPatchStore>());
        builder.Services.AddSingleton(Mock.Of<IHealthService>());
        builder.Services.AddHealthChecks();
        var app = builder.Build();
        app.UseTestServiceToServiceAuthentication();
        app.MapVersionedOpenApi();
        app.MapCattleEndpoints();
        app.MapHoldingEndpoints();
        app.MapRegistrationEndpoints();
        app.MapUserEndpoints();
        app.MapAnimalEnrichmentEndpoints();
        app.MapHealthEndpoints();
        await app.StartAsync(TestContext.Current.CancellationToken);
        return app;
    }

    private static WebApplicationBuilder CreateBuilder(string environment = "Production")
    {
        var builder = WebApplication.CreateEmptyBuilder(new WebApplicationOptions { EnvironmentName = environment });
        builder.WebHost.UseTestServer();
        builder.Services.AddRouting();
        builder.Services.AddTestServiceToServiceAuthentication();
        builder.Services.AddVersionedOpenApi();
        return builder;
    }

    private static async Task<JsonDocument> GetDocumentAsync(HttpClient client, string documentName)
    {
        var json = await client.GetStringAsync($"/openapi/{documentName}.json", TestContext.Current.CancellationToken);
        return JsonDocument.Parse(json);
    }

    private static IEnumerable<string> PathsOf(JsonDocument document) =>
        document.RootElement.GetProperty("paths").EnumerateObject().Select(path => path.Name);

    private static IEnumerable<string> OperationIdsOf(JsonDocument document) =>
        document.RootElement.GetProperty("paths").EnumerateObject()
            .SelectMany(path => path.Value.EnumerateObject())
            .Select(operation => operation.Value.GetProperty("operationId").GetString() ?? string.Empty);
}
