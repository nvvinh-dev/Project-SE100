using HouseholdFarm.Api.Domain.Enums;
using HouseholdFarm.Api.Features.Reports.Common;

namespace HouseholdFarm.Api.Features.Reports.Supplies;

/// <summary>
/// Bộ lọc của báo cáo vật tư (FR-RPT-01): khoảng thời gian theo <c>transaction_date</c> và nhóm vật tư.
/// Không lọc theo mùa vụ, thửa, cây trồng vì giao dịch nhập kho, điều chỉnh không gắn mùa vụ.
/// </summary>
public sealed record SupplyReportQuery
{
    public DateOnly? From { get; init; }

    public DateOnly? To { get; init; }

    public SupplyGroup? Group { get; init; }
}

/// <summary>Yêu cầu xuất báo cáo vật tư ra file, cùng bộ lọc với <see cref="SupplyReportQuery"/>.</summary>
public sealed record ExportSupplyReportRequest
{
    public DateOnly? From { get; init; }

    public DateOnly? To { get; init; }

    public SupplyGroup? Group { get; init; }

    // Khai báo nullable để thiếu trường trong JSON không âm thầm nhận giá trị 0 của enum.
    public ExportFormat? Format { get; init; }
}

/// <summary>Báo cáo vật tư.</summary>
/// <param name="LowStock">
/// Vật tư dưới ngưỡng tại thời điểm hiện tại, đặt đầu báo cáo (FR-RPT-SUP-02, BR-SUP-12); tồn âm xếp trước.
/// </param>
/// <param name="Groups">Nhập, xuất, tồn theo từng vật tư, gom theo nhóm vật tư (FR-RPT-SUP-01, BR-RPT-05).</param>
/// <param name="TotalStockValue">Tổng giá trị tồn hiện tại của mọi nhóm.</param>
/// <param name="Reconciliation">
/// Đối chiếu xuất kho với vật tư ghi trong công việc, theo vật tư và mùa vụ (FR-RPT-SUP-03, BR-RPT-06).
/// </param>
/// <param name="ManualOuts">Các lần xuất thủ công (không sinh từ công việc) trong kỳ, mới nhất trước.</param>
public sealed record SupplyReportResponse(
    IReadOnlyList<SupplyReportLowStockItem> LowStock,
    IReadOnlyList<SupplyReportGroupItem> Groups,
    decimal TotalStockValue,
    IReadOnlyList<SupplyReconciliationItem> Reconciliation,
    IReadOnlyList<SupplyManualOutItem> ManualOuts);

/// <param name="StockQuantity">Tồn hiện tại, có thể âm.</param>
public sealed record SupplyReportLowStockItem(
    Guid SupplyId,
    string Code,
    string Name,
    SupplyGroup Group,
    Unit Unit,
    decimal StockQuantity,
    decimal MinStockLevel);

/// <summary>
/// Một nhóm vật tư. Nhóm chỉ cộng giá trị, không cộng lượng vì các vật tư trong nhóm khác đơn vị (BR-RPT-05).
/// </summary>
public sealed record SupplyReportGroupItem(
    SupplyGroup Group,
    IReadOnlyList<SupplyReportRow> Supplies,
    decimal InValue,
    decimal OutValue,
    decimal AdjustmentValue,
    decimal StockValue);

/// <summary>Một vật tư trong báo cáo, lượng theo đơn vị của vật tư (BR-RPT-05).</summary>
/// <param name="OpeningQuantity">Tồn đầu kỳ: tổng lượng các giao dịch trước ngày đầu kỳ.</param>
/// <param name="InQuantity">Lượng nhập trong kỳ.</param>
/// <param name="InValue">Giá trị nhập trong kỳ (tổng <c>total_cost</c>).</param>
/// <param name="OutQuantity">Lượng xuất trong kỳ, số dương.</param>
/// <param name="OutValue">Giá trị xuất trong kỳ.</param>
/// <param name="AdjustmentQuantity">Lượng điều chỉnh trong kỳ, có dấu.</param>
/// <param name="AdjustmentValue">Giá trị điều chỉnh trong kỳ, cùng dấu với lượng điều chỉnh.</param>
/// <param name="ClosingQuantity">
/// Tồn cuối kỳ = đầu kỳ + nhập − xuất + điều chỉnh; có thể âm khi giao dịch được ghi lùi ngày.
/// </param>
/// <param name="StockQuantity">Tồn hiện tại.</param>
/// <param name="AverageUnitCost">Giá vốn bình quân hiện tại.</param>
/// <param name="StockValue">Giá trị tồn = tồn hiện tại × giá vốn bình quân hiện tại, chỉ tính cho thời điểm hiện tại.</param>
public sealed record SupplyReportRow(
    Guid SupplyId,
    string Code,
    string Name,
    Unit Unit,
    RecordStatus Status,
    decimal OpeningQuantity,
    decimal InQuantity,
    decimal InValue,
    decimal OutQuantity,
    decimal OutValue,
    decimal AdjustmentQuantity,
    decimal AdjustmentValue,
    decimal ClosingQuantity,
    decimal StockQuantity,
    decimal AverageUnitCost,
    decimal StockValue);

/// <summary>Đối chiếu xuất kho của một vật tư cho một mùa vụ trong kỳ (BR-RPT-06).</summary>
/// <param name="TotalOutQuantity">Tổng xuất = xuất sinh từ công việc + xuất thủ công.</param>
/// <param name="TaskOutQuantity">Lượng của các giao dịch xuất sinh từ công việc.</param>
/// <param name="ManualOutQuantity">Lượng của các lần xuất thủ công.</param>
/// <param name="RecordedInTasksQuantity">Tổng lượng thực dùng của các dòng vật tư công việc gắn các giao dịch xuất đó.</param>
/// <param name="DifferenceQuantity">Tổng xuất − lượng ghi trong công việc: phần xuất ra không ghi vào công việc nào.</param>
public sealed record SupplyReconciliationItem(
    Guid SupplyId,
    string SupplyCode,
    string SupplyName,
    Unit Unit,
    ReportSeasonSummaryDto Season,
    decimal TotalOutQuantity,
    decimal TaskOutQuantity,
    decimal ManualOutQuantity,
    decimal RecordedInTasksQuantity,
    decimal DifferenceQuantity);

/// <summary>Một lần xuất thủ công cho mùa vụ (BR-RPT-06).</summary>
/// <param name="Quantity">Lượng xuất, số dương.</param>
/// <param name="CreatedByName">Người ghi giao dịch.</param>
public sealed record SupplyManualOutItem(
    Guid StockTransactionId,
    DateOnly TransactionDate,
    Guid SupplyId,
    string SupplyCode,
    string SupplyName,
    Unit Unit,
    ReportSeasonSummaryDto Season,
    decimal Quantity,
    string CreatedByName,
    string? Note);
