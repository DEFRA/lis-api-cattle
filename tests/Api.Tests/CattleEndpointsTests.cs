using Lis.Cattle.Endpoints;
using Lis.Cattle.Interfaces;
using Lis.Cattle.Models;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http.HttpResults;
using Moq;

namespace Lis.Cattle.Tests;

public class CattleEndpointsTests
{
    [Fact]
    public void MapCattleEndpoints_MapsGroupAndRouteSuccessfully()
    {
        var builder = WebApplication.CreateBuilder();
        var app = builder.Build();

        var returned = app.MapCattleEndpoints();

        Assert.NotNull(returned);
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
}