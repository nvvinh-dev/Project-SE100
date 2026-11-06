using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HouseholdFarm.Api.Features.Seasons;

[ApiController]
[Route("api/seasons")]
[Authorize]
public sealed class SeasonsController(ISeasonService seasonService) : ControllerBase
{
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<SeasonResponse>> GetAsync(Guid id, CancellationToken ct) =>
        Ok(await seasonService.GetAsync(id, ct));

    [HttpPost]
    public async Task<ActionResult<SeasonResponse>> CreateAsync(CreateSeasonRequest request, CancellationToken ct)
    {
        var season = await seasonService.CreateAsync(request, ct);
        return CreatedAtAction(nameof(GetAsync), new { id = season.Id }, season);
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<SeasonResponse>> UpdateAsync(Guid id, UpdateSeasonRequest request, CancellationToken ct) =>
        Ok(await seasonService.UpdateAsync(id, request, ct));
}

