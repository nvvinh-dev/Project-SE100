using HouseholdFarm.Api.Common.Responses;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HouseholdFarm.Api.Features.Plots;

// Controller chỉ nhận yêu cầu và gọi đúng một hàm service: kiểm tra quyền, quy tắc nghiệp vụ đều nằm trong service,
// vì công cụ AI gọi thẳng service. Không có API xóa thửa (FR-PLOT-02).
[ApiController]
[Route("api/plots")]
[Authorize]
public sealed class PlotsController(IPlotService plotService) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<PagedResult<PlotListItem>>> GetListAsync([FromQuery] PlotQuery query, CancellationToken ct) =>
        Ok(await plotService.GetListAsync(query, ct));

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<PlotResponse>> GetAsync(Guid id, CancellationToken ct) =>
        Ok(await plotService.GetAsync(id, ct));

    [HttpPost]
    public async Task<ActionResult<PlotResponse>> CreateAsync(CreatePlotRequest request, CancellationToken ct)
    {
        var plot = await plotService.CreateAsync(request, ct);
        return CreatedAtAction(nameof(GetAsync), new { id = plot.Id }, plot);
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<PlotResponse>> UpdateAsync(Guid id, UpdatePlotRequest request, CancellationToken ct) =>
        Ok(await plotService.UpdateAsync(id, request, ct));
}
