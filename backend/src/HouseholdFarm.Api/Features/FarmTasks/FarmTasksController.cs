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
    public async Task<ActionResult<FarmTaskResponse>> GetAsync(Guid id, CancellationToken ct) =>
        Ok(await farmTaskService.GetAsync(id, ct));

    [HttpPost]
    public async Task<ActionResult<FarmTaskResponse>> CreateAsync(
        CreateFarmTaskRequest request,
        CancellationToken ct)
    {
        var created = await farmTaskService.CreateAsync(request, ct);
        return CreatedAtAction(nameof(GetAsync), new { id = created.Id }, created);
    }

    [HttpPost("recurring")]
    public async Task<ActionResult<IReadOnlyList<FarmTaskResponse>>> CreateRecurringAsync(
        CreateRecurringFarmTasksRequest request,
        CancellationToken ct)
    {
        var created = await farmTaskService.CreateRecurringAsync(request, ct);
        return StatusCode(StatusCodes.Status201Created, created);
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<FarmTaskResponse>> UpdateAsync(
        Guid id,
        UpdateFarmTaskRequest request,
        CancellationToken ct) =>
        Ok(await farmTaskService.UpdateAsync(id, request, ct));

    [HttpPost("{id:guid}/cancel")]
    public async Task<ActionResult<FarmTaskResponse>> CancelAsync(
        Guid id,
        CancelFarmTaskRequest request,
        CancellationToken ct) =>
        Ok(await farmTaskService.CancelAsync(id, request, ct));

    [HttpPost("{id:guid}/record-result")]
    public async Task<ActionResult<FarmTaskResponse>> RecordResultAsync(
        Guid id,
        RecordFarmTaskResultRequest request,
        CancellationToken ct) =>
        Ok(await farmTaskService.RecordResultAsync(id, request, ct));

    [HttpPost("recurrence-groups/{recurrenceGroupId:guid}/cancel")]
    public async Task<ActionResult<CancelRecurrenceGroupResponse>> CancelRecurrenceGroupAsync(
        Guid recurrenceGroupId,
        CancellationToken ct) =>
        Ok(await farmTaskService.CancelRecurrenceGroupAsync(recurrenceGroupId, ct));
}
