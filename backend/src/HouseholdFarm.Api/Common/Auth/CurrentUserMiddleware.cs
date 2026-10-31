using System.Security.Claims;
using HouseholdFarm.Api.Common.Errors;
using HouseholdFarm.Api.Data;
using HouseholdFarm.Api.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.JsonWebTokens;

namespace HouseholdFarm.Api.Common.Auth;

/// <summary>
/// Nạp <see cref="CurrentUser"/> từ database một lần mỗi yêu cầu (D-11): hộ, vai trò, quyền bổ sung, trạng thái khóa,
/// mùa vụ được phân công. Access token chỉ mang mã người dùng, nên khóa tài khoản, đổi vai trò, đổi quyền, đổi phân công
/// có hiệu lực ngay ở yêu cầu kế tiếp.
/// </summary>
/// <remarks>Đặt sau xác thực JWT và phân quyền theo <c>[Authorize]</c>, trước controller.</remarks>
public sealed class CurrentUserMiddleware(RequestDelegate next)
{
    /// <summary>Thông báo cho tài khoản bị khóa (BR-GEN-04), dùng cả khi đăng nhập.</summary>
    public const string AccountLockedMessage =
        "Tài khoản đã bị khóa. Liên hệ Chủ hộ hoặc quản trị hệ thống để được mở khóa.";

    public async Task InvokeAsync(HttpContext context, CurrentUser currentUser)
    {
        // Endpoint cho phép chưa đăng nhập (đăng nhập, đăng ký, làm mới phiên) không dùng người dùng hiện tại,
        // kể cả khi trình duyệt còn gửi kèm access token cũ.
        if (context.GetEndpoint() is { } endpoint
            && endpoint.Metadata.GetMetadata<IAllowAnonymous>() is null
            && context.User.FindFirstValue(JwtRegisteredClaimNames.Sub) is { } subject
            && Guid.TryParse(subject, out var userId))
        {
            var db = context.RequestServices.GetRequiredService<AppDbContext>();
            await LoadAsync(db, currentUser, userId, context.RequestAborted);
        }

        await next(context);
    }

    private static async Task LoadAsync(AppDbContext db, CurrentUser currentUser, Guid userId, CancellationToken ct)
    {
        var user = await db.Users
            .AsNoTracking()
            .Where(u => u.Id == userId)
            .Select(u => new { u.HouseholdId, u.Role, u.CanManageStock, u.CanManageFinance, u.CanCloseSeason, u.IsLocked })
            .SingleOrDefaultAsync(ct)
            ?? throw new AppException(ErrorCodes.Unauthenticated, "Phiên đăng nhập không còn hợp lệ, vui lòng đăng nhập lại.");

        // BR-GEN-04: tài khoản bị khóa không thực hiện được thao tác nào, kể cả khi access token còn hạn.
        if (user.IsLocked)
        {
            throw new AppException(ErrorCodes.AccountLocked, AccountLockedMessage);
        }

        currentUser.SignIn(userId, user.HouseholdId, user.Role, user.CanManageStock, user.CanManageFinance, user.CanCloseSeason);

        // Chủ hộ thấy cả hộ, Quản trị không thuộc hộ nào: chỉ Thành viên hộ, Nhân công có phạm vi làm việc.
        // Đọc sau khi đã có hộ, vì Global Query Filter của season_assignments lấy hộ từ chính người dùng hiện tại.
        if (user.Role is UserRole.Member or UserRole.Worker)
        {
            currentUser.SetAssignedSeasonIds(await db.SeasonAssignments
                .AsNoTracking()
                .Where(a => a.UserId == userId)
                .Select(a => a.SeasonId)
                .ToListAsync(ct));
        }
    }
}
