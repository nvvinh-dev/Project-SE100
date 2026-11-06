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
    ManageTasks,

    /// <summary>Ghi kết quả công việc, vật tư đã dùng (InProgress)</summary>
    RecordTaskExecution,

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
    GenerateAiSummary,
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


