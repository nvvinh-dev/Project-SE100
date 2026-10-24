using HouseholdFarm.Api.Domain.Enums;

namespace HouseholdFarm.Api.Features.Seasons;

/// <summary>
/// Điểm nối kiểm tra và bảo vệ trạng thái mùa vụ cho các module khác (SUP, TSK, HAR, SAL, EXP):
/// kiểm tra mùa vụ tồn tại trong hộ, nằm trong phạm vi người dùng (gồm ngoại lệ quyền Kho, TC ở BR-USR-13),
/// và trạng thái cho phép thực hiện thao tác theo bảng quy tắc trạng thái (mục 4.3).
/// Hỗ trợ khóa dòng seasons (SELECT ... FOR UPDATE) theo BR-GEN-12 khi bên gọi thay đổi dữ liệu.
/// </summary>
public interface ISeasonStateGuard
{
    /// <summary>
    /// Kiểm tra mùa vụ tồn tại, thuộc phạm vi người dùng và ở một trong các trạng thái được phép.
    /// Tùy chọn khóa dòng seasons (<c>FOR UPDATE</c>) theo BR-GEN-12.
    /// </summary>
    /// <param name="seasonId">Id mùa vụ cần kiểm tra.</param>
    /// <param name="allowedStatuses">Danh sách trạng thái cho phép thao tác.</param>
    /// <param name="forUpdate">
    /// <c>true</c> nếu bên gọi cần khóa dòng seasons bằng <c>FOR UPDATE</c> trong giao dịch database (BR-GEN-12)
    /// trước khi ghi dữ liệu hoặc kiểm tra tồn/công nợ; <c>false</c> nếu chỉ đọc.
    /// </param>
    /// <param name="ct">Token hủy.</param>
    /// <returns>Thông tin tóm tắt của mùa vụ (Id, Status, StartDate, PlotId, CropId...) để bên gọi tái sử dụng nếu cần.</returns>
    /// <remarks>
    /// <para>
    /// Kiểm tra phạm vi làm việc: dùng hàm lọc phạm vi chung (<c>VisibleTo</c>), áp dụng ngoại lệ BR-USR-13
    /// cho người dùng có quyền Quản lý kho (<c>ManageStock</c>) hoặc Quản lý tài chính (<c>ManageFinance</c>)
    /// được thao tác trên mùa vụ ngoài phân công.
    /// </para>
    /// <para>
    /// Tham gia giao dịch database của bên gọi: khi <paramref name="forUpdate"/> là <c>true</c>, bên gọi
    /// bắt buộc phải đang mở giao dịch database (<c>BeginTransactionAsync</c>). Hàm sẽ thực hiện khóa dòng
    /// <c>seasons</c> trước mọi bảng khác (BR-GEN-12) và không tự commit.
    /// </para>
    /// <para>
    /// Lỗi: <c>NOT_FOUND</c> khi mùa vụ không tồn tại trong hộ hoặc nằm ngoài phạm vi người dùng (BR-GEN-02);
    /// <c>SEASON_INVALID_STATE</c> khi trạng thái mùa vụ không nằm trong <paramref name="allowedStatuses"/>.
    /// </para>
    /// </remarks>
    Task<SeasonStateResponse> EnsureStatusAsync(
        Guid seasonId,
        IReadOnlyCollection<SeasonStatus> allowedStatuses,
        bool forUpdate = false,
        CancellationToken ct = default);

    /// <summary>
    /// Kiểm tra mùa vụ bắt buộc đang ở trạng thái <c>InProgress</c> (Đang canh tác).
    /// Đây là điều kiện tiên quyết cho hầu hết các thao tác ghi dữ liệu sản xuất, kho, bán hàng và chi phí
    /// (tạo/sửa phiếu thu hoạch, điều chỉnh tồn nông sản, xuất kho mùa vụ, tạo/sửa giao dịch bán,
    /// ghi chi phí mùa vụ, ghi kết quả công việc).
    /// </summary>
    /// <param name="seasonId">Id mùa vụ cần kiểm tra.</param>
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
        bool forUpdate = false,
        CancellationToken ct = default);
}

