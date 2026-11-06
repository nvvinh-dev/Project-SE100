using HouseholdFarm.Api.Common.Responses;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HouseholdFarm.Api.Features.Supplies;

// Không có API xóa vật tư và không có API sửa tồn: muốn đổi tồn phải tạo giao dịch kho (BR-SUP-01).
[ApiController]
[Route("api/supplies")]
[Authorize]
public sealed class SuppliesController(ISupplyService supplyService) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<PagedResult<SupplyListItem>>> GetListAsync([FromQuery] SupplyQuery query, CancellationToken ct) =>
        Ok(await supplyService.GetListAsync(query, ct));

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<SupplyResponse>> GetAsync(Guid id, CancellationToken ct) =>
        Ok(await supplyService.GetAsync(id, ct));

    [HttpPost]
    public async Task<ActionResult<SupplyResponse>> CreateAsync(CreateSupplyRequest request, CancellationToken ct)
    {
        var supply = await supplyService.CreateAsync(request, ct);
        return CreatedAtAction(nameof(GetAsync), new { id = supply.Id }, supply);
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<SupplyResponse>> UpdateAsync(Guid id, UpdateSupplyRequest request, CancellationToken ct) =>
        Ok(await supplyService.UpdateAsync(id, request, ct));
}
