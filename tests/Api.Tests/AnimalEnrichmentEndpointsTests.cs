// <copyright file="AnimalEnrichmentEndpointsTests.cs" company="Defra">
// Copyright (c) Defra. All rights reserved.
// </copyright>

namespace Defra.Lis.Api.Tests;

using System.Net;
using System.Net.Http.Json;
using System.Text;
using Asp.Versioning;
using Defra.Lis.Api.Endpoints.S3;
using Defra.Lis.Api.Exceptions;
using Defra.Lis.Api.Interfaces;
using Defra.Lis.Api.Models.Cads;
using Defra.Lis.Api.Tests.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Moq;

public class AnimalEnrichmentEndpointsTests
{
    private const string Cph = "22/001/0001";
    private const string Root = "/v1/animal-enrichment";
    private readonly Mock<ICadsDataPatchStore> store = new();

    [Fact]
    public async Task Browse_ReturnsHoldingsAndTheirEarTags()
    {
        store.Setup(s => s.GetCphListAsync(It.IsAny<CancellationToken>())).ReturnsAsync([Cph]);
        store.Setup(s => s.GetCphEarTagsAsync(Cph, It.IsAny<CancellationToken>())).ReturnsAsync(["UK1"]);
        await using var app = await StartAppAsync();
        var client = app.GetAuthenticatedTestClient();

        (await client.GetFromJsonAsync<string[]>($"{Root}/", TestContext.Current.CancellationToken) ?? Array.Empty<string>()).ShouldBe([Cph]);
        (await client.GetFromJsonAsync<string[]>($"{Root}/{Cph}", TestContext.Current.CancellationToken) ?? Array.Empty<string>()).ShouldBe(["UK1"]);
    }

    [Fact]
    public async Task Get_ReturnsTheAnimalOr404()
    {
        store.Setup(s => s.GetAnimalAsync(Cph, "UK1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CadsAnimal(new CadsIdentifier(null, "UK1"), null, null, null, null, null, null, "Alive"));
        await using var app = await StartAppAsync();
        var client = app.GetAuthenticatedTestClient();

        var response = await client.GetAsync($"{Root}/{Cph}/UK1", TestContext.Current.CancellationToken);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken)).ShouldContain("UK1");
        var missing = await client.GetAsync($"{Root}/{Cph}/UK2", TestContext.Current.CancellationToken);
        missing.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Put_PassesJsonToTheStoreAndReturnsNoContent()
    {
        const string json = """{"identifier":{"identifier":"UK1"}}""";
        store.Setup(s => s.UpsertAnimalAsync(Cph, "UK1", json, It.IsAny<CancellationToken>())).ReturnsAsync(true);
        await using var app = await StartAppAsync();
        using var body = new StringContent(json, Encoding.UTF8, "application/json");

        var response = await app.GetAuthenticatedTestClient().PutAsync($"{Root}/{Cph}/UK1", body, TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        store.Verify(s => s.UpsertAnimalAsync(Cph, "UK1", json, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Theory]
    [InlineData("PUT")]
    [InlineData("DELETE")]
    public async Task Writes_Return503WhenTheBucketIsDisabled(string method)
    {
        await using var app = await StartAppAsync();
        using var request = new HttpRequestMessage(new HttpMethod(method), $"{Root}/{Cph}/UK1");
        if (method == "PUT")
        {
            request.Content = new StringContent("{}", Encoding.UTF8, "application/json");
        }

        var response = await app.GetAuthenticatedTestClient().SendAsync(request, TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.ServiceUnavailable);
    }

    [Fact]
    public async Task Delete_ReturnsNoContent()
    {
        store.Setup(s => s.DeleteAnimalAsync(Cph, "UK1", It.IsAny<CancellationToken>())).ReturnsAsync(true);
        await using var app = await StartAppAsync();

        var response = await app.GetAuthenticatedTestClient().DeleteAsync($"{Root}/{Cph}/UK1", TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        store.Verify(s => s.DeleteAnimalAsync(Cph, "UK1", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Put_Returns400WhenAnimalValidationFails()
    {
        store.Setup(s => s.UpsertAnimalAsync(Cph, "UK1", It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ArgumentException("The animal identifier must match the ear tag in the path."));
        await using var app = await StartAppAsync();
        using var body = new StringContent("{}", Encoding.UTF8, "application/json");

        var response = await app.GetAuthenticatedTestClient().PutAsync($"{Root}/{Cph}/UK1", body, TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Theory]
    [InlineData("GET", "")]
    [InlineData("GET", "/22/001/0001")]
    [InlineData("GET", "/22/001/0001/UK1")]
    [InlineData("PUT", "/22/001/0001/UK1")]
    [InlineData("DELETE", "/22/001/0001/UK1")]
    public async Task AllRoutes_RequireAuthentication(string method, string suffix)
    {
        await using var app = await StartAppAsync();
        using var request = new HttpRequestMessage(new HttpMethod(method), Root + suffix);

        var response = await app.GetTestClient().SendAsync(request, TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        store.VerifyNoOtherCalls();
    }

    private async Task<WebApplication> StartAppAsync()
    {
        var builder = WebApplication.CreateEmptyBuilder(new WebApplicationOptions());
        builder.WebHost.UseTestServer();
        builder.Services.AddRouting();
        builder.Services.AddTestServiceToServiceAuthentication();
        builder.Services.AddApiVersioning(options => options.ApiVersionReader = new UrlSegmentApiVersionReader());
        builder.Services.AddLogging();
        builder.Services.AddProblemDetails();
        builder.Services.AddExceptionHandler<ApiExceptionHandler>();
        builder.Services.AddSingleton(store.Object);
        var app = builder.Build();
        app.UseExceptionHandler();
        app.UseTestServiceToServiceAuthentication();
        app.MapAnimalEnrichmentEndpoints();
        await app.StartAsync(TestContext.Current.CancellationToken);
        return app;
    }
}
