using System.Text.Json.Serialization;
using HouseholdFarm.Api.Common.Responses;
using HouseholdFarm.Api.Domain.Enums;

namespace HouseholdFarm.Api.Features.Seasons;

/// <summary>
/// Loại thao tác trên dữ liệu mùa vụ, ánh xạ 1-1 với từng dòng của bảng quy tắc trạng thái (BR-SEA-10).
/// Dùng chung cho <see cref="ISeasonStateGuard"/> và API ô chọn mùa vụ.
/// </summary>
public enum SeasonOperation
{
    /// <summary>Sửa ngày, mục tiêu sản lượng (Planned, InProgress)</summary>
    EditScheduleAndYield,

    /// <summary>Đổi thửa, cây trồng (Planned)</summary>
    ChangePlotOrCrop,

    /// <summary>Khai báo, sửa, xóa vật tư dự kiến (Planned)</summary>
    ManagePlannedSupplies,

    /// <summary>Phân công, thu hồi phân công (Planned, InProgress)</summary>
    ManageAssignments,

    /// <summary>Tạo, sửa, hủy công việc (Planned, InProgress)</summary>
    ManageFarmTasks,

    /// <summary>Ghi kết quả công việc, vật tư đã dùng (InProgress)</summary>
    RecordFarmTaskResult,

    /// <summary>Xuất kho thủ công cho mùa vụ; hủy giao dịch xuất của mùa vụ (InProgress, ngoại lệ Kho: ManageStock)</summary>
    StockOut,

    /// <summary>Tạo, sửa, hủy phiếu thu hoạch (InProgress)</summary>
    ManageHarvests,

    /// <summary>Điều chỉnh tồn nông sản, hủy điều chỉnh (InProgress)</summary>
    AdjustProduceStock,

    /// <summary>Tạo, sửa, hủy giao dịch bán; liên kết phiếu thu (InProgress, ngoại lệ TC: ManageFinance)</summary>
    ManageSales,

    /// <summary>Ghi, hủy thanh toán (InProgress, Closed, ngoại lệ TC: ManageFinance)</summary>
    RecordPayment,

    /// <summary>Ghi, sửa, hủy chi phí gắn mùa vụ (InProgress, ngoại lệ TC: ManageFinance)</summary>
    ManageExpenses,

    /// <summary>Ghi chú kinh nghiệm (Planned, InProgress, Closed, Cancelled)</summary>
    ManageNotes,

    /// <summary>Tạo tóm tắt AI (Closed)</summary>
    GenerateAISummary,
}

/// <summary>
/// Yêu cầu tạo mùa vụ mới (FR-SEA-01). Mùa vụ mới luôn ở trạng thái Planned.
/// </summary>
/// <param name="PlotId">Thửa đất của mùa vụ.</param>
/// <param name="CropId">Cây trồng của mùa vụ.</param>
/// <param name="StartDate">Ngày bắt đầu mùa vụ.</param>
/// <param name="ExpectedHarvestDate">Ngày dự kiến thu hoạch (lớn hơn hoặc bằng ngày bắt đầu, BR-SEA-01).</param>
/// <param name="TargetYield">Mục tiêu sản lượng (lớn hơn 0, theo đơn vị của cây trồng, BR-SEA-01).</param>
public sealed record CreateSeasonRequest(
    Guid PlotId,
    Guid CropId,
    DateOnly StartDate,
    DateOnly ExpectedHarvestDate,
    decimal TargetYield);

/// <summary>
/// Yêu cầu sửa mùa vụ (FR-SEA-07, BR-GEN-11).
/// </summary>
/// <param name="PlotId">Thửa đất của mùa vụ.</param>
/// <param name="CropId">Cây trồng của mùa vụ.</param>
/// <param name="StartDate">Ngày bắt đầu mùa vụ.</param>
/// <param name="ExpectedHarvestDate">Ngày dự kiến thu hoạch (lớn hơn hoặc bằng ngày bắt đầu, BR-SEA-01).</param>
/// <param name="TargetYield">Mục tiêu sản lượng (lớn hơn 0, BR-SEA-01).</param>
/// <param name="Version">Phiên bản dữ liệu hiện tại để kiểm soát xung đột đồng thời (BR-GEN-11).</param>
public sealed record UpdateSeasonRequest(
    Guid PlotId,
    Guid CropId,
    DateOnly StartDate,
    DateOnly ExpectedHarvestDate,
    decimal TargetYield,
    uint Version);

/// <summary>
/// Thông tin mùa vụ trả về khi tạo hoặc xem chi tiết cơ bản (FR-SEA-01).
/// </summary>
public sealed record SeasonResponse(
    Guid Id,
    Guid PlotId,
    string PlotCode,
    Guid CropId,
    string CropName,
    Unit YieldUnit,
    DateOnly StartDate,
    DateOnly ExpectedHarvestDate,
    decimal TargetYield,
    SeasonStatus Status,
    uint Version);

/// <summary>
/// Thông tin trạng thái và ngày của mùa vụ do <see cref="ISeasonStateGuard"/> trả về.
/// Bên gọi (như Sales, Supplies) có thể sử dụng <c>StartDate</c> để kiểm tra ngày giao dịch không trước ngày bắt đầu mùa vụ (BR-SAL-01).
/// </summary>
/// <param name="Id">Id mùa vụ.</param>
/// <param name="Status">Trạng thái hiện tại của mùa vụ.</param>
/// <param name="StartDate">Ngày bắt đầu mùa vụ.</param>
/// <param name="ExpectedHarvestDate">Ngày dự kiến thu hoạch.</param>
/// <param name="PlotId">Thửa đất của mùa vụ.</param>
/// <param name="CropId">Cây trồng của mùa vụ.</param>
public record SeasonStateResponse(
    Guid Id,
    SeasonStatus Status,
    DateOnly StartDate,
    DateOnly ExpectedHarvestDate,
    Guid PlotId,
    Guid CropId);

/// <summary>
/// Một dòng trong danh sách mùa vụ (FR-SEA-11).
/// </summary>
public sealed record SeasonListItem(
    Guid Id,
    Guid PlotId,
    string PlotCode,
    Guid CropId,
    string CropName,
    Unit YieldUnit,
    DateOnly StartDate,
    DateOnly ExpectedHarvestDate,
    decimal TargetYield,
    SeasonStatus Status);

/// <summary>
/// Tham số truy vấn danh sách mùa vụ (FR-SEA-11).
/// </summary>
public sealed record SeasonQuery : PageQuery
{
    public static readonly IReadOnlyList<string> SortFields = ["startDate", "expectedHarvestDate", "targetYield", "createdAt"];

    public Guid? PlotId { get; init; }

    public Guid? CropId { get; init; }

    public SeasonStatus? Status { get; init; }

    public DateOnly? From { get; init; }

    public DateOnly? To { get; init; }
}

/// <summary>
/// Tham số truy vấn ô chọn mùa vụ theo loại thao tác (FR-SEA-11, BR-SEA-10) hoặc quyền xem (BR-USR-13).
/// </summary>
public sealed record SeasonOptionQuery(
    SeasonOperation? Operation = null,
    ExtraPermission? Permission = null);

/// <summary>
/// Mục lựa chọn mùa vụ cho component SeasonSelect dùng chung (FR-SEA-11).
/// </summary>
public sealed record SeasonOptionItem(
    Guid Id,
    string Label,
    SeasonStatus Status,
    Guid PlotId,
    string PlotCode,
    Guid CropId,
    string CropName,
    Unit YieldUnit,
    DateOnly StartDate,
    DateOnly ExpectedHarvestDate);

/// <summary>
/// Tóm tắt tồn nông sản theo loại chất lượng của mùa vụ (FR-SEA-12).
/// </summary>
public sealed record ProduceStockSummaryItem(
    QualityGrade Grade,
    decimal Quantity,
    Unit Unit);

/// <summary>
/// Tóm tắt tài chính của mùa vụ dành cho người có quyền Quản lý tài chính (FR-SEA-12, BR-USR-13).
/// </summary>
public sealed record SeasonFinancialSummary(
    decimal TotalExpense,
    decimal TotalRevenue,
    decimal EstimatedProfit);

/// <summary>
/// Người được phân công vào mùa vụ (FR-SEA-06, FR-SEA-12).
/// </summary>
public sealed record SeasonAssignmentResponse(
    Guid Id,
    Guid SeasonId,
    Guid UserId,
    string FullName,
    UserRole Role,
    string PhoneNumber,
    DateTime CreatedAt);

/// <summary>
/// Chi tiết mùa vụ đầy đủ (FR-SEA-12).
/// </summary>
public sealed record SeasonDetailResponse(
    Guid Id,
    Guid PlotId,
    string PlotCode,
    Guid CropId,
    string CropName,
    Unit YieldUnit,
    DateOnly StartDate,
    DateOnly ExpectedHarvestDate,
    decimal TargetYield,
    SeasonStatus Status,
    DateTime? StartedAt,
    DateTime? ClosedAt,
    DateTime? CancelledAt,
    string? CancelReason,
    uint Version,
    decimal HarvestedYield,
    int CompletedTasksCount,
    int TotalTasksCount,
    IReadOnlyList<ProduceStockSummaryItem> ProduceStocks,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    SeasonFinancialSummary? FinancialSummary,
    IReadOnlyList<SeasonAssignmentResponse> Assignments,
    bool IsOutOfScope,
    bool CanEdit,
    bool CanStart,
    bool CanClose,
    bool CanCancel,
    bool CanReopen);

/// <summary>
/// Yêu cầu phân công người vào mùa vụ (FR-SEA-06).
/// </summary>
public sealed record CreateSeasonAssignmentRequest(
    Guid SeasonId,
    Guid UserId);

/// <summary>
/// Yêu cầu thu hồi phân công người khỏi mùa vụ (FR-SEA-06, BR-USR-12, BR-GEN-14).
/// </summary>
public sealed record UnassignSeasonAssignmentRequest(
    bool Confirmed = false);

/// <summary>
/// Bản ghi ghi chú kinh nghiệm mùa vụ (FR-SEA-13).
/// </summary>
public sealed record SeasonNoteResponse(
    Guid Id,
    Guid SeasonId,
    string Content,
    Guid CreatedById,
    string AuthorName,
    DateTime CreatedAt,
    DateTime? UpdatedAt,
    uint Version,
    bool CanEdit);

/// <summary>
/// Yêu cầu tạo ghi chú kinh nghiệm mùa vụ (FR-SEA-13, BR-SEA-11).
/// </summary>
public sealed record CreateSeasonNoteRequest(
    Guid SeasonId,
    string Content);

/// <summary>
/// Yêu cầu sửa ghi chú kinh nghiệm mùa vụ (FR-SEA-13, BR-SEA-11).
/// </summary>
public sealed record UpdateSeasonNoteRequest(
    string Content,
    uint Version);

/// <summary>
/// Dòng kế hoạch vật tư dự kiến của mùa vụ (FR-SEA-04, BR-SEA-09).
/// </summary>
public sealed record SeasonSupplyPlanResponse(
    Guid Id,
    Guid SeasonId,
    Guid SupplyId,
    string SupplyName,
    Unit Unit,
    decimal PlannedQuantity,
    decimal CurrentStock,
    decimal MinStockLevel,
    string? WarningCode,
    uint Version);

/// <summary>
/// Yêu cầu khai báo vật tư dự kiến cho mùa vụ (FR-SEA-04, BR-SEA-10).
/// </summary>
public sealed record CreateSeasonSupplyPlanRequest(
    Guid SeasonId,
    Guid SupplyId,
    decimal PlannedQuantity);

/// <summary>
/// Yêu cầu cập nhật lượng vật tư dự kiến của mùa vụ (FR-SEA-04, BR-SEA-10).
/// </summary>
public sealed record UpdateSeasonSupplyPlanRequest(
    decimal PlannedQuantity,
    uint Version);

/// <summary>
/// Yêu cầu chốt mùa vụ (FR-SEA-09, BR-SEA-05, BR-GEN-14).
/// </summary>
public sealed record CloseSeasonRequest(
    bool? Confirmed);

/// <summary>
/// Yêu cầu hủy mùa vụ (FR-SEA-10, BR-SEA-06, BR-GEN-06, BR-GEN-14).
/// </summary>
public sealed record CancelSeasonRequest(
    string Reason,
    bool? Confirmed);

/// <summary>
/// Yêu cầu mở lại mùa vụ đã chốt (FR-SEA-14, BR-SEA-08, BR-GEN-06).
/// </summary>
public sealed record ReopenSeasonRequest(
    string Reason);

