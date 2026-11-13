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
using Npgsql;

namespace HouseholdFarm.Api.Features.Households;

public sealed class MemberService(
    AppDbContext db,
    UserManager<ApplicationUser> userManager,
    RefreshTokenRevoker refreshTokenRevoker,
    ICurrentUser currentUser,
    IAuditLogger audit) : IMemberService
{
    public async Task<PagedResult<MemberListItem>> GetListAsync(MemberQuery query, CancellationToken ct)
    {
        currentUser.EnsureNotWorker();

        // asp_net_users không có Global Query Filter: lọc theo hộ bằng hàm dùng chung.
        var users = db.Users.AsNoTracking().InCurrentHousehold(currentUser);
        var isOwner = currentUser.Role == UserRole.Owner;

        if (!isOwner)
        {
            // Ma trận phân quyền, ghi chú 1: Thành viên hộ chỉ thấy Chủ hộ và người cùng được phân công vào mùa vụ của mình,
            // chỉ họ tên và vai trò. Trạng thái khóa không được thấy nên cũng không lọc, không tìm theo số điện thoại được.
            var me = currentUser.UserId;
            var mySeasonIds = db.SeasonAssignments.Where(a => a.UserId == me).Select(a => a.SeasonId);
            users = users.Where(u => u.Role == UserRole.Owner
                || db.SeasonAssignments.Any(a => a.UserId == u.Id && mySeasonIds.Contains(a.SeasonId)));
            query = query with { IsLocked = null };
        }

        if (query.Role is { } role)
        {
            users = users.Where(u => u.Role == role);
        }

        if (query.IsLocked is { } isLocked)
        {
            users = users.Where(u => u.IsLocked == isLocked);
        }

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var pattern = SearchPatterns.Contains(query.Search);
            users = isOwner
                ? users.Where(u => EF.Functions.ILike(u.FullName, pattern, SearchPatterns.EscapeCharacter)
                    || EF.Functions.ILike(u.PhoneNumber!, pattern, SearchPatterns.EscapeCharacter))
                : users.Where(u => EF.Functions.ILike(u.FullName, pattern, SearchPatterns.EscapeCharacter));
        }

        // Sắp thêm theo Id để thứ tự giữa các trang ổn định khi giá trị sắp xếp trùng nhau.
        return await Sort(users, query.Sort)
            .ThenBy(u => u.Id)
            .Select(u => new MemberListItem(
                u.Id,
                u.FullName,
                isOwner ? u.PhoneNumber : null,
                u.Role,
                isOwner ? u.CanManageStock : null,
                isOwner ? u.CanManageFinance : null,
                isOwner ? u.CanCloseSeason : null,
                isOwner ? u.IsLocked : null))
            .ToPagedResultAsync(query, ct);
    }

    public async Task<MemberResponse> GetAsync(Guid id, CancellationToken ct)
    {
        currentUser.EnsureOwner();

        var user = await db.Users.AsNoTracking().InCurrentHousehold(currentUser).SingleOrDefaultAsync(u => u.Id == id, ct)
            ?? throw NotFound();
        return ToResponse(user);
    }

    public async Task<MemberResponse> CreateAsync(CreateMemberRequest request, CancellationToken ct)
    {
        currentUser.EnsureOwner();

        // Validator đã bảo đảm số chuẩn hóa được (BR-USR-01) và vai trò là Member hoặc Worker (BR-USR-08).
        var phone = PhoneNumbers.Normalize(request.Phone)!;
        await EnsurePhoneUnusedAsync(phone, exceptUserId: null, ct);

        // Bảng người dùng không có interceptor điền household_id: tài khoản mới thuộc hộ của Chủ hộ đang đăng nhập.
        // Ba quyền bổ sung mặc định tắt (FR-HH-07).
        var user = new ApplicationUser
        {
            HouseholdId = currentUser.HouseholdId,
            FullName = request.FullName.Trim(),
            Role = request.Role!.Value,
            UserName = phone,
            PhoneNumber = phone,
        };
        await SaveUserAsync(() => userManager.CreateAsync(user, request.InitialPassword));

        return ToResponse(user);
    }

    public async Task<MemberResponse> UpdateAsync(Guid id, UpdateMemberRequest request, CancellationToken ct)
    {
        var phone = PhoneNumbers.Normalize(request.Phone)!;

        return await ChangeMemberAsync(id, request.Version, async user =>
        {
            if (phone != user.PhoneNumber)
            {
                await EnsurePhoneUnusedAsync(phone, exceptUserId: user.Id, ct);
            }

            user.FullName = request.FullName.Trim();
            // Đăng nhập tìm theo user_name nên đổi cùng lúc với phone_number.
            user.UserName = phone;
            user.NormalizedUserName = userManager.NormalizeName(phone);
            user.PhoneNumber = phone;
            await SaveAsync(ct);
        }, ct);
    }

    public Task<MemberResponse> ChangeRoleAsync(Guid id, ChangeMemberRoleRequest request, CancellationToken ct) =>
        ChangeMemberAsync(id, request.Version, async user =>
        {
            // Validator đã bảo đảm vai trò mới là Member hoặc Worker (BR-USR-08).
            var role = request.Role!.Value;
            if (role == user.Role)
            {
                return;
            }

            user.Role = role;
            // BR-USR-09: Nhân công không có quyền bổ sung; không tắt thì đổi lại sang Thành viên hộ sẽ âm thầm có lại quyền cũ.
            if (role == UserRole.Worker)
            {
                user.CanManageStock = false;
                user.CanManageFinance = false;
                user.CanCloseSeason = false;
            }

            var changes = ChangedValues.Of(db.Entry(user));
            audit.Log(AuditActions.ApplicationUserChangeRole, user.Id, changes.OldValues, changes.NewValues, reason: null);
            await SaveAsync(ct);

            // BR-USR-04: kết thúc mọi phiên để thành viên đăng nhập lại và nhận vai trò mới.
            await refreshTokenRevoker.RevokeAllAsync(user.Id, ct);
        }, ct);

    public Task<MemberResponse> ChangePermissionsAsync(Guid id, ChangeMemberPermissionsRequest request, CancellationToken ct) =>
        ChangeMemberAsync(id, request.Version, async user =>
        {
            // BR-USR-09: chỉ Thành viên hộ có quyền bổ sung; ràng buộc trong database là lớp bảo vệ cuối.
            if (user.Role != UserRole.Member
                && (request.CanManageStock || request.CanManageFinance || request.CanCloseSeason))
            {
                throw new AppException(ErrorCodes.ValidationError, "Nhân công không có quyền bổ sung.");
            }

            user.CanManageStock = request.CanManageStock;
            user.CanManageFinance = request.CanManageFinance;
            user.CanCloseSeason = request.CanCloseSeason;

            // D-11: quyền có hiệu lực từ yêu cầu kế tiếp vì ICurrentUser nạp lại mỗi yêu cầu, nên không cần thu hồi phiên.
            var changes = ChangedValues.Of(db.Entry(user));
            if (!changes.IsEmpty)
            {
                audit.Log(
                    AuditActions.ApplicationUserChangePermissions,
                    user.Id,
                    changes.OldValues,
                    changes.NewValues,
                    reason: null);
            }

            await SaveAsync(ct);
        }, ct);

    public Task<MemberResponse> LockAsync(Guid id, CancellationToken ct) =>
        ChangeMemberAsync(id, version: null, async user =>
        {
            if (user.IsLocked)
            {
                return;
            }

            user.IsLocked = true;
            audit.Log(AuditActions.ApplicationUserLock, user.Id, new { isLocked = false }, new { isLocked = true }, reason: null);
            await SaveAsync(ct);

            // BR-USR-04: kết thúc mọi phiên; từ yêu cầu kế tiếp người này bị chặn với ACCOUNT_LOCKED (BR-GEN-04).
            await refreshTokenRevoker.RevokeAllAsync(user.Id, ct);
        }, ct);

    public Task<MemberResponse> UnlockAsync(Guid id, CancellationToken ct) =>
        ChangeMemberAsync(id, version: null, async user =>
        {
            if (!user.IsLocked)
            {
                return;
            }

            // Chỉ mở khóa do Chủ hộ thực hiện; khóa tạm do đăng nhập sai (lockout_end) là cơ chế khác, tự hết (BR-USR-15).
            user.IsLocked = false;
            audit.Log(AuditActions.ApplicationUserUnlock, user.Id, new { isLocked = true }, new { isLocked = false }, reason: null);
            await SaveAsync(ct);
        }, ct);

    public Task<MemberResponse> ResetPasswordAsync(Guid id, ResetMemberPasswordRequest request, CancellationToken ct) =>
        ChangeMemberAsync(id, version: null, async user =>
        {
            // Validator đã kiểm tra mật khẩu theo D-22, cùng chính sách với Identity. Đổi security stamp để mật khẩu cũ
            // không còn giá trị với mọi cơ chế của Identity dựa trên stamp.
            user.PasswordHash = userManager.PasswordHasher.HashPassword(user, request.NewPassword);
            user.SecurityStamp = Guid.NewGuid().ToString("N");
            await SaveAsync(ct);

            // BR-USR-04: kết thúc mọi phiên; thành viên đăng nhập lại bằng mật khẩu mới.
            await refreshTokenRevoker.RevokeAllAsync(user.Id, ct);
        }, ct);

    // Khung chung của thao tác sửa một người trong hộ: chỉ Chủ hộ; khóa dòng người dùng trước khi đọc để thao tác cùng lúc
    // trên cùng người chạy lần lượt (cùng thứ tự khóa với luồng xác thực); kiểm tra người đích; giao dịch bao cả nhật ký và
    // thu hồi phiên (BR-GEN-07).
    private async Task<MemberResponse> ChangeMemberAsync(
        Guid id,
        uint? version,
        Func<ApplicationUser, Task> change,
        CancellationToken ct)
    {
        currentUser.EnsureOwner();

        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        await refreshTokenRevoker.LockUserAsync(id, ct);

        var user = await db.Users.InCurrentHousehold(currentUser).SingleOrDefaultAsync(u => u.Id == id, ct)
            ?? throw NotFound();

        // BR-USR-07: hộ có đúng một Chủ hộ; không sửa, khóa, đặt lại mật khẩu cho Chủ hộ qua chức năng thành viên.
        if (user.Role == UserRole.Owner || user.Id == currentUser.UserId)
        {
            throw new AppException(
                ErrorCodes.Forbidden,
                "Không thực hiện được thao tác này với tài khoản Chủ hộ. Chủ hộ sửa thông tin của mình ở trang Tài khoản.");
        }

        // BR-GEN-11: người khác đã sửa sau lần đọc của người này thì lưu báo CONCURRENCY_CONFLICT, không ghi đè.
        if (version is { } expected)
        {
            db.Entry(user).Property(u => u.Version).OriginalValue = expected;
        }

        await change(user);
        await transaction.CommitAsync(ct);

        return ToResponse(user);
    }

    // BR-USR-02: so với mọi tài khoản của mọi hộ, kể cả tài khoản bị khóa và Quản trị hệ thống.
    private async Task EnsurePhoneUnusedAsync(string phone, Guid? exceptUserId, CancellationToken ct)
    {
        var normalizedUserName = userManager.NormalizeName(phone);
        if (await db.Users.AnyAsync(u => u.NormalizedUserName == normalizedUserName && u.Id != exceptUserId, ct))
        {
            throw PhoneAlreadyUsed();
        }
    }

    // Kiểm tra trùng số ở trên có thể lọt khi hai yêu cầu cùng lúc; chỉ mục duy nhất của user_name chặn nốt.
    private async Task SaveAsync(CancellationToken ct)
    {
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (IsUserNameViolation(ex))
        {
            throw PhoneAlreadyUsed();
        }
    }

    private async Task SaveUserAsync(Func<Task<IdentityResult>> save)
    {
        IdentityResult result;
        try
        {
            result = await save();
        }
        catch (DbUpdateException ex) when (IsUserNameViolation(ex))
        {
            throw PhoneAlreadyUsed();
        }

        if (result.Errors.Any(e => e.Code == nameof(IdentityErrorDescriber.DuplicateUserName)))
        {
            throw PhoneAlreadyUsed();
        }

        // Validator đã kiểm tra mật khẩu theo cùng chính sách với Identity, lỗi còn lại là lỗi lập trình.
        if (!result.Succeeded)
        {
            throw new InvalidOperationException(
                "Identity từ chối lưu tài khoản: " + string.Join(", ", result.Errors.Select(e => e.Code)));
        }
    }

    private bool IsUserNameViolation(DbUpdateException ex)
    {
        if (ex.InnerException is not PostgresException { SqlState: PostgresErrorCodes.UniqueViolation } postgres)
        {
            return false;
        }

        var userNameIndex = db.Model.FindEntityType(typeof(ApplicationUser))!
            .GetIndexes()
            .Single(i => i.IsUnique && i.Properties.Any(p => p.Name == nameof(ApplicationUser.NormalizedUserName)));
        return postgres.ConstraintName == userNameIndex.GetDatabaseName();
    }

    // Tham số sort đã được MemberQueryValidator kiểm tra, chỉ còn các trường trong MemberQuery.SortFields.
    private static IOrderedQueryable<ApplicationUser> Sort(IQueryable<ApplicationUser> users, string? sort)
    {
        var descending = sort?.StartsWith('-') == true;
        return sort?.TrimStart('-') switch
        {
            "createdAt" => descending ? users.OrderByDescending(u => u.CreatedAt) : users.OrderBy(u => u.CreatedAt),
            _ => descending ? users.OrderByDescending(u => u.FullName) : users.OrderBy(u => u.FullName),
        };
    }

    private static MemberResponse ToResponse(ApplicationUser u) => new(
        u.Id,
        u.FullName,
        u.PhoneNumber!,
        u.Role,
        u.CanManageStock,
        u.CanManageFinance,
        u.CanCloseSeason,
        u.IsLocked,
        u.Version);

    private static AppException NotFound() => AppException.NotFound("thành viên");

    private static AppException PhoneAlreadyUsed() =>
        new(ErrorCodes.PhoneAlreadyUsed, "Số điện thoại này đã được dùng cho một tài khoản khác.");
}
