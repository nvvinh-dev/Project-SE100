using HouseholdFarm.Api.Common.Responses;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HouseholdFarm.Api.Features.Households;

// Controller chỉ nhận yêu cầu và gọi đúng một hàm service; kiểm tra quyền, quy tắc nghiệp vụ nằm trong service.
// Không có API xóa tài khoản: khóa để giữ lịch sử (FR-HH-05).
[ApiController]
[Route("api/members")]
[Authorize]
public sealed class MembersController(IMemberService memberService) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<PagedResult<MemberListItem>>> GetListAsync([FromQuery] MemberQuery query, CancellationToken ct) =>
        Ok(await memberService.GetListAsync(query, ct));

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<MemberResponse>> GetAsync(Guid id, CancellationToken ct) =>
        Ok(await memberService.GetAsync(id, ct));

    [HttpPost]
    public async Task<ActionResult<MemberResponse>> CreateAsync(CreateMemberRequest request, CancellationToken ct)
    {
        var member = await memberService.CreateAsync(request, ct);
        return CreatedAtAction(nameof(GetAsync), new { id = member.Id }, member);
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<MemberResponse>> UpdateAsync(Guid id, UpdateMemberRequest request, CancellationToken ct) =>
        Ok(await memberService.UpdateAsync(id, request, ct));

    [HttpPost("{id:guid}/change-role")]
    public async Task<ActionResult<MemberResponse>> ChangeRoleAsync(Guid id, ChangeMemberRoleRequest request, CancellationToken ct) =>
        Ok(await memberService.ChangeRoleAsync(id, request, ct));

    [HttpPost("{id:guid}/change-permissions")]
    public async Task<ActionResult<MemberResponse>> ChangePermissionsAsync(
        Guid id,
        ChangeMemberPermissionsRequest request,
        CancellationToken ct) =>
        Ok(await memberService.ChangePermissionsAsync(id, request, ct));

    [HttpPost("{id:guid}/lock")]
    public async Task<ActionResult<MemberResponse>> LockAsync(Guid id, CancellationToken ct) =>
        Ok(await memberService.LockAsync(id, ct));

    [HttpPost("{id:guid}/unlock")]
    public async Task<ActionResult<MemberResponse>> UnlockAsync(Guid id, CancellationToken ct) =>
        Ok(await memberService.UnlockAsync(id, ct));

    [HttpPost("{id:guid}/reset-password")]
    public async Task<ActionResult<MemberResponse>> ResetPasswordAsync(
        Guid id,
        ResetMemberPasswordRequest request,
        CancellationToken ct) =>
        Ok(await memberService.ResetPasswordAsync(id, request, ct));
}
