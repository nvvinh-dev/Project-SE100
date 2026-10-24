using HouseholdFarm.Api.Common.Auth;
using HouseholdFarm.Api.Domain.Entities;
using HouseholdFarm.Api.Domain.Enums;

namespace HouseholdFarm.Api.Common.Persistence;

/// <summary>
/// Lọc dữ liệu theo phạm vi làm việc (ký hiệu <c>PV</c>, <c>Giao</c> của ma trận phân quyền). Bản ghi ngoài phạm vi được xử lý
/// như không tồn tại (BR-GEN-02): tìm một bản ghi theo id cũng lọc qua đây rồi mới báo <c>NOT_FOUND</c>.
/// Không tự viết lại các điều kiện này trong module.
/// </summary>
/// <remarks>
/// <para>
/// Chủ hộ thấy cả hộ; Thành viên hộ thấy dữ liệu của mùa vụ được phân công; Nhân công chỉ thấy công việc giao cho mình và vật tư
/// của công việc đó (BR-USR-14), còn tên cây, thửa của mùa vụ thì lấy qua công việc; Quản trị hệ thống và người chưa đăng nhập
/// không thấy gì. Lọc theo hộ đã do Global Query Filter làm, các hàm này chỉ thu hẹp thêm trong hộ.
/// </para>
/// <para>
/// Giao dịch bán, thanh toán, chi phí, giao dịch kho, tóm tắt AI áp dụng toàn hộ theo quyền Kho, TC nên không lọc ở đây,
/// chỉ kiểm tra quyền bằng các hàm <c>Ensure...</c> của <see cref="ICurrentUser"/>.
/// </para>
/// </remarks>
public static class WorkScopeQueryExtensions
{
    /// <summary>Mùa vụ trong phạm vi làm việc (phần sản xuất của mùa vụ).</summary>
    public static IQueryable<Season> VisibleTo(this IQueryable<Season> seasons, ICurrentUser user) =>
        SeasonIdsInScope(user) is { } ids ? seasons.Where(s => ids.Contains(s.Id)) : seasons;

    /// <summary>
    /// Mùa vụ trong phạm vi làm việc, cộng mọi mùa vụ của hộ khi người dùng có <paramref name="widenedBy"/> (BR-USR-13):
    /// dùng khi chọn mùa vụ để ghi dữ liệu thuộc quyền đó và khi xem phần mùa vụ mà quyền đó được thấy.
    /// </summary>
    /// <remarks>
    /// Với mùa vụ ngoài phân công, <c>ManageStock</c> chỉ được thấy thông tin rút gọn (cây trồng, thửa, ngày bắt đầu,
    /// ngày dự kiến thu hoạch, trạng thái); <c>ManageFinance</c> thấy thêm tồn nông sản, sản lượng, phần tài chính.
    /// Service tự chọn cột trả về theo quyền.
    /// </remarks>
    /// <param name="widenedBy"><c>ManageStock</c> hoặc <c>ManageFinance</c>.</param>
    public static IQueryable<Season> VisibleTo(this IQueryable<Season> seasons, ICurrentUser user, ExtraPermission widenedBy) =>
        IsWidened(user, widenedBy, ExtraPermission.ManageStock, ExtraPermission.ManageFinance)
            ? seasons
            : seasons.VisibleTo(user);

    /// <summary>
    /// Công việc trong phạm vi: Thành viên hộ thấy mọi công việc của mùa vụ được phân công; Nhân công chỉ thấy công việc giao
    /// cho mình. Quyền Kho, TC không mở rộng phạm vi này (BR-USR-13).
    /// </summary>
    public static IQueryable<FarmTask> VisibleTo(this IQueryable<FarmTask> farmTasks, ICurrentUser user)
    {
        // BR-USR-14: theo người được giao, không theo phân công, nên Nhân công đã bị thu hồi phân công vẫn thấy công việc
        // Done của mình (công việc Done giữ người thực hiện, BR-USR-12).
        if (IsWorker(user))
        {
            var userId = user.UserId;
            return farmTasks.Where(t => t.AssigneeId == userId);
        }

        return SeasonIdsInScope(user) is { } ids ? farmTasks.Where(t => ids.Contains(t.SeasonId)) : farmTasks;
    }

    /// <summary>Vật tư của công việc, cùng phạm vi với công việc chứa nó.</summary>
    public static IQueryable<FarmTaskSupplyUsage> VisibleTo(this IQueryable<FarmTaskSupplyUsage> usages, ICurrentUser user)
    {
        if (IsWorker(user))
        {
            var userId = user.UserId;
            return usages.Where(u => u.FarmTask.AssigneeId == userId);
        }

        return SeasonIdsInScope(user) is { } ids ? usages.Where(u => ids.Contains(u.FarmTask.SeasonId)) : usages;
    }

    /// <summary>Ghi chú kinh nghiệm của mùa vụ trong phạm vi.</summary>
    public static IQueryable<SeasonNote> VisibleTo(this IQueryable<SeasonNote> notes, ICurrentUser user) =>
        SeasonIdsInScope(user) is { } ids ? notes.Where(n => ids.Contains(n.SeasonId)) : notes;

    /// <summary>Vật tư dự kiến của mùa vụ trong phạm vi.</summary>
    public static IQueryable<SeasonSupplyPlan> VisibleTo(this IQueryable<SeasonSupplyPlan> plans, ICurrentUser user) =>
        SeasonIdsInScope(user) is { } ids ? plans.Where(p => ids.Contains(p.SeasonId)) : plans;

    /// <summary>
    /// Phân công của mùa vụ trong phạm vi; Thành viên hộ dùng để thấy những người cùng mùa vụ với mình.
    /// </summary>
    public static IQueryable<SeasonAssignment> VisibleTo(this IQueryable<SeasonAssignment> assignments, ICurrentUser user) =>
        SeasonIdsInScope(user) is { } ids ? assignments.Where(a => ids.Contains(a.SeasonId)) : assignments;

    /// <summary>Phiếu thu hoạch của mùa vụ trong phạm vi.</summary>
    public static IQueryable<HarvestRecord> VisibleTo(this IQueryable<HarvestRecord> harvestRecords, ICurrentUser user) =>
        SeasonIdsInScope(user) is { } ids ? harvestRecords.Where(h => ids.Contains(h.SeasonId)) : harvestRecords;

    /// <summary>
    /// Phiếu thu hoạch trong phạm vi, cộng mọi phiếu của hộ khi người dùng có <c>ManageFinance</c> (BR-USR-13): chỉ dùng
    /// để chọn liên kết giao dịch bán và tính tồn nông sản, sản lượng của mùa vụ ngoài phân công.
    /// </summary>
    /// <param name="widenedBy">Chỉ nhận <c>ManageFinance</c>.</param>
    public static IQueryable<HarvestRecord> VisibleTo(
        this IQueryable<HarvestRecord> harvestRecords,
        ICurrentUser user,
        ExtraPermission widenedBy) =>
        IsWidened(user, widenedBy, ExtraPermission.ManageFinance) ? harvestRecords : harvestRecords.VisibleTo(user);

    /// <summary>Điều chỉnh tồn nông sản của mùa vụ trong phạm vi.</summary>
    public static IQueryable<ProduceStockAdjustment> VisibleTo(
        this IQueryable<ProduceStockAdjustment> adjustments,
        ICurrentUser user) =>
        SeasonIdsInScope(user) is { } ids ? adjustments.Where(a => ids.Contains(a.SeasonId)) : adjustments;

    /// <summary>
    /// Điều chỉnh tồn nông sản trong phạm vi, cộng mọi điều chỉnh của hộ khi người dùng có <c>ManageFinance</c> (BR-USR-13):
    /// dùng để tính tồn nông sản của mùa vụ ngoài phân công.
    /// </summary>
    /// <param name="widenedBy">Chỉ nhận <c>ManageFinance</c>.</param>
    public static IQueryable<ProduceStockAdjustment> VisibleTo(
        this IQueryable<ProduceStockAdjustment> adjustments,
        ICurrentUser user,
        ExtraPermission widenedBy) =>
        IsWidened(user, widenedBy, ExtraPermission.ManageFinance) ? adjustments : adjustments.VisibleTo(user);

    // null nghĩa là cả hộ, không cần lọc thêm.
    private static IReadOnlyCollection<Guid>? SeasonIdsInScope(ICurrentUser user)
    {
        if (!user.IsAuthenticated)
        {
            return [];
        }

        return user.Role switch
        {
            UserRole.Owner => null,
            UserRole.Member => user.AssignedSeasonIds,
            // Nhân công được phân công vào mùa vụ chỉ để nhận công việc, không xem được mùa vụ (BR-USR-14);
            // Quản trị hệ thống không thuộc hộ nào.
            _ => [],
        };
    }

    private static bool IsWorker(ICurrentUser user) => user.IsAuthenticated && user.Role == UserRole.Worker;

    // BR-USR-13: CloseSeason chỉ áp dụng cho mùa vụ được phân công, ManageStock chỉ mở thông tin rút gọn của mùa vụ,
    // nên mỗi bảng chỉ nhận những quyền được mở rộng tới nó; truyền sai là lỗi lập trình.
    private static bool IsWidened(ICurrentUser user, ExtraPermission widenedBy, params ExtraPermission[] allowed)
    {
        if (!allowed.Contains(widenedBy))
        {
            throw new ArgumentOutOfRangeException(
                nameof(widenedBy),
                widenedBy,
                $"Quyền {widenedBy} không mở rộng phạm vi của bảng này.");
        }

        return user.HasPermission(widenedBy);
    }
}
