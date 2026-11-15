using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HouseholdFarm.Api.Features.Harvests;

// Tồn nông sản theo mùa vụ và loại chất lượng (FR-HAR-06).
[ApiController]
[Route("api/produce-stock")]
[Authorize]
public sealed class ProduceStockController(IProduceStockService produceStockService) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<ProduceStockOverviewItem>>> GetOverviewAsync(
        [FromQuery] ProduceStockOverviewQuery query,
        CancellationToken ct) =>
        Ok(await produceStockService.GetOverviewAsync(query, ct));
}
