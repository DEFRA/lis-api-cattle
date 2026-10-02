// <copyright file="ServiceToServiceEndpointsTests.cs" company="Defra">
// Copyright (c) Defra. All rights reserved.
// </copyright>

namespace Defra.Lis.Api.Tests.Authentication;

using System.Net;
using System.Text.Json;
using Asp.Versioning;
using Defra.Lis.Api.Authentication;
using Defra.Lis.Api.Endpoints.Cattle;
using Defra.Lis.Api.Endpoints.Holding;
using Defra.Lis.Api.Endpoints.Registration;
using Defra.Lis.Api.Endpoints.Users;
using Defra.Lis.Api.Interfaces;
using Defra.Lis.Api.Validation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Moq;

public class ServiceToServiceEndpointsTests
{
    [Fact]
    public async Task EveryVersionedEndpoint_RequiresTheServiceToServicePolicy()
    {
        await using var app = await StartAppAsync();

        var endpoints = app.Services.GetRequiredService<EndpointDataSource>().Endpoints.OfType<RouteEndpoint>().ToList();

        Assert.NotEmpty(endpoints);
        Assert.All(endpoints, endpoint =>
            Assert.Contains(endpoint.Metadata.GetOrderedMetadata<IAuthorizeData>(), data => data.Policy == AuthPolicies.ServiceToService));
    }

    [Theory]
    [InlineData("GET", "/v1/cattle/UK123456700001")]
    [InlineData("GET", "/v1/holdings/22/001/0001")]
    [InlineData("GET", "/v1/holdings/22/001/0001/cattle")]
    [InlineData("GET", "/v1/holdings/22-001-0001/bundles")]
    [InlineData("POST", "/v1/registrations/")]
    [InlineData("POST", "/v1/registrations/2f1d5c4e-0b7a-4f7e-9a51-6c3d2e1f0a9b/validate")]
    [InlineData("GET", "/v1/users/0b6f2f0e-3c1a-4e8e-9d4b-2f6a1c9e7d51")]
    public async Task Request_WithoutTheKey_IsRejectedBeforeReachingTheService(string method, string path)
    {
        await using var app = await StartAppAsync();
        using var request = new HttpRequestMessage(new HttpMethod(method), path);

        var response = await app.GetTestClient().SendAsync(request, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Request_WithTheKey_ReachesTheEndpoint()
    {
        await using var app = await StartAppAsync();

        var response = await app.GetAuthenticatedTestClient().GetAsync("/v1/holdings/22-001-0001/bundles", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task OpenApiDocument_DeclaresTheApiKeySchemeOnProtectedOperationsOnly()
    {
        var builder = WebApplication.CreateEmptyBuilder(new WebApplicationOptions());
        builder.WebHost.UseTestServer();
        builder.Services.AddRouting();
        builder.Services.AddTestServiceToServiceAuthentication();
        builder.Services.AddOpenApi(options => options.AddApiKeySecurity());
        await using var app = builder.Build();
        app.UseTestServiceToServiceAuthentication();
        app.MapOpenApi();
        app.MapGet("/protected", () => "ok").WithName("Protected").RequireServiceToServiceAuthorization();
        app.MapGet("/anonymous", () => "ok").WithName("Anonymous");
        await app.StartAsync(TestContext.Current.CancellationToken);

        var json = await app.GetTestClient().GetStringAsync("/openapi/v1.json", TestContext.Current.CancellationToken);

        using var document = JsonDocument.Parse(json);
        var scheme = document.RootElement.GetProperty("components").GetProperty("securitySchemes").GetProperty("ApiKey");
        Assert.Equal("apiKey", scheme.GetProperty("type").GetString());
        Assert.Equal("header", scheme.GetProperty("in").GetString());
        Assert.Equal("x-api-key", scheme.GetProperty("name").GetString());
        var paths = document.RootElement.GetProperty("paths");
        var protectedOperation = paths.GetProperty("/protected").GetProperty("get");
        Assert.True(protectedOperation.GetProperty("security")[0].TryGetProperty("ApiKey", out _));
        Assert.True(protectedOperation.GetProperty("responses").TryGetProperty("401", out _));
        Assert.False(paths.GetProperty("/anonymous").GetProperty("get").TryGetProperty("security", out _));
    }

    private static async Task<WebApplication> StartAppAsync()
    {
        var builder = WebApplication.CreateEmptyBuilder(new WebApplicationOptions());
        builder.WebHost.UseTestServer();
        builder.Services.AddRouting();
        builder.Services.AddApiVersioning(options => options.ApiVersionReader = new UrlSegmentApiVersionReader());
        builder.Services.AddTestServiceToServiceAuthentication();
        var cattleService = new Mock<ICattleService>();
        cattleService.Setup(s => s.GetBundlesForHoldingAsync(It.IsAny<string>())).ReturnsAsync([]);
        builder.Services.AddSingleton(cattleService.Object);
        builder.Services.AddSingleton(Mock.Of<IKrdsService>());
        builder.Services.AddSingleton(Mock.Of<ISubmissionValidationService>());
        var app = builder.Build();
        app.UseTestServiceToServiceAuthentication();
        app.MapCattleEndpoints();
        app.MapHoldingEndpoints();
        app.MapRegistrationEndpoints();
        app.MapUserEndpoints();
        await app.StartAsync(TestContext.Current.CancellationToken);
        return app;
    }
}
