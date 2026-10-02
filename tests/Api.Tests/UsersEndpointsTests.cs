// <copyright file="UsersEndpointsTests.cs" company="Defra">
// Copyright (c) Defra. All rights reserved.
// </copyright>

namespace Defra.Lis.Api.Tests;

using System.Net;
using System.Net.Http.Json;
using Asp.Versioning;
using Defra.Lis.Api.Endpoints.Users;
using Defra.Lis.Api.Exceptions;
using Defra.Lis.Api.Interfaces;
using Defra.Lis.Api.Models.Responses;
using Defra.Lis.Api.Tests.Authentication;
using Defra.Lis.Core.Exceptions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Metadata;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Moq;

public class UsersEndpointsTests
{
    private const string Subject = "0b6f2f0e-3c1a-4e8e-9d4b-2f6a1c9e7d51";

    private readonly Mock<IKrdsService> krdsService = new();

    [Fact]
    public async Task GetUserDetails_MatchesRouteAndReturnsUserDetails()
    {
        krdsService.Setup(s => s.GetUserAccountAsync(Subject, It.IsAny<CancellationToken>()))
                   .ReturnsAsync(new UserDetailsResponse
                   {
                       Subject = Subject,
                       Email = "oakfield.farmer@oakhill-farms.co.uk",
                       DisplayName = "Oakfield Farmer",
                       Cphs = [new UserCphResponse { Cph = "22/001/0001", HoldingId = "holding-0001", HoldingName = "Oakfield Farm", Role = "Keeper" }],
                   });
        await using var app = await StartAppAsync();

        var response = await app.GetAuthenticatedTestClient().GetAsync($"/v1/users/{Subject}", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<UserDetailsResponse>(TestContext.Current.CancellationToken);
        Assert.NotNull(result);
        Assert.Equal(Subject, result.Subject);
        var cph = Assert.Single(result.Cphs);
        Assert.Equal("22/001/0001", cph.Cph);
        Assert.Equal("holding-0001", cph.HoldingId);
        Assert.Equal("Oakfield Farm", cph.HoldingName);
        Assert.Equal("Keeper", cph.Role);
    }

    [Fact]
    public async Task GetUserDetails_DecodesTheSubjectBeforeCallingTheService()
    {
        krdsService.Setup(s => s.GetUserAccountAsync("idp|user 1", It.IsAny<CancellationToken>()))
                   .ReturnsAsync(new UserDetailsResponse { Subject = "idp|user 1" });
        await using var app = await StartAppAsync();

        var response = await app.GetAuthenticatedTestClient().GetAsync("/v1/users/idp%7Cuser%201", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        krdsService.Verify(s => s.GetUserAccountAsync("idp|user 1", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetUserDetails_ReturnsProblemDetails404_WhenTheSubjectIsUnknown()
    {
        krdsService.Setup(s => s.GetUserAccountAsync(Subject, It.IsAny<CancellationToken>()))
                   .ThrowsAsync(new NotFoundException($"User '{Subject}' was not found."));
        await using var app = await StartAppAsync();

        var response = await app.GetAuthenticatedTestClient().GetAsync($"/v1/users/{Subject}", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        Assert.Contains(Subject, body);
    }

    [Fact]
    public async Task GetUserDetails_ReturnsProblemDetails400_WhenTheSubjectIsRejected()
    {
        krdsService.Setup(s => s.GetUserAccountAsync("not-a-subject", It.IsAny<CancellationToken>()))
                   .ThrowsAsync(new ArgumentException("User 'not-a-subject' is not a valid subject."));
        await using var app = await StartAppAsync();

        var response = await app.GetAuthenticatedTestClient().GetAsync("/v1/users/not-a-subject", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task MapUserEndpoints_DescribesTheRouteForOpenApi()
    {
        await using var app = await StartAppAsync();

        var endpoint = app.Services.GetRequiredService<EndpointDataSource>().Endpoints
            .OfType<RouteEndpoint>()
            .Single(e => e.Metadata.GetMetadata<IEndpointNameMetadata>()?.EndpointName == OpenApiMetadata.GetUserDetailsRoute.Name);

        Assert.Equal("/v{version:apiVersion}/users/{subject}", endpoint.RoutePattern.RawText);
        Assert.Equal(OpenApiMetadata.GetUserDetailsRoute.Summary, endpoint.Metadata.GetMetadata<IEndpointSummaryMetadata>()?.Summary);
        Assert.Equal(OpenApiMetadata.GetUserDetailsRoute.Description, endpoint.Metadata.GetMetadata<IEndpointDescriptionMetadata>()?.Description);
        Assert.Contains(OpenApiMetadata.Tag, endpoint.Metadata.GetMetadata<ITagsMetadata>()!.Tags);
        var statusCodes = endpoint.Metadata.GetOrderedMetadata<IProducesResponseTypeMetadata>().Select(m => m.StatusCode).ToList();
        Assert.Equal([StatusCodes.Status401Unauthorized, StatusCodes.Status200OK, StatusCodes.Status400BadRequest, StatusCodes.Status404NotFound, StatusCodes.Status500InternalServerError], statusCodes);
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
        builder.Services.AddSingleton(krdsService.Object);
        var app = builder.Build();
        app.UseExceptionHandler();
        app.UseTestServiceToServiceAuthentication();
        app.MapUserEndpoints();
        await app.StartAsync(TestContext.Current.CancellationToken);
        return app;
    }
}
