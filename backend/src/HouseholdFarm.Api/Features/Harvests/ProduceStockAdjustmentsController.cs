using HouseholdFarm.Api.Common.Responses;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HouseholdFarm.Api.Features.Harvests;

// Điều chỉnh giảm tồn nông sản không có PUT, DELETE và không có Correct; ghi nhầm thì hủy rồi ghi lại (BR-HAR-05).
[ApiController]
[Route("api/produce-stock-adjustments")]
[Authorize]
public sealed class ProduceStockAdjustmentsController(
    IProduceStockAdjustmentService adjustmentService) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<PagedResult<ProduceStockAdjustmentResponse>>> GetListAsync(
        [FromQuery] ProduceStockAdjustmentQuery query,
        CancellationToken ct) =>
        Ok(await adjustmentService.GetListAsync(query, ct));

    [HttpPost]
    public async Task<ActionResult<ProduceStockAdjustmentResponse>> CreateAsync(
        CreateProduceStockAdjustmentRequest request,
        CancellationToken ct)
    {
        var created = await adjustmentService.CreateAsync(request, ct);
        return StatusCode(StatusCodes.Status201Created, created);
    }

    [HttpPost("{id:guid}/void")]
    public async Task<ActionResult<ProduceStockAdjustmentResponse>> VoidAsync(
        Guid id,
        VoidProduceStockAdjustmentRequest request,
        CancellationToken ct) =>
        Ok(await adjustmentService.VoidAsync(id, request, ct));
}
