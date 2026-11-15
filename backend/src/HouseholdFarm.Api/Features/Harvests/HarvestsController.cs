using HouseholdFarm.Api.Common.Responses;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HouseholdFarm.Api.Features.Harvests;

// Phiếu thu hoạch không có PUT, DELETE: ghi nhầm thì đính chính hoặc hủy, có lý do và lưu lịch sử (FR-HAR-03).
[ApiController]
[Route("api/harvests")]
[Authorize]
public sealed class HarvestsController(IHarvestService harvestService) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<PagedResult<HarvestRecordListItem>>> GetListAsync(
        [FromQuery] HarvestRecordQuery query,
        CancellationToken ct) =>
        Ok(await harvestService.GetListAsync(query, ct));

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<HarvestRecordResponse>> GetAsync(Guid id, CancellationToken ct) =>
        Ok(await harvestService.GetAsync(id, ct));

    [HttpPost]
    public async Task<ActionResult<HarvestRecordResponse>> CreateAsync(
        CreateHarvestRecordRequest request,
        CancellationToken ct)
    {
        var harvest = await harvestService.CreateAsync(request, ct);
        return CreatedAtAction(nameof(GetAsync), new { id = harvest.Id }, harvest);
    }

    [HttpPost("{id:guid}/correct")]
    public async Task<ActionResult<HarvestRecordResponse>> CorrectAsync(
        Guid id,
        CorrectHarvestRecordRequest request,
        CancellationToken ct) =>
        Ok(await harvestService.CorrectAsync(id, request, ct));

    [HttpPost("{id:guid}/void")]
    public async Task<ActionResult<HarvestRecordResponse>> VoidAsync(
        Guid id,
        VoidHarvestRecordRequest request,
        CancellationToken ct) =>
        Ok(await harvestService.VoidAsync(id, request, ct));
}
