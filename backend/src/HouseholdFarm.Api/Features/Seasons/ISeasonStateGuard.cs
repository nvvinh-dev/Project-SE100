using HouseholdFarm.Api.Domain.Enums;

namespace HouseholdFarm.Api.Features.Seasons;

/// <summary>
/// Điểm nối kiểm tra và bảo vệ trạng thái mùa vụ cho các module khác (SUP, TSK, HAR, SAL, EXP):
/// kiểm tra mùa vụ tồn tại trong hộ, nằm trong phạm vi người dùng (gồm ngoại lệ quyền Kho, TC ở BR-USR-13),
/// và trạng thái cho phép thực hiện thao tác theo quy tắc trạng thái BR-SEA-10.
/// Hỗ trợ khóa dòng seasons (SELECT ... FOR UPDATE) theo BR-GEN-12 khi bên gọi thay đổi dữ liệu.
/// </summary>
public interface ISeasonStateGuard
{
    /// <summary>
    /// Kiểm tra mùa vụ tồn tại, thuộc phạm vi người dùng và ở một trong các trạng thái được phép (BR-SEA-10).
    /// Tùy chọn khóa dòng seasons (<c>FOR UPDATE</c>) theo BR-GEN-12.
    /// </summary>
    /// <param name="seasonId">Id mùa vụ cần kiểm tra.</param>
    /// <param name="allowedStatuses">Danh sách trạng thái cho phép thao tác.</param>
    /// <param name="outOfScopePermission">
    /// Quyền bổ sung cho phép thao tác trên mùa vụ ngoài phân công theo BR-USR-13.
    /// <list type="bullet">
    /// <item><description><c>null</c>: chỉ cho phép mùa vụ trong phạm vi phân công của người dùng (HAR ghi phiếu thu, TSK quản lý công việc).</description></item>
    /// <item><description><see cref="ExtraPermission.ManageStock"/>: cho phép thêm mùa vụ ngoài phân công khi SUP xuất kho thủ công.</description></item>
    /// <item><description><see cref="ExtraPermission.ManageFinance"/>: cho phép thêm mùa vụ ngoài phân công khi SAL ghi bán hàng, thanh toán; EXP ghi chi phí.</description></item>
    /// </list>
    /// Chỉ nhận hai giá trị này (quyền <see cref="ExtraPermission.CloseSeason"/> chỉ áp dụng cho mùa vụ được phân công).
    /// </param>
    /// <param name="forUpdate">
    /// <c>true</c> nếu bên gọi cần khóa dòng seasons bằng <c>FOR UPDATE</c> trong giao dịch database (BR-GEN-12)
    /// trước khi ghi dữ liệu hoặc kiểm tra tồn/công nợ; <c>false</c> nếu chỉ đọc.
    /// </param>
    /// <param name="ct">Token hủy.</param>
    /// <returns>Thông tin tóm tắt của mùa vụ (Id, Status, StartDate, PlotId, CropId...) để bên gọi tái sử dụng nếu cần.</returns>
    /// <remarks>
    /// <para>
    /// Không kiểm tra quyền chức năng: bên gọi phải tự kiểm tra quyền chức năng của người dùng trước khi gọi guard
    /// (ví dụ TC-21: Thành viên không có quyền Quản lý tài chính tạo giao dịch bán phải nhận lỗi <c>FORBIDDEN</c>,
    /// không phải <c>NOT_FOUND</c>).
    /// </para>
    /// <para>
    /// Tham gia giao dịch database của bên gọi: khi <paramref name="forUpdate"/> là <c>true</c>, bên gọi
    /// bắt buộc phải đang mở giao dịch database (<c>BeginTransactionAsync</c>). Nếu chưa mở giao dịch mà
    /// <paramref name="forUpdate"/> là <c>true</c>, hàm ném <see cref="InvalidOperationException"/> (lỗi lập trình)
    /// để phát hiện sớm lỗi bên gọi quên mở transaction. Hàm thực hiện khóa dòng <c>seasons</c> trước mọi bảng khác (BR-GEN-12)
    /// và không tự commit.
    /// </para>
    /// <para>
    /// Lỗi: <c>NOT_FOUND</c> khi mùa vụ không tồn tại trong hộ hoặc nằm ngoài phạm vi người dùng (BR-GEN-02);
    /// <c>SEASON_INVALID_STATE</c> khi trạng thái mùa vụ không nằm trong <paramref name="allowedStatuses"/>;
    /// lỗi trả kèm <c>details = { seasonId, currentStatus, allowedStatuses }</c>.
    /// </para>
    /// </remarks>
    Task<SeasonStateResponse> EnsureStatusAsync(
        Guid seasonId,
        IReadOnlyCollection<SeasonStatus> allowedStatuses,
        ExtraPermission? outOfScopePermission,
        bool forUpdate,
        CancellationToken ct);

    /// <summary>
    /// Kiểm tra mùa vụ bắt buộc đang ở trạng thái <c>InProgress</c> (Đang canh tác) theo BR-SEA-10.
    /// Đây là điều kiện tiên quyết cho hầu hết các thao tác ghi dữ liệu sản xuất, kho, bán hàng và chi phí
    /// (tạo/sửa phiếu thu hoạch, điều chỉnh tồn nông sản, xuất kho mùa vụ, tạo/sửa giao dịch bán,
    /// ghi chi phí mùa vụ, ghi kết quả công việc).
    /// </summary>
    /// <param name="seasonId">Id mùa vụ cần kiểm tra.</param>
    /// <param name="outOfScopePermission">
    /// Quyền bổ sung cho phép thao tác trên mùa vụ ngoài phân công (BR-USR-13).
    /// Truyền <c>null</c> cho HAR, TSK; <see cref="ExtraPermission.ManageStock"/> cho SUP; <see cref="ExtraPermission.ManageFinance"/> cho SAL, EXP.
    /// </param>
    /// <param name="forUpdate">
    /// <c>true</c> nếu bên gọi cần khóa dòng seasons bằng <c>FOR UPDATE</c> trong giao dịch database (BR-GEN-12).
    /// </param>
    /// <param name="ct">Token hủy.</param>
    /// <returns>Thông tin tóm tắt của mùa vụ.</returns>
    /// <remarks>
    /// Tương đương gọi <see cref="EnsureStatusAsync"/> với <c>allowedStatuses = [SeasonStatus.InProgress]</c>.
    /// Lỗi: <c>NOT_FOUND</c>, <c>SEASON_INVALID_STATE</c>.
    /// </remarks>
    Task<SeasonStateResponse> EnsureInProgressAsync(
        Guid seasonId,
        ExtraPermission? outOfScopePermission,
        bool forUpdate,
        CancellationToken ct);
}
