using HouseholdFarm.Api.Common.Responses;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HouseholdFarm.Api.Features.FarmTasks;

[ApiController]
[Route("api/farm-tasks")]
[Authorize]
public sealed class FarmTasksController(IFarmTaskService farmTaskService) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<PagedResult<FarmTaskListItem>>> GetListAsync(
        [FromQuery] FarmTaskQuery query,
        CancellationToken ct) =>
        Ok(await farmTaskService.GetListAsync(query, ct));

    [HttpGet("assignees")]
    public async Task<ActionResult<IReadOnlyList<FarmTaskAssigneeOption>>> GetAssigneesAsync(
        [FromQuery] Guid? seasonId,
        CancellationToken ct) =>
        Ok(await farmTaskService.GetAssigneesAsync(seasonId, ct));

    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(FarmTaskResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(WorkerFarmTaskResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<FarmTaskResponse>> GetAsync(Guid id, CancellationToken ct) =>
        Ok(await farmTaskService.GetAsync(id, ct));

    [HttpPost]
    [ProducesResponseType(typeof(FarmTaskResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(IReadOnlyList<FarmTaskResponse>), StatusCodes.Status201Created)]
    public async Task<ActionResult> CreateAsync(
        CreateFarmTaskRequest request,
        CancellationToken ct)
    {
        if (request.RecurrenceIntervalDays.HasValue)
        {
            var createdList = await farmTaskService.CreateRecurringAsync(request, ct);
            return StatusCode(StatusCodes.Status201Created, createdList);
        }

        var created = await farmTaskService.CreateAsync(request, ct);
        return CreatedAtAction(nameof(GetAsync), new { id = created.Id }, created);
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<FarmTaskResponse>> UpdateAsync(
        Guid id,
        UpdateFarmTaskRequest request,
        CancellationToken ct) =>
        Ok(await farmTaskService.UpdateAsync(id, request, ct));

    [HttpPost("{id:guid}/cancel")]
    [ProducesResponseType(typeof(FarmTaskResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(CancelRecurrenceGroupResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult> CancelAsync(
        Guid id,
        CancelFarmTaskRequest request,
        CancellationToken ct) =>
        request.IncludeRecurrenceGroup
            ? Ok(await farmTaskService.CancelRecurrenceGroupAsync(id, ct))
            : Ok(await farmTaskService.CancelAsync(id, request, ct));

    [HttpPost("{id:guid}/record-result")]
    [ProducesResponseType(typeof(FarmTaskResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(WorkerFarmTaskResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<FarmTaskResponse>> RecordResultAsync(
        Guid id,
        RecordFarmTaskResultRequest request,
        CancellationToken ct) =>
        Ok(await farmTaskService.RecordResultAsync(id, request, ct));
}
