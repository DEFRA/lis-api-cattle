using Lis.Cattle.Interfaces;
using Lis.Cattle.Models;
using Microsoft.AspNetCore.Mvc;

namespace Lis.Cattle.Controllers;

[ApiController]
[Route("holdings")]
public class HoldingsController : ControllerBase
{
    private readonly ICattleService _cattleService;

    public HoldingsController(ICattleService cattleService)
    {
        _cattleService = cattleService;
    }

    [HttpGet("{cph}/cattle")]
    [ProducesResponseType(typeof(IEnumerable<CattleResponse>), 200)]
    public async Task<IActionResult> GetCattleForHolding(string cph)
    {
        var cattle = await _cattleService.GetCattleForHoldingAsync(cph);
        return Ok(cattle);
    }
}
