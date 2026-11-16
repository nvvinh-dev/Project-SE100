using HouseholdFarm.Api.Features.Reports.Common;

namespace HouseholdFarm.Api.Features.Reports.Finance;

/// <summary>Báo cáo chi phí - doanh thu, dòng tiền (FR-RPT-FIN-01, FR-RPT-FIN-03, FR-RPT-FIN-04, FR-RPT-FIN-06).</summary>
public interface IFinanceReportService
{
    /// <summary>Xem báo cáo theo bộ lọc. Chỉ Chủ hộ và Thành viên có quyền Quản lý tài chính, còn lại <c>FORBIDDEN</c>.</summary>
    Task<FinanceReportResponse> GetAsync(FinanceReportQuery query, CancellationToken ct);

    /// <summary>Xuất báo cáo ra file theo bộ lọc, lưu lần xuất và ghi nhật ký (FR-RPT-04, FR-RPT-06).</summary>
    Task<ExportedFileResult> ExportAsync(ExportReportRequest request, CancellationToken ct);
}
