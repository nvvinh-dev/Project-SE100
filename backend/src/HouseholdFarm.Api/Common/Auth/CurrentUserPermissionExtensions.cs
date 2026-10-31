using HouseholdFarm.Api.Common.Errors;
using HouseholdFarm.Api.Domain.Enums;

namespace HouseholdFarm.Api.Common.Auth;

/// <summary>
/// Kiểm tra quyền chức năng theo ma trận phân quyền, gọi ở đầu hàm service (không ở controller, vì công cụ AI gọi thẳng service).
/// Vi phạm ném <c>FORBIDDEN</c> (BR-GEN-03); chưa đăng nhập ném <c>UNAUTHENTICATED</c>.
/// </summary>
/// <remarks>
/// Các hàm này chỉ trả lời câu hỏi "ai được làm". Bản ghi ngoài phạm vi làm việc thì lọc bằng <c>VisibleTo</c> và báo
/// <c>NOT_FOUND</c> (BR-GEN-02); điều kiện trạng thái mùa vụ do service tự kiểm tra.
/// </remarks>
public static class CurrentUserPermissionExtensions
{
    /// <summary>
    /// Người dùng có quyền bổ sung <paramref name="permission"/>: Chủ hộ luôn có, Thành viên hộ có khi được bật,
    /// Nhân công và Quản trị hệ thống không có.
    /// </summary>
    public static bool HasPermission(this ICurrentUser user, ExtraPermission permission)
    {
        if (!user.IsAuthenticated)
        {
            return false;
        }

        return user.Role switch
        {
            UserRole.Owner => true,
            UserRole.Member => permission switch
            {
                ExtraPermission.ManageStock => user.CanManageStock,
                ExtraPermission.ManageFinance => user.CanManageFinance,
                ExtraPermission.CloseSeason => user.CanCloseSeason,
                _ => throw new ArgumentOutOfRangeException(nameof(permission), permission, null),
            },
            _ => false,
        };
    }

    /// <summary>Mọi người đã đăng nhập, kể cả Quản trị hệ thống (thông tin của chính mình).</summary>
    public static void EnsureAuthenticated(this ICurrentUser user) => Ensure(user, static _ => true, string.Empty);

    /// <summary>Chỉ Chủ hộ.</summary>
    public static void EnsureOwner(this ICurrentUser user) =>
        Ensure(user, static u => u.Role == UserRole.Owner, "Chỉ Chủ hộ được thực hiện thao tác này.");

    /// <summary>
    /// Chủ hộ hoặc Thành viên hộ. Quản trị hệ thống cũng bị từ chối: ma trận không cho Quản trị chức năng nào của hộ.
    /// </summary>
    public static void EnsureNotWorker(this ICurrentUser user) =>
        Ensure(
            user,
            static u => u.Role is UserRole.Owner or UserRole.Member,
            "Chỉ Chủ hộ và Thành viên hộ được thực hiện thao tác này.");

    /// <summary>Chủ hộ, hoặc Thành viên hộ có quyền Quản lý kho (áp dụng toàn hộ, BR-USR-13).</summary>
    public static void EnsureCanManageStock(this ICurrentUser user) =>
        Ensure(user, static u => u.HasPermission(ExtraPermission.ManageStock), "Thao tác này cần quyền Quản lý kho.");

    /// <summary>Chủ hộ, hoặc Thành viên hộ có quyền Quản lý tài chính (áp dụng toàn hộ, BR-USR-13).</summary>
    public static void EnsureCanManageFinance(this ICurrentUser user) =>
        Ensure(user, static u => u.HasPermission(ExtraPermission.ManageFinance), "Thao tác này cần quyền Quản lý tài chính.");

    /// <summary>
    /// Chủ hộ, hoặc Thành viên hộ có quyền Chốt mùa vụ. Quyền này chỉ áp dụng cho mùa vụ được phân công (BR-USR-13):
    /// service tìm mùa vụ qua <c>VisibleTo</c> trước, mùa vụ ngoài phạm vi đã thành <c>NOT_FOUND</c>.
    /// </summary>
    public static void EnsureCanCloseSeason(this ICurrentUser user) =>
        Ensure(user, static u => u.HasPermission(ExtraPermission.CloseSeason), "Thao tác này cần quyền Chốt mùa vụ.");

    /// <summary>Chỉ Quản trị hệ thống (module ADM).</summary>
    public static void EnsureSystemAdmin(this ICurrentUser user) =>
        Ensure(user, static u => u.Role == UserRole.SystemAdmin, "Chỉ Quản trị hệ thống được thực hiện thao tác này.");

    // Kiểm tra đăng nhập trước: người chưa đăng nhập không có vai trò để đọc.
    private static void Ensure(ICurrentUser user, Func<ICurrentUser, bool> isAllowed, string message)
    {
        if (!user.IsAuthenticated)
        {
            throw new AppException(ErrorCodes.Unauthenticated, "Cần đăng nhập để thực hiện thao tác này.");
        }

        if (!isAllowed(user))
        {
            throw new AppException(ErrorCodes.Forbidden, message);
        }
    }
}
