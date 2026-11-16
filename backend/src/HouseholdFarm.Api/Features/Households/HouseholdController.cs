using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HouseholdFarm.Api.Features.Households;

// Số ít: luôn là hộ của người đang đăng nhập, không nhận id hộ từ client. Kiểm tra quyền nằm trong service.
[ApiController]
[Route("api/household")]
[Authorize]
public sealed class HouseholdController(IHouseholdService householdService) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<HouseholdResponse>> GetAsync(CancellationToken ct) =>
        Ok(await householdService.GetAsync(ct));

    [HttpPut]
    public async Task<ActionResult<HouseholdResponse>> UpdateAsync(UpdateHouseholdRequest request, CancellationToken ct) =>
        Ok(await householdService.UpdateAsync(request, ct));

    [HttpGet("settings")]
    public async Task<ActionResult<HouseholdSettingsResponse>> GetSettingsAsync(CancellationToken ct) =>
        Ok(await householdService.GetSettingsAsync(ct));

    [HttpPost("change-settings")]
    public async Task<ActionResult<HouseholdSettingsResponse>> ChangeSettingsAsync(
        ChangeHouseholdSettingsRequest request,
        CancellationToken ct) =>
        Ok(await householdService.ChangeSettingsAsync(request, ct));
}
