using Lis.Cattle.Interfaces;
using Lis.Cattle.Models;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace Lis.Cattle.Endpoints;

public static class CattleEndpoints
{
    public static IEndpointRouteBuilder MapCattleEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/holdings")
                       .WithTags("Cattle");

        group.MapGet("/{cph}/cattle", GetCattleForHolding)
             .WithName("GetCattleForHolding")
             .Produces<IEnumerable<CattleResponse>>(StatusCodes.Status200OK);

        group.MapGet("/{cph}/bundles", GetBundlesForHolding)
             .WithName("GetBundlesForHolding")
             .Produces<IEnumerable<BundleResponse>>(StatusCodes.Status200OK);

        return app;
    }

    private static async Task<IResult> GetCattleForHolding(string cph, [FromServices] ICattleService cattleService)
    {
        var cattle = await cattleService.GetCattleForHoldingAsync(cph);
        return Results.Ok(cattle);
    }

    private static async Task<IResult> GetBundlesForHolding(string cph, [FromServices] ICattleService cattleService)
    {
        var bundles = await cattleService.GetBundlesForHoldingAsync(cph);
        return Results.Ok(bundles);
    }
}