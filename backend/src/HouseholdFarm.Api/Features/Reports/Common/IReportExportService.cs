using HouseholdFarm.Api.Domain.Enums;

namespace HouseholdFarm.Api.Features.Reports.Common;

/// <summary>
/// Dịch vụ xuất báo cáo dùng chung: kiểm tra quyền, tạo tệp Excel/PDF, lưu <c>report_exports</c> và ghi nhật ký
/// <c>Report.Export</c> trong cùng giao dịch database (FR-RPT-04, FR-RPT-06, BR-RPT-09).
/// </summary>
public interface IReportExportService
{
    Task<ExportedFileResult> ExportAsync(
        ReportType reportType,
        ExportFormat format,
        ReportFilter filter,
        object fullFilterPayload,
        ReportExportDocument document,
        CancellationToken ct);
}
