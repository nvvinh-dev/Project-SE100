using HouseholdFarm.Api.Domain.Enums;

namespace HouseholdFarm.Api.Features.Reports.Common;

/// <summary>
/// Kiểm tra quyền xem báo cáo và quyền thấy số liệu tài chính theo ma trận phân quyền (FR-RPT-02, BR-RPT-04).
/// </summary>
public interface IReportAccessService
{
    void EnsureCanViewReport(ReportType reportType);

    bool CanViewFinancialColumns();
}
