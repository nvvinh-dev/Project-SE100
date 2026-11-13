using HouseholdFarm.Api.Common.Responses;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HouseholdFarm.Api.Features.Seasons;

[ApiController]
[Route("api/seasons")]
[Authorize]
public sealed class SeasonsController(ISeasonService seasonService) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<PagedResult<SeasonListItem>>> GetListAsync([FromQuery] SeasonQuery query, CancellationToken ct) =>
        Ok(await seasonService.GetListAsync(query, ct));

    [HttpGet("options")]
    public async Task<ActionResult<IReadOnlyList<SeasonOptionItem>>> GetOptionsAsync([FromQuery] SeasonOptionQuery query, CancellationToken ct) =>
        Ok(await seasonService.GetOptionsAsync(query, ct));

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<SeasonDetailResponse>> GetAsync(Guid id, CancellationToken ct) =>
        Ok(await seasonService.GetDetailAsync(id, ct));

    [HttpPost]
    public async Task<ActionResult<SeasonResponse>> CreateAsync(CreateSeasonRequest request, CancellationToken ct)
    {
        var season = await seasonService.CreateAsync(request, ct);
        return CreatedAtAction(nameof(GetAsync), new { id = season.Id }, season);
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<SeasonResponse>> UpdateAsync(Guid id, UpdateSeasonRequest request, CancellationToken ct) =>
        Ok(await seasonService.UpdateAsync(id, request, ct));

    [HttpPost("{id:guid}/start")]
    public async Task<ActionResult<SeasonResponse>> StartAsync(Guid id, CancellationToken ct) =>
        Ok(await seasonService.StartAsync(id, ct));
}

