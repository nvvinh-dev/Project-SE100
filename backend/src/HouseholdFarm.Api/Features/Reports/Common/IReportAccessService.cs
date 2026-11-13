using HouseholdFarm.Api.Common.Auth;
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

public sealed class ReportAccessService(ICurrentUser currentUser) : IReportAccessService
{
    public void EnsureCanViewReport(ReportType reportType)
    {
        switch (reportType)
        {
            case ReportType.Production:
            case ReportType.FarmTask:
                currentUser.EnsureNotWorker();
                break;
            case ReportType.Supply:
                currentUser.EnsureCanManageStock();
                break;
            case ReportType.Finance:
                currentUser.EnsureCanManageFinance();
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(reportType), reportType, null);
        }
    }

    // BR-RPT-04: Thành viên không có quyền Quản lý tài chính thì ẩn cột tài chính ngay ở backend.
    public bool CanViewFinancialColumns() => currentUser.HasPermission(ExtraPermission.ManageFinance);
}
