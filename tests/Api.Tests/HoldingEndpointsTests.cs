// <copyright file="HoldingEndpointsTests.cs" company="Defra">
// Copyright (c) Defra. All rights reserved.
// </copyright>

namespace Defra.Lis.Api.Tests;

using System.Net;
using System.Net.Http.Json;
using Asp.Versioning;
using Defra.Lis.Api.Endpoints.Cattle;
using Defra.Lis.Api.Endpoints.Holding;
using Defra.Lis.Api.Interfaces;
using Defra.Lis.Api.Models;
using Defra.Lis.Api.Models.Requests;
using Defra.Lis.Api.Models.Responses;
using Defra.Lis.Api.Tests.Authentication;
using Defra.Lis.Api.Validation;
using Defra.Lis.Entities;
using FluentValidation;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Moq;

public class HoldingEndpointsTests
{
    [Theory]
    [InlineData("/holdings/12-345-6789/cattle")]
    [InlineData("/v2/holdings/12-345-6789/cattle")]
    public async Task GetCattleForHolding_WithoutSupportedVersion_Returns404(string requestUri)
    {
        var builder = WebApplication.CreateEmptyBuilder(new WebApplicationOptions());
        builder.WebHost.UseTestServer();
        builder.Services.AddRouting();
        builder.Services.AddTestServiceToServiceAuthentication();
        builder.Services.AddApiVersioning(options => options.ApiVersionReader = new UrlSegmentApiVersionReader());
        builder.Services.AddSingleton(Mock.Of<ICattleService>());
        var app = builder.Build();
        app.UseTestServiceToServiceAuthentication();
        app.MapHoldingEndpoints();
        await app.StartAsync(TestContext.Current.CancellationToken);

        var response = await app.GetAuthenticatedTestClient().GetAsync(requestUri, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public void MapHoldingEndpoints_MapsGroupAndRouteSuccessfully()
    {
        var builder = WebApplication.CreateEmptyBuilder(new WebApplicationOptions());
        builder.WebHost.UseTestServer();
        builder.Services.AddRouting();
        builder.Services.AddTestServiceToServiceAuthentication();
        builder.Services.AddApiVersioning(options => options.ApiVersionReader = new UrlSegmentApiVersionReader());
        var app = builder.Build();
        app.UseTestServiceToServiceAuthentication();

        var returned = app.MapHoldingEndpoints();

        Assert.NotNull(returned);
    }

    [Fact]
    public async Task GetCattleForHolding_WithSlashSeparatedCph_MatchesRouteAndReturns200()
    {
        var mockService = new Mock<ICattleService>();
        var cph = "12/345/6789";
        var expected = new List<CattleResponse>
        {
            new() { EarTag = "UK123456700001", Status = Statuses.Submitted },
        };

        mockService.Setup(s => s.GetCattleForHoldingAsync(cph, It.IsAny<CattleFilter>(), It.IsAny<CancellationToken>()))
                   .ReturnsAsync(expected);

        var builder = WebApplication.CreateEmptyBuilder(new WebApplicationOptions());
        builder.WebHost.UseTestServer();
        builder.Services.AddRouting();
        builder.Services.AddTestServiceToServiceAuthentication();
        builder.Services.AddApiVersioning(options => options.ApiVersionReader = new UrlSegmentApiVersionReader());
        builder.Services.AddSingleton(mockService.Object);
        var app = builder.Build();
        app.UseTestServiceToServiceAuthentication();
        app.MapHoldingEndpoints();
        await app.StartAsync(TestContext.Current.CancellationToken);

        var client = app.GetAuthenticatedTestClient();
        var response = await client.GetAsync($"/v1/holdings/{cph}/cattle", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<List<CattleResponse>>(TestContext.Current.CancellationToken);
        Assert.NotNull(result);
        Assert.Single(result);
        Assert.Equal("UK123456700001", result[0].EarTag);
    }

    [Fact]
    public async Task GetCattleForHolding_WithSingleSegmentCph_MatchesRouteAndReturns200()
    {
        var mockService = new Mock<ICattleService>();
        var cph = "12-345-6789";
        var expected = new List<CattleResponse>
        {
            new() { EarTag = "UK123456700001", Status = Statuses.Submitted },
        };

        mockService.Setup(s => s.GetCattleForHoldingAsync(cph, It.IsAny<CattleFilter>(), It.IsAny<CancellationToken>()))
                   .ReturnsAsync(expected);

        var builder = WebApplication.CreateEmptyBuilder(new WebApplicationOptions());
        builder.WebHost.UseTestServer();
        builder.Services.AddRouting();
        builder.Services.AddTestServiceToServiceAuthentication();
        builder.Services.AddApiVersioning(options => options.ApiVersionReader = new UrlSegmentApiVersionReader());
        builder.Services.AddSingleton(mockService.Object);
        var app = builder.Build();
        app.UseTestServiceToServiceAuthentication();
        app.MapHoldingEndpoints();
        await app.StartAsync(TestContext.Current.CancellationToken);

        var client = app.GetAuthenticatedTestClient();
        var response = await client.GetAsync($"/v1/holdings/{cph}/cattle", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<List<CattleResponse>>(TestContext.Current.CancellationToken);
        Assert.NotNull(result);
        Assert.Single(result);
        Assert.Equal("UK123456700001", result[0].EarTag);
    }

    [Fact]
    public async Task GetCattleForHolding_BindsSearchFiltersFromQueryString()
    {
        var mockService = new Mock<ICattleService>();
        var cph = "22/001/0001";
        CattleFilter? capturedFilter = null;

        mockService.Setup(s => s.GetCattleForHoldingAsync(cph, It.IsAny<CattleFilter>(), It.IsAny<CancellationToken>()))
                   .Callback<string, CattleFilter?, CancellationToken>((_, filter, _) => capturedFilter = filter)
                   .ReturnsAsync([]);

        var builder = WebApplication.CreateEmptyBuilder(new WebApplicationOptions());
        builder.WebHost.UseTestServer();
        builder.Services.AddRouting();
        builder.Services.AddTestServiceToServiceAuthentication();
        builder.Services.AddApiVersioning(options => options.ApiVersionReader = new UrlSegmentApiVersionReader());
        builder.Services.AddSingleton(mockService.Object);
        var app = builder.Build();
        app.UseTestServiceToServiceAuthentication();
        app.MapHoldingEndpoints();
        await app.StartAsync(TestContext.Current.CancellationToken);

        var client = app.GetAuthenticatedTestClient();
        var response = await client.GetAsync($"/v1/holdings/{cph}/cattle?earTag=UK2000&breed=AA&sex=female", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(capturedFilter);
        Assert.Equal("UK2000", capturedFilter.EarTag);
        Assert.Equal("AA", capturedFilter.Breed);
        Assert.Equal("female", capturedFilter.Sex);
    }

    [Fact]
    public async Task GetHolding_MatchesRouteAndReturnsHoldingDetails()
    {
        var mockKrds = new Mock<IKrdsService>();
        var expected = new HoldingDetailedResponse
        {
            Cph = "22/001/0001",
            Name = "Oakfield Farm",
            HoldingType = "AH",
            Address = ["Oakfield Farm", "Church Lane", "Shrewsbury", "SY4 1AB"],
            KeeperName = "Oakfield Farmer",
            HerdMarks =
            [
                new HoldingMarkResponse
                {
                    Mark = "UK 324537",
                    StartDate = DateTimeOffset.Parse("2010-04-01T00:00:00Z"),
                    Species = ["Cattle"],
                },
            ],
            AllowedSpecies = ["Cattle"],
            Associations =
            [
                new HoldingAssociationResponse
                {
                    Name = "Oakfield Farmer",
                    Roles = [new HoldingRoleResponse { Code = "Keeper", Species = ["Cattle"] }],
                },
            ],
        };

        mockKrds.Setup(s => s.GetHoldingAsync("22", "001", "0001", It.IsAny<CancellationToken>()))
                .ReturnsAsync(expected);

        var builder = WebApplication.CreateEmptyBuilder(new WebApplicationOptions());
        builder.WebHost.UseTestServer();
        builder.Services.AddRouting();
        builder.Services.AddTestServiceToServiceAuthentication();
        builder.Services.AddApiVersioning(options => options.ApiVersionReader = new UrlSegmentApiVersionReader());
        builder.Services.AddSingleton(mockKrds.Object);
        var app = builder.Build();
        app.UseTestServiceToServiceAuthentication();
        app.MapHoldingEndpoints();
        await app.StartAsync(TestContext.Current.CancellationToken);

        var client = app.GetAuthenticatedTestClient();
        var response = await client.GetAsync("/v1/holdings/22/001/0001", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<HoldingDetailedResponse>(TestContext.Current.CancellationToken);
        Assert.NotNull(result);
        Assert.Equal("22/001/0001", result.Cph);
        Assert.Equal("Oakfield Farm", result.Name);
        Assert.Equal("Oakfield Farmer", result.KeeperName);
        Assert.Single(result.HerdMarks);
        Assert.Equal("UK 324537", result.HerdMarks[0].Mark);
        Assert.Single(result.Associations);
        Assert.Equal("Oakfield Farmer", result.Associations[0].Name);
        Assert.Single(result.Associations[0].Roles);
        Assert.Equal("Keeper", result.Associations[0].Roles[0].Code);
    }

    [Fact]
    public async Task SearchHoldings_WithValidParameters_Returns200AndPaginatedHoldings()
    {
        var mockKrds = new Mock<IKrdsService>();
        var expected = new PaginatedResponse<HoldingResponse>
        {
            Count = 1,
            TotalCount = 1,
            Page = 1,
            PageSize = 10,
            TotalPages = 1,
            HasNextPage = false,
            HasPreviousPage = false,
            Items =
            [
                new HoldingResponse
                {
                    Cph = "22/001/0001",
                    Name = "Oakfield Farm",
                    HoldingType = "AH",
                    KeeperName = "Oakfield Farmer",
                }
            ],
        };

        mockKrds.Setup(s => s.SearchHoldingsAsync(It.Is<HoldingSearchRequest>(r => r.Search == "Oakfield" && r.Page == 1 && r.PageSize == 10 && r.Sort == "asc" && r.Order == "name"), It.IsAny<CancellationToken>()))
                .ReturnsAsync(expected);

        var builder = WebApplication.CreateEmptyBuilder(new WebApplicationOptions());
        builder.WebHost.UseTestServer();
        builder.Services.AddRouting();
        builder.Services.AddTestServiceToServiceAuthentication();
        builder.Services.AddApiVersioning(options => options.ApiVersionReader = new UrlSegmentApiVersionReader());
        builder.Services.AddSingleton<IValidator<HoldingSearchRequest>>(new HoldingSearchRequestValidator());
        builder.Services.AddSingleton(mockKrds.Object);
        var app = builder.Build();
        app.UseTestServiceToServiceAuthentication();
        app.MapHoldingEndpoints();
        await app.StartAsync(TestContext.Current.CancellationToken);

        var client = app.GetAuthenticatedTestClient();
        var response = await client.GetAsync("/v1/holdings?search=Oakfield&page=1&pageSize=10&sort=asc&order=name", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<PaginatedResponse<HoldingResponse>>(TestContext.Current.CancellationToken);
        Assert.NotNull(result);
        Assert.Equal(1, result.TotalCount);
        Assert.Single(result.Items);
        Assert.Equal("22/001/0001", result.Items[0].Cph);
    }

    [Theory]
    [InlineData("page=0", "Page")]
    [InlineData("pageSize=0", "PageSize")]
    [InlineData("pageSize=101", "PageSize")]
    [InlineData("sort=invalid", "Sort")]
    [InlineData("order=invalid", "Order")]
    public async Task SearchHoldings_WithInvalidParameters_ReturnsValidationProblem400(string queryString, string expectedErrorField)
    {
        var mockKrds = new Mock<IKrdsService>();

        var builder = WebApplication.CreateEmptyBuilder(new WebApplicationOptions());
        builder.WebHost.UseTestServer();
        builder.Services.AddRouting();
        builder.Services.AddTestServiceToServiceAuthentication();
        builder.Services.AddApiVersioning(options => options.ApiVersionReader = new UrlSegmentApiVersionReader());
        builder.Services.AddSingleton<IValidator<HoldingSearchRequest>>(new HoldingSearchRequestValidator());
        builder.Services.AddSingleton(mockKrds.Object);
        var app = builder.Build();
        app.UseTestServiceToServiceAuthentication();
        app.MapHoldingEndpoints();
        await app.StartAsync(TestContext.Current.CancellationToken);

        var client = app.GetAuthenticatedTestClient();
        var response = await client.GetAsync($"/v1/holdings?{queryString}", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        Assert.Contains(expectedErrorField, body, StringComparison.OrdinalIgnoreCase);
        mockKrds.Verify(s => s.SearchHoldingsAsync(It.IsAny<HoldingSearchRequest>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GetHolding_ReturnsProblemDetails404_WhenHoldingIsUnknown()
    {
        var mockKrds = new Mock<IKrdsService>();
        mockKrds.Setup(s => s.GetHoldingAsync("22", "050", "0050", It.IsAny<CancellationToken>()))
                .ThrowsAsync(new Defra.Lis.Core.Exceptions.NotFoundException("Holding '22/050/0050' was not found."));

        var builder = WebApplication.CreateEmptyBuilder(new WebApplicationOptions());
        builder.WebHost.UseTestServer();
        builder.Services.AddRouting();
        builder.Services.AddTestServiceToServiceAuthentication();
        builder.Services.AddApiVersioning(options => options.ApiVersionReader = new UrlSegmentApiVersionReader());
        builder.Services.AddLogging();
        builder.Services.AddProblemDetails();
        builder.Services.AddExceptionHandler<Defra.Lis.Api.Exceptions.ApiExceptionHandler>();
        builder.Services.AddSingleton(mockKrds.Object);
        var app = builder.Build();
        app.UseExceptionHandler();
        app.UseTestServiceToServiceAuthentication();
        app.MapHoldingEndpoints();
        await app.StartAsync(TestContext.Current.CancellationToken);

        var client = app.GetAuthenticatedTestClient();
        var response = await client.GetAsync("/v1/holdings/22/050/0050", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        Assert.Contains("22/050/0050", body);
    }

    [Fact]
    public async Task GetCattleDetails_MatchesRouteAndReturnsAnimalDetails()
    {
        var mockService = new Mock<ICattleService>();
        var expected = new CattleDetailsResponse
        {
            EarTag = "UK200000000001",
            Species = "Cattle",
            Sex = "Male",
            DateBirth = new DateOnly(2023, 2, 1),
            DateRegistered = new DateOnly(2023, 2, 5),
            DateOnCph = new DateOnly(2023, 2, 1),
            Breed = "Aberdeen Angus",
            BreedCode = "AA",
            BreedName = "Aberdeen Angus",
            State = "Alive",
            RestrictionStatus = "None",
            DamType = "genetic",
            GeneticDamEarTag = "UK200000000098",
            SireEarTag = "UK200000000099",
        };

        mockService.Setup(s => s.GetCattleDetailsAsync("UK200000000001", It.IsAny<CancellationToken>()))
                   .ReturnsAsync(expected);

        var builder = WebApplication.CreateEmptyBuilder(new WebApplicationOptions());
        builder.WebHost.UseTestServer();
        builder.Services.AddRouting();
        builder.Services.AddTestServiceToServiceAuthentication();
        builder.Services.AddApiVersioning(options => options.ApiVersionReader = new UrlSegmentApiVersionReader());
        builder.Services.AddSingleton(mockService.Object);
        var app = builder.Build();
        app.UseTestServiceToServiceAuthentication();
        app.MapCattleEndpoints();
        await app.StartAsync(TestContext.Current.CancellationToken);

        var client = app.GetAuthenticatedTestClient();
        var response = await client.GetAsync("/v1/cattle/UK200000000001", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<CattleDetailsResponse>(TestContext.Current.CancellationToken);
        Assert.NotNull(result);
        Assert.Equal("UK200000000001", result.EarTag);
        Assert.Equal("AA", result.BreedCode);
        Assert.Equal("Alive", result.State);
        Assert.Equal("genetic", result.DamType);
        Assert.Equal("UK200000000098", result.GeneticDamEarTag);
        Assert.Equal("UK200000000099", result.SireEarTag);
    }

    [Fact]
    public async Task GetCattleDetails_DecodesTheEarTagBeforeCallingTheService()
    {
        var mockService = new Mock<ICattleService>();
        mockService.Setup(s => s.GetCattleDetailsAsync("UK2 0000 00001", It.IsAny<CancellationToken>()))
                   .ReturnsAsync(new CattleDetailsResponse { EarTag = "UK2 0000 00001" });

        var builder = WebApplication.CreateEmptyBuilder(new WebApplicationOptions());
        builder.WebHost.UseTestServer();
        builder.Services.AddRouting();
        builder.Services.AddTestServiceToServiceAuthentication();
        builder.Services.AddApiVersioning(options => options.ApiVersionReader = new UrlSegmentApiVersionReader());
        builder.Services.AddSingleton(mockService.Object);
        var app = builder.Build();
        app.UseTestServiceToServiceAuthentication();
        app.MapCattleEndpoints();
        await app.StartAsync(TestContext.Current.CancellationToken);

        var client = app.GetAuthenticatedTestClient();
        var response = await client.GetAsync("/v1/cattle/UK2%200000%2000001", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        mockService.Verify(s => s.GetCattleDetailsAsync("UK2 0000 00001", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetCattleDetails_ReturnsProblemDetails404_WhenTheAnimalIsUnknown()
    {
        var mockService = new Mock<ICattleService>();
        mockService.Setup(s => s.GetCattleDetailsAsync("UK999999999999", It.IsAny<CancellationToken>()))
                   .ThrowsAsync(new Defra.Lis.Core.Exceptions.NotFoundException("Animal 'UK999999999999' was not found."));

        var builder = WebApplication.CreateEmptyBuilder(new WebApplicationOptions());
        builder.WebHost.UseTestServer();
        builder.Services.AddRouting();
        builder.Services.AddTestServiceToServiceAuthentication();
        builder.Services.AddApiVersioning(options => options.ApiVersionReader = new UrlSegmentApiVersionReader());
        builder.Services.AddLogging();
        builder.Services.AddProblemDetails();
        builder.Services.AddExceptionHandler<Defra.Lis.Api.Exceptions.ApiExceptionHandler>();
        builder.Services.AddSingleton(mockService.Object);
        var app = builder.Build();
        app.UseExceptionHandler();
        app.UseTestServiceToServiceAuthentication();
        app.MapCattleEndpoints();
        await app.StartAsync(TestContext.Current.CancellationToken);

        var client = app.GetAuthenticatedTestClient();
        var response = await client.GetAsync("/v1/cattle/UK999999999999", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        Assert.Contains("UK999999999999", body);
    }

    [Fact]
    public async Task GetBundlesForHolding_WithSingleSegmentCph_MatchesRouteAndReturns200()
    {
        var mockService = new Mock<ICattleService>();
        var cph = "12-345-6789";
        var expected = new List<BundleResponse>
        {
            new()
            {
                Id = Guid.NewGuid(),
                ClientReference = "ref1",
                CountyParishHolding = cph,
                SubmittedBy = "user1",
                Status = Statuses.Pending,
                CreatedAt = DateTimeOffset.UtcNow,
                Animals = [],
            },
        };

        mockService.Setup(s => s.GetBundlesForHoldingAsync(cph))
                   .ReturnsAsync(expected);

        var builder = WebApplication.CreateEmptyBuilder(new WebApplicationOptions());
        builder.WebHost.UseTestServer();
        builder.Services.AddRouting();
        builder.Services.AddTestServiceToServiceAuthentication();
        builder.Services.AddApiVersioning(options => options.ApiVersionReader = new UrlSegmentApiVersionReader());
        builder.Services.AddSingleton(mockService.Object);
        var app = builder.Build();
        app.UseTestServiceToServiceAuthentication();
        app.MapHoldingEndpoints();
        await app.StartAsync(TestContext.Current.CancellationToken);

        var client = app.GetAuthenticatedTestClient();
        var response = await client.GetAsync($"/v1/holdings/{cph}/bundles", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<List<BundleResponse>>(TestContext.Current.CancellationToken);
        Assert.NotNull(result);
        Assert.Single(result);
        Assert.Equal("ref1", result[0].ClientReference);
    }

    [Fact]
    public async Task GetBundlesForHolding_WithSlashSeparatedCph_MatchesRouteAndReturns200()
    {
        var mockService = new Mock<ICattleService>();
        var cph = "12/345/6789";
        var expected = new List<BundleResponse>
        {
            new()
            {
                Id = Guid.NewGuid(),
                ClientReference = "ref1",
                CountyParishHolding = cph,
                SubmittedBy = "user1",
                Status = Statuses.Pending,
                CreatedAt = DateTimeOffset.UtcNow,
                Animals = [],
            },
        };

        mockService.Setup(s => s.GetBundlesForHoldingAsync(cph))
                   .ReturnsAsync(expected);

        var builder = WebApplication.CreateEmptyBuilder(new WebApplicationOptions());
        builder.WebHost.UseTestServer();
        builder.Services.AddRouting();
        builder.Services.AddTestServiceToServiceAuthentication();
        builder.Services.AddApiVersioning(options => options.ApiVersionReader = new UrlSegmentApiVersionReader());
        builder.Services.AddSingleton(mockService.Object);
        var app = builder.Build();
        app.UseTestServiceToServiceAuthentication();
        app.MapHoldingEndpoints();
        await app.StartAsync(TestContext.Current.CancellationToken);

        var client = app.GetAuthenticatedTestClient();
        var response = await client.GetAsync($"/v1/holdings/{cph}/bundles", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<List<BundleResponse>>(TestContext.Current.CancellationToken);
        Assert.NotNull(result);
        Assert.Single(result);
        Assert.Equal("ref1", result[0].ClientReference);
    }

    [Fact]
    public async Task GetCattleForHolding_CallsServiceAndReturnsResults()
    {
        var mockService = new Mock<ICattleService>();
        var cph = "12/345/6789";
        var expected = new List<CattleResponse>
        {
            new() { EarTag = "UK123456700001", Status = Statuses.Submitted },
        };

        mockService.Setup(s => s.GetCattleForHoldingAsync(cph, It.IsAny<CattleFilter>(), It.IsAny<CancellationToken>()))
                   .ReturnsAsync(expected);

        var result = await mockService.Object.GetCattleForHoldingAsync(cph, null, CancellationToken.None);

        Assert.NotNull(result);
        Assert.Single(result);
        Assert.Equal("UK123456700001", result.First().EarTag);
    }

    [Fact]
    public async Task GetBundlesForHolding_CallsServiceAndReturnsResults()
    {
        var mockService = new Mock<ICattleService>();
        var cph = "12/345/6789";
        var expected = new List<BundleResponse>
        {
            new()
            {
                Id = Guid.NewGuid(),
                ClientReference = "ref1",
                CountyParishHolding = cph,
                SubmittedBy = "user1",
                Status = Statuses.Pending,
                CreatedAt = DateTimeOffset.UtcNow,
                Animals =
                [
                    new()
                    {
                        Id = Guid.NewGuid(),
                        EarTag = "UK123456700001",
                        Status = Statuses.Pending,
                        Errors =
                        [
                            new()
                            {
                                ErrorCode = "ERR01",
                                ErrorText = "Test Error",
                            }
                        ],
                    }
                ],
            },
        };

        mockService.Setup(s => s.GetBundlesForHoldingAsync(cph))
                   .ReturnsAsync(expected);

        var result = await mockService.Object.GetBundlesForHoldingAsync(cph);

        Assert.NotNull(result);
        Assert.Single(result);
        var bundle = result.First();
        Assert.Equal("ref1", bundle.ClientReference);
        Assert.Single(bundle.Animals);
        Assert.Single(bundle.Animals[0].Errors);
        Assert.Equal("ERR01", bundle.Animals[0].Errors[0].ErrorCode);
    }
}
