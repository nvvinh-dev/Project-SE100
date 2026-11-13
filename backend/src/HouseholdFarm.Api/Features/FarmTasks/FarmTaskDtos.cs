using HouseholdFarm.Api.Common.Responses;
using HouseholdFarm.Api.Domain.Enums;

namespace HouseholdFarm.Api.Features.FarmTasks;

/// <summary>Một người có thể được giao công việc trong mùa vụ (BR-TSK-02).</summary>
public sealed record FarmTaskAssigneeOption(
    Guid UserId,
    string FullName,
    UserRole Role);

/// <summary>Vật tư dự kiến dùng cho một công việc (FR-TSK-01, FR-TSK-03).</summary>
public sealed record FarmTaskPlannedSupplyInput(
    Guid? SupplyId,
    decimal? PlannedQuantity);

/// <summary>Tạo một công việc canh tác đơn lẻ (FR-TSK-01, FR-SEA-05). Công việc mới luôn ở trạng thái Pending.</summary>
public sealed record CreateFarmTaskRequest(
    Guid SeasonId,
    FarmTaskType? Type,
    string? TypeOther,
    DateOnly? ScheduledDate,
    Guid? AssigneeId,
    int? PlannedDurationMinutes,
    string? Note,
    IReadOnlyList<FarmTaskPlannedSupplyInput>? Supplies);

/// <summary>Tạo nhóm công việc lặp theo chu kỳ cố định đến hết ngày dự kiến thu hoạch của mùa vụ (FR-TSK-02, BR-TSK-03).</summary>
public sealed record CreateRecurringFarmTasksRequest(
    Guid SeasonId,
    FarmTaskType? Type,
    string? TypeOther,
    DateOnly? FirstScheduledDate,
    int? IntervalDays,
    Guid? AssigneeId,
    int? PlannedDurationMinutes,
    string? Note,
    IReadOnlyList<FarmTaskPlannedSupplyInput>? Supplies);

/// <summary>Sửa công việc khi chưa thực hiện (FR-TSK-03, BR-TSK-01).</summary>
public sealed record UpdateFarmTaskRequest(
    FarmTaskType? Type,
    string? TypeOther,
    DateOnly? ScheduledDate,
    Guid? AssigneeId,
    int? PlannedDurationMinutes,
    string? Note,
    IReadOnlyList<FarmTaskPlannedSupplyInput>? Supplies,
    uint Version);

/// <summary>Hủy một công việc khi chưa thực hiện (FR-TSK-03, BR-TSK-01).</summary>
public sealed record CancelFarmTaskRequest(uint Version);

/// <summary>Vật tư thực tế sử dụng khi ghi kết quả công việc (FR-TSK-04, FR-TSK-05).</summary>
public sealed record FarmTaskActualSupplyInput(
    Guid? SupplyId,
    decimal? ActualQuantity);

/// <summary>Ghi hoặc bổ sung kết quả thực hiện công việc (FR-TSK-04..07, FR-TSK-10).</summary>
public sealed record RecordFarmTaskResultRequest(
    FarmTaskStatus? Status,
    DateOnly? PerformedDate,
    int? ActualDurationMinutes,
    string? AbnormalNote,
    string? FieldNote,
    IReadOnlyList<FarmTaskActualSupplyInput>? Supplies,
    string? Reason,
    uint Version);

/// <summary>Kết quả hủy nhóm công việc lặp (FR-TSK-11, BR-TSK-04).</summary>
public sealed record CancelRecurrenceGroupResponse(
    Guid RecurrenceGroupId,
    int CancelledCount);

/// <summary>Một dòng vật tư gắn với công việc dành cho Chủ hộ và Thành viên hộ.</summary>
public sealed record FarmTaskSupplyUsageItem(
    Guid Id,
    Guid SupplyId,
    string SupplyName,
    Unit Unit,
    decimal? PlannedQuantity,
    decimal? ActualQuantity,
    Guid? StockTransactionId,
    decimal CurrentStock,
    decimal MinStockLevel);

/// <summary>Chi tiết công việc canh tác dành cho Chủ hộ và Thành viên hộ.</summary>
public sealed record FarmTaskResponse(
    Guid Id,
    Guid SeasonId,
    Guid PlotId,
    string PlotCode,
    Guid CropId,
    string CropName,
    Guid? AssigneeId,
    string? AssigneeName,
    FarmTaskType Type,
    string? TypeOther,
    DateOnly ScheduledDate,
    int? PlannedDurationMinutes,
    string? Note,
    FarmTaskStatus Status,
    bool IsOverdue,
    DateOnly? PerformedDate,
    int? ActualDurationMinutes,
    string? AbnormalNote,
    string? FieldNote,
    DateTime? CompletedAt,
    DateTime? CancelledAt,
    Guid? RecurrenceGroupId,
    DateTime CreatedAt,
    Guid CreatedById,
    string? CreatedByName,
    DateTime? UpdatedAt,
    uint Version,
    IReadOnlyList<FarmTaskSupplyUsageItem> SupplyUsages);

/// <summary>Một dòng vật tư dự kiến ở chế độ Nhân công (BR-USR-14: không có tồn kho, đơn giá, tiền).</summary>
public sealed record WorkerFarmTaskSupplyItem(
    Guid Id,
    Guid SupplyId,
    string SupplyName,
    Unit Unit,
    decimal? PlannedQuantity);

/// <summary>Chi tiết công việc rút gọn dành cho Nhân công (FR-TSK-10, BR-USR-14).</summary>
public sealed record WorkerFarmTaskResponse(
    Guid Id,
    Guid SeasonId,
    Guid PlotId,
    string PlotCode,
    Guid CropId,
    string CropName,
    Guid? AssigneeId,
    string? AssigneeName,
    FarmTaskType Type,
    string? TypeOther,
    DateOnly ScheduledDate,
    int? PlannedDurationMinutes,
    string? Note,
    FarmTaskStatus Status,
    bool IsOverdue,
    DateOnly? PerformedDate,
    int? ActualDurationMinutes,
    string? AbnormalNote,
    string? FieldNote,
    DateTime? CompletedAt,
    DateTime? UpdatedAt,
    uint Version,
    IReadOnlyList<WorkerFarmTaskSupplyItem> SupplyUsages);

/// <summary>Một dòng của danh sách công việc canh tác (FR-TSK-09).</summary>
public sealed record FarmTaskListItem(
    Guid Id,
    Guid SeasonId,
    Guid PlotId,
    string PlotCode,
    Guid CropId,
    string CropName,
    FarmTaskType Type,
    string? TypeOther,
    DateOnly ScheduledDate,
    int? PlannedDurationMinutes,
    Guid? AssigneeId,
    string? AssigneeName,
    FarmTaskStatus Status,
    bool IsOverdue,
    DateOnly? PerformedDate,
    Guid? RecurrenceGroupId);

/// <summary>Tham số tìm kiếm, lọc danh sách công việc (FR-TSK-09).</summary>
public sealed record FarmTaskQuery : PageQuery
{
    public static readonly IReadOnlyList<string> SortFields = ["scheduledDate", "status", "createdAt"];

    public Guid? SeasonId { get; init; }

    public Guid? AssigneeId { get; init; }

    public FarmTaskStatus? Status { get; init; }

    public bool? IsOverdue { get; init; }

    public DateOnly? From { get; init; }

    public DateOnly? To { get; init; }

    /// <summary>Chỉ lấy công việc được giao cho chính người đang đăng nhập.</summary>
    public bool Mine { get; init; }
}
