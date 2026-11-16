using HouseholdFarm.Api.Common.Responses;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HouseholdFarm.Api.Features.Admin;

// Controller chỉ nhận yêu cầu và gọi đúng một hàm service; kiểm tra quyền Quản trị nằm trong service.
[ApiController]
[Route("api/admin")]
[Authorize]
public sealed class AdminController(IAdminService adminService) : ControllerBase
{
    [HttpGet("accounts")]
    public async Task<ActionResult<PagedResult<AdminAccountResponse>>> GetAccountsAsync(
        [FromQuery] AdminAccountQuery query,
        CancellationToken ct) =>
        Ok(await adminService.GetAccountsAsync(query, ct));

    [HttpGet("accounts/{id:guid}")]
    public async Task<ActionResult<AdminAccountResponse>> GetAccountAsync(Guid id, CancellationToken ct) =>
        Ok(await adminService.GetAccountAsync(id, ct));

    [HttpPost("accounts/{id:guid}/lock")]
    public async Task<ActionResult<AdminAccountResponse>> LockAsync(Guid id, CancellationToken ct) =>
        Ok(await adminService.LockAsync(id, ct));

    [HttpPost("accounts/{id:guid}/unlock")]
    public async Task<ActionResult<AdminAccountResponse>> UnlockAsync(Guid id, CancellationToken ct) =>
        Ok(await adminService.UnlockAsync(id, ct));

    [HttpPost("accounts/{id:guid}/reset-password")]
    public async Task<ActionResult<AdminAccountResponse>> ResetPasswordAsync(
        Guid id,
        ResetAccountPasswordRequest request,
        CancellationToken ct) =>
        Ok(await adminService.ResetPasswordAsync(id, request, ct));
}
