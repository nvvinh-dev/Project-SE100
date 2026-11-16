using HouseholdFarm.Api.Features.Reports.Common;

namespace HouseholdFarm.Api.Features.Reports.FarmTasks;

/// <summary>Kiểu nhóm số liệu của báo cáo công việc (FR-RPT-TASK-01).</summary>
public enum FarmTaskReportGroupBy
{
    Time,
    Assignee,
}

/// <summary>Bộ lọc báo cáo công việc (FR-RPT-01, FR-RPT-TASK-01).</summary>
public sealed record FarmTaskReportQuery : ReportFilter
{
    public FarmTaskReportGroupBy GroupBy { get; init; } = FarmTaskReportGroupBy.Time;
}

/// <summary>Yêu cầu xuất báo cáo công việc (FR-RPT-04, FR-RPT-05, FR-RPT-06).</summary>
public sealed record ExportFarmTaskReportRequest : ExportReportRequest
{
    public FarmTaskReportGroupBy GroupBy { get; init; } = FarmTaskReportGroupBy.Time;
}

/// <summary>
/// Số liệu tổng quan của báo cáo công việc (FR-RPT-TASK-01).
/// Việc đã hủy tách riêng khỏi tổng đã lập để bốn chỉ số điều phối nhất quán.
/// </summary>
public sealed record FarmTaskReportOverviewDto(
    int PlannedCount,
    int CompletedCount,
    int OverdueCount,
    int PendingCount,
    int CancelledCount);

/// <summary>
/// Một dòng thống kê công việc. Theo BR-RPT-08, không chứa trường xếp hạng hay chấm điểm cá nhân.
/// </summary>
public sealed record FarmTaskReportRowDto(
    string Key,
    string Label,
    bool IsUnassigned,
    int PlannedCount,
    int CompletedCount,
    int OverdueCount,
    int PendingCount);

public sealed record FarmTaskReportResponse(
    FarmTaskReportGroupBy GroupBy,
    FarmTaskReportOverviewDto Overview,
    IReadOnlyList<FarmTaskReportRowDto> Rows);
