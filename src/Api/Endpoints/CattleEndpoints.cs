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

        group.MapGet("/{cph1}/{cph2}/{cph3}/cattle", (string cph1, string cph2, string cph3, [FromServices] ICattleService cattleService) =>
                 GetCattleForHolding($"{cph1}/{cph2}/{cph3}", cattleService))
             .WithName("GetCattleForHoldingMultiSegment")
             .Produces<IEnumerable<CattleResponse>>(StatusCodes.Status200OK);

        group.MapGet("/{cph}/cattle", (string cph, [FromServices] ICattleService cattleService) =>
                 GetCattleForHolding(cph, cattleService))
             .WithName("GetCattleForHolding")
             .Produces<IEnumerable<CattleResponse>>(StatusCodes.Status200OK);

        group.MapGet("/{cph1}/{cph2}/{cph3}/bundles", (string cph1, string cph2, string cph3, [FromServices] ICattleService cattleService) =>
                 GetBundlesForHolding($"{cph1}/{cph2}/{cph3}", cattleService))
             .WithName("GetBundlesForHoldingMultiSegment")
             .Produces<IEnumerable<BundleResponse>>(StatusCodes.Status200OK);

        group.MapGet("/{cph}/bundles", (string cph, [FromServices] ICattleService cattleService) =>
                 GetBundlesForHolding(cph, cattleService))
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