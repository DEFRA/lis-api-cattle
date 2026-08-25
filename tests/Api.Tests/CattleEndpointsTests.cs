using System.Net;
using System.Net.Http.Json;
using Lis.Cattle.Endpoints;
using Lis.Cattle.Interfaces;
using Lis.Cattle.Models;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Moq;

namespace Lis.Cattle;

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
            new() { EarTag = "UK123456700001", Status = "submitted" }
        };

        mockService.Setup(s => s.GetCattleForHoldingAsync(cph))
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
            new() { EarTag = "UK123456700001", Status = "submitted" }
        };

        mockService.Setup(s => s.GetCattleForHoldingAsync(cph))
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
                Status = "pending",
                CreatedAt = DateTimeOffset.UtcNow,
                Animals = []
            }
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
                Status = "pending",
                CreatedAt = DateTimeOffset.UtcNow,
                Animals = []
            }
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
            new() { EarTag = "UK123456700001", Status = "submitted" }
        };

        mockService.Setup(s => s.GetCattleForHoldingAsync(cph))
                   .ReturnsAsync(expected);

        var result = await mockService.Object.GetCattleForHoldingAsync(cph);

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
                Status = "pending",
                CreatedAt = DateTimeOffset.UtcNow,
                Animals =
                [
                    new()
                    {
                        Id = Guid.NewGuid(),
                        EarTag = "UK123456700001",
                        Status = "pending",
                        Errors =
                        [
                            new()
                            {
                                ErrorCode = "ERR01",
                                ErrorText = "Test Error"
                            }
                        ]
                    }
                ]
            }
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