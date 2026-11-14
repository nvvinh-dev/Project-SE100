using System.Linq.Expressions;
using HouseholdFarm.Api.Common.Auditing;
using HouseholdFarm.Api.Common.Auth;
using HouseholdFarm.Api.Common.Errors;
using HouseholdFarm.Api.Common.Persistence;
using HouseholdFarm.Api.Common.Responses;
using HouseholdFarm.Api.Data;
using HouseholdFarm.Api.Domain.Entities;
using HouseholdFarm.Api.Domain.Enums;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace HouseholdFarm.Api.Features.Admin;

// Bảng tài khoản và hộ không có Global Query Filter: module này đọc tài khoản của mọi hộ, chỉ trừ tài khoản Quản trị.
public sealed class AdminService(
    AppDbContext db,
    UserManager<ApplicationUser> userManager,
    RefreshTokenRevoker refreshTokenRevoker,
    ICurrentUser currentUser,
    ICrossHouseholdAuditLogger audit) : IAdminService
{
    private static readonly Expression<Func<ApplicationUser, AdminAccountResponse>> _toResponse = u => new AdminAccountResponse(
        u.Id,
        u.FullName,
        u.PhoneNumber!,
        u.Role,
        u.Household!.Code,
        u.Household.Name,
        u.IsLocked,
        u.CreatedAt);

    private static readonly Func<ApplicationUser, AdminAccountResponse> _toResponseCompiled = _toResponse.Compile();

    public async Task<PagedResult<AdminAccountResponse>> GetAccountsAsync(AdminAccountQuery query, CancellationToken ct)
    {
        currentUser.EnsureSystemAdmin();

        var accounts = HouseholdAccounts().AsNoTracking();

        if (query.Role is { } role)
        {
            accounts = accounts.Where(u => u.Role == role);
        }

        if (query.IsLocked is { } isLocked)
        {
            accounts = accounts.Where(u => u.IsLocked == isLocked);
        }

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var pattern = SearchPatterns.Contains(query.Search);
            accounts = accounts.Where(u =>
                EF.Functions.ILike(u.FullName, pattern, SearchPatterns.EscapeCharacter)
                || EF.Functions.ILike(u.PhoneNumber!, pattern, SearchPatterns.EscapeCharacter)
                || EF.Functions.ILike(u.Household!.Code, pattern, SearchPatterns.EscapeCharacter)
                || EF.Functions.ILike(u.Household!.Name, pattern, SearchPatterns.EscapeCharacter));
        }

        // Sắp thêm theo Id để thứ tự giữa các trang ổn định khi giá trị sắp xếp trùng nhau.
        return await Sort(accounts, query.Sort)
            .ThenBy(u => u.Id)
            .Select(_toResponse)
            .ToPagedResultAsync(query, ct);
    }

    public async Task<AdminAccountResponse> GetAccountAsync(Guid id, CancellationToken ct)
    {
        currentUser.EnsureSystemAdmin();

        return await HouseholdAccounts().AsNoTracking().Where(u => u.Id == id).Select(_toResponse).SingleOrDefaultAsync(ct)
            ?? throw NotFound();
    }

    public Task<AdminAccountResponse> LockAsync(Guid id, CancellationToken ct) =>
        ChangeAccountAsync(id, async user =>
        {
            if (user.IsLocked)
            {
                return;
            }

            user.IsLocked = true;
            // Quản trị không thuộc hộ nào: dòng nhật ký thuộc hộ của tài khoản bị khóa để Chủ hộ thấy được.
            audit.Log(
                user.HouseholdId!.Value,
                AuditActions.ApplicationUserLock,
                user.Id,
                new { isLocked = false },
                new { isLocked = true },
                reason: null);
            await db.SaveChangesAsync(ct);

            // BR-USR-04: kết thúc mọi phiên; từ yêu cầu kế tiếp tài khoản bị chặn với ACCOUNT_LOCKED (BR-GEN-04).
            await refreshTokenRevoker.RevokeAllAsync(user.Id, ct);
        }, ct);

    public Task<AdminAccountResponse> UnlockAsync(Guid id, CancellationToken ct) =>
        ChangeAccountAsync(id, async user =>
        {
            if (!user.IsLocked)
            {
                return;
            }

            // Chỉ mở khóa do Chủ hộ, Quản trị thực hiện; khóa tạm do đăng nhập sai là cơ chế khác, tự hết (BR-USR-15).
            user.IsLocked = false;
            audit.Log(
                user.HouseholdId!.Value,
                AuditActions.ApplicationUserUnlock,
                user.Id,
                new { isLocked = true },
                new { isLocked = false },
                reason: null);
            await db.SaveChangesAsync(ct);
        }, ct);

    public Task<AdminAccountResponse> ResetPasswordAsync(Guid id, ResetAccountPasswordRequest request, CancellationToken ct) =>
        ChangeAccountAsync(id, async user =>
        {
            // FR-ADM-03: Quản trị chỉ đặt lại mật khẩu cho Chủ hộ; thành viên quên mật khẩu do Chủ hộ đặt lại (FR-HH-09).
            if (user.Role != UserRole.Owner)
            {
                throw new AppException(
                    ErrorCodes.Forbidden,
                    "Quản trị chỉ đặt lại mật khẩu cho Chủ hộ. Thành viên quên mật khẩu thì Chủ hộ đặt lại.");
            }

            // Validator đã kiểm tra mật khẩu theo D-22, cùng chính sách với Identity. Đổi security stamp để mật khẩu cũ
            // không còn giá trị với mọi cơ chế của Identity dựa trên stamp.
            user.PasswordHash = userManager.PasswordHasher.HashPassword(user, request.NewPassword);
            user.SecurityStamp = Guid.NewGuid().ToString("N");
            // BR-USR-15: người quên mật khẩu thường đã nhập sai nhiều lần; gỡ khóa tạm để đăng nhập được ngay bằng mật khẩu mới.
            user.AccessFailedCount = 0;
            user.LockoutEnd = null;
            await db.SaveChangesAsync(ct);

            // BR-USR-04: kết thúc mọi phiên; Chủ hộ đăng nhập lại bằng mật khẩu mới.
            await refreshTokenRevoker.RevokeAllAsync(user.Id, ct);
        }, ct);

    // Khung chung của thao tác lên một tài khoản: chỉ Quản trị; khóa dòng người dùng trước khi đọc, cùng thứ tự khóa với luồng
    // xác thực và thao tác của Chủ hộ; giao dịch bao cả nhật ký và thu hồi phiên (BR-GEN-07).
    private async Task<AdminAccountResponse> ChangeAccountAsync(Guid id, Func<ApplicationUser, Task> change, CancellationToken ct)
    {
        currentUser.EnsureSystemAdmin();

        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        await refreshTokenRevoker.LockUserAsync(id, ct);

        var user = await HouseholdAccounts().Include(u => u.Household).SingleOrDefaultAsync(u => u.Id == id, ct)
            ?? throw NotFound();

        await change(user);
        await transaction.CommitAsync(ct);

        return _toResponseCompiled(user);
    }

    // BR-USR-06: chỉ tài khoản thuộc hộ; tài khoản Quản trị được xử lý như không tồn tại.
    private IQueryable<ApplicationUser> HouseholdAccounts() =>
        db.Users.Where(u => u.Role != UserRole.SystemAdmin && u.HouseholdId != null);

    // Tham số sort đã được AdminAccountQueryValidator kiểm tra, chỉ còn các trường trong AdminAccountQuery.SortFields.
    private static IOrderedQueryable<ApplicationUser> Sort(IQueryable<ApplicationUser> users, string? sort)
    {
        var descending = sort?.StartsWith('-') == true;
        return sort?.TrimStart('-') switch
        {
            "householdCode" => descending
                ? users.OrderByDescending(u => u.Household!.Code)
                : users.OrderBy(u => u.Household!.Code),
            "createdAt" => descending ? users.OrderByDescending(u => u.CreatedAt) : users.OrderBy(u => u.CreatedAt),
            _ => descending ? users.OrderByDescending(u => u.FullName) : users.OrderBy(u => u.FullName),
        };
    }

    private static AppException NotFound() => AppException.NotFound("tài khoản");
}
