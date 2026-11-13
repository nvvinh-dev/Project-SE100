using HouseholdFarm.Api.Common.Responses;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HouseholdFarm.Api.Features.Crops;

// Controller chỉ nhận yêu cầu và gọi đúng một hàm service: kiểm tra quyền, quy tắc nghiệp vụ đều nằm trong service,
// vì công cụ AI gọi thẳng service. Không có API xóa cây trồng (FR-CROP-03).
[ApiController]
[Route("api/crops")]
[Authorize]
public sealed class CropsController(ICropService cropService) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<PagedResult<CropListItem>>> GetListAsync([FromQuery] CropQuery query, CancellationToken ct) =>
        Ok(await cropService.GetListAsync(query, ct));

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<CropResponse>> GetAsync(Guid id, CancellationToken ct) =>
        Ok(await cropService.GetAsync(id, ct));

    [HttpPost]
    public async Task<ActionResult<CropResponse>> CreateAsync(CreateCropRequest request, CancellationToken ct)
    {
        var crop = await cropService.CreateAsync(request, ct);
        return CreatedAtAction(nameof(GetAsync), new { id = crop.Id }, crop);
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<CropResponse>> UpdateAsync(Guid id, UpdateCropRequest request, CancellationToken ct) =>
        Ok(await cropService.UpdateAsync(id, request, ct));
}
