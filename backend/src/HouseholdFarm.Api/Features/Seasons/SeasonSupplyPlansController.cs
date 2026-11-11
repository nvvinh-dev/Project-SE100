using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HouseholdFarm.Api.Features.Seasons;

[ApiController]
[Route("api/season-supply-plans")]
[Authorize]
public sealed class SeasonSupplyPlansController(ISeasonSupplyPlanService planService) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<SeasonSupplyPlanResponse>>> GetListAsync(
        [FromQuery] Guid seasonId,
        CancellationToken ct) =>
        Ok(await planService.GetListAsync(seasonId, ct));

    [HttpPost]
    public async Task<ActionResult<SeasonSupplyPlanResponse>> CreateAsync(
        CreateSeasonSupplyPlanRequest request,
        CancellationToken ct)
    {
        var result = await planService.CreateAsync(request, ct);
        return CreatedAtAction(nameof(GetListAsync), new { seasonId = result.SeasonId }, result);
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<SeasonSupplyPlanResponse>> UpdateAsync(
        Guid id,
        UpdateSeasonSupplyPlanRequest request,
        CancellationToken ct) =>
        Ok(await planService.UpdateAsync(id, request, ct));

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> DeleteAsync(
        Guid id,
        CancellationToken ct)
    {
        await planService.DeleteAsync(id, ct);
        return NoContent();
    }
}
