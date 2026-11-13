using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HouseholdFarm.Api.Features.Seasons;

[ApiController]
[Route("api/season-assignments")]
[Authorize]
public sealed class SeasonAssignmentsController(ISeasonAssignmentService assignmentService) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<SeasonAssignmentResponse>>> GetListAsync(
        [FromQuery] Guid seasonId,
        CancellationToken ct) =>
        Ok(await assignmentService.GetListAsync(seasonId, ct));

    [HttpPost]
    public async Task<ActionResult<SeasonAssignmentResponse>> AssignAsync(
        CreateSeasonAssignmentRequest request,
        CancellationToken ct)
    {
        var result = await assignmentService.AssignAsync(request, ct);
        return CreatedAtAction(nameof(GetListAsync), new { seasonId = result.SeasonId }, result);
    }

    [HttpPost("{id:guid}/unassign")]
    public async Task<IActionResult> UnassignAsync(
        Guid id,
        UnassignSeasonAssignmentRequest request,
        CancellationToken ct)
    {
        await assignmentService.UnassignAsync(id, request, ct);
        return Ok(new { success = true });
    }
}
