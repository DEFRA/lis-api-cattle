// <copyright file="CattleEndpointsTests.cs" company="Defra">
// Copyright (c) Defra. All rights reserved.
// </copyright>

namespace Defra.Lis.Api.Tests;

using System.Net;
using System.Net.Http.Json;
using Defra.Lis.Api.Endpoints;
using Defra.Lis.Api.Interfaces;
using Defra.Lis.Api.Models;
using Defra.Lis.Entities;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Moq;

public class CattleEndpointsTests
{
    [Fact]
    public void MapCattleEndpoints_MapsGroupAndRouteSuccessfully()
    {
        var builder = WebApplication.CreateEmptyBuilder(new WebApplicationOptions());
        builder.WebHost.UseTestServer();
        builder.Services.AddRouting();
        var app = builder.Build();

        var returned = app.MapCattleEndpoints();

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
        builder.Services.AddSingleton(mockService.Object);
        var app = builder.Build();
        app.MapCattleEndpoints();
        await app.StartAsync(TestContext.Current.CancellationToken);

        var client = app.GetTestClient();
        var response = await client.GetAsync($"/holdings/{cph}/cattle", TestContext.Current.CancellationToken);

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
        builder.Services.AddSingleton(mockService.Object);
        var app = builder.Build();
        app.MapCattleEndpoints();
        await app.StartAsync(TestContext.Current.CancellationToken);

        var client = app.GetTestClient();
        var response = await client.GetAsync($"/holdings/{cph}/cattle", TestContext.Current.CancellationToken);

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
        builder.Services.AddSingleton(mockService.Object);
        var app = builder.Build();
        app.MapCattleEndpoints();
        await app.StartAsync(TestContext.Current.CancellationToken);

        var client = app.GetTestClient();
        var response = await client.GetAsync($"/holdings/{cph}/cattle?earTag=UK2000&breed=AA&sex=female", TestContext.Current.CancellationToken);

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
        var expected = new HoldingResponse
        {
            Cph = "22/001/0001",
            Name = "Oakfield Farm",
            HoldingType = "AH",
            Address = ["Oakfield Farm", "Church Lane", "Shrewsbury", "SY4 1AB"],
            KeeperName = "Oakfield Farmer",
            HerdMarks = ["UK 324537"],
            AllowedSpecies = ["Cattle"],
        };

        mockKrds.Setup(s => s.GetHoldingAsync("22", "001", "0001", It.IsAny<CancellationToken>()))
                .ReturnsAsync(expected);

        var builder = WebApplication.CreateEmptyBuilder(new WebApplicationOptions());
        builder.WebHost.UseTestServer();
        builder.Services.AddRouting();
        builder.Services.AddSingleton(mockKrds.Object);
        var app = builder.Build();
        app.MapCattleEndpoints();
        await app.StartAsync(TestContext.Current.CancellationToken);

        var client = app.GetTestClient();
        var response = await client.GetAsync("/holdings/22/001/0001", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<HoldingResponse>(TestContext.Current.CancellationToken);
        Assert.NotNull(result);
        Assert.Equal("22/001/0001", result.Cph);
        Assert.Equal("Oakfield Farm", result.Name);
        Assert.Equal("Oakfield Farmer", result.KeeperName);
        Assert.Equal(["UK 324537"], result.HerdMarks);
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
        builder.Services.AddLogging();
        builder.Services.AddProblemDetails();
        builder.Services.AddExceptionHandler<Defra.Lis.Api.Exceptions.ApiExceptionHandler>();
        builder.Services.AddSingleton(mockKrds.Object);
        var app = builder.Build();
        app.UseExceptionHandler();
        app.MapCattleEndpoints();
        await app.StartAsync(TestContext.Current.CancellationToken);

        var client = app.GetTestClient();
        var response = await client.GetAsync("/holdings/22/050/0050", TestContext.Current.CancellationToken);

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
        builder.Services.AddSingleton(mockService.Object);
        var app = builder.Build();
        app.MapCattleEndpoints();
        await app.StartAsync(TestContext.Current.CancellationToken);

        var client = app.GetTestClient();
        var response = await client.GetAsync("/cattle/UK200000000001", TestContext.Current.CancellationToken);

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
        builder.Services.AddSingleton(mockService.Object);
        var app = builder.Build();
        app.MapCattleEndpoints();
        await app.StartAsync(TestContext.Current.CancellationToken);

        var client = app.GetTestClient();
        var response = await client.GetAsync("/cattle/UK2%200000%2000001", TestContext.Current.CancellationToken);

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
        builder.Services.AddLogging();
        builder.Services.AddProblemDetails();
        builder.Services.AddExceptionHandler<Defra.Lis.Api.Exceptions.ApiExceptionHandler>();
        builder.Services.AddSingleton(mockService.Object);
        var app = builder.Build();
        app.UseExceptionHandler();
        app.MapCattleEndpoints();
        await app.StartAsync(TestContext.Current.CancellationToken);

        var client = app.GetTestClient();
        var response = await client.GetAsync("/cattle/UK999999999999", TestContext.Current.CancellationToken);

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
        builder.Services.AddSingleton(mockService.Object);
        var app = builder.Build();
        app.MapCattleEndpoints();
        await app.StartAsync(TestContext.Current.CancellationToken);

        var client = app.GetTestClient();
        var response = await client.GetAsync($"/holdings/{cph}/bundles", TestContext.Current.CancellationToken);

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
        builder.Services.AddSingleton(mockService.Object);
        var app = builder.Build();
        app.MapCattleEndpoints();
        await app.StartAsync(TestContext.Current.CancellationToken);

        var client = app.GetTestClient();
        var response = await client.GetAsync($"/holdings/{cph}/bundles", TestContext.Current.CancellationToken);

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
