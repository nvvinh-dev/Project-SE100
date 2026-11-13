using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HouseholdFarm.Api.Features.Seasons;

[ApiController]
[Route("api/season-notes")]
[Authorize]
public sealed class SeasonNotesController(ISeasonNoteService noteService) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<SeasonNoteResponse>>> GetListAsync(
        [FromQuery] Guid seasonId,
        CancellationToken ct) =>
        Ok(await noteService.GetListAsync(seasonId, ct));

    [HttpPost]
    public async Task<ActionResult<SeasonNoteResponse>> CreateAsync(
        CreateSeasonNoteRequest request,
        CancellationToken ct)
    {
        var result = await noteService.CreateAsync(request, ct);
        return CreatedAtAction(nameof(GetListAsync), new { seasonId = result.SeasonId }, result);
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<SeasonNoteResponse>> UpdateAsync(
        Guid id,
        UpdateSeasonNoteRequest request,
        CancellationToken ct) =>
        Ok(await noteService.UpdateAsync(id, request, ct));
}
