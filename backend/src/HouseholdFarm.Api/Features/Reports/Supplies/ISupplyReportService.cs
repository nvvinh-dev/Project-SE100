using HouseholdFarm.Api.Features.Reports.Common;

namespace HouseholdFarm.Api.Features.Reports.Supplies;

/// <summary>Báo cáo vật tư (FR-RPT-SUP-01, FR-RPT-SUP-02, FR-RPT-SUP-03).</summary>
public interface ISupplyReportService
{
    /// <summary>
    /// Xem báo cáo theo khoảng thời gian và nhóm vật tư. Chỉ Chủ hộ và Thành viên có quyền Quản lý kho,
    /// còn lại <c>FORBIDDEN</c>.
    /// </summary>
    Task<SupplyReportResponse> GetAsync(SupplyReportQuery query, CancellationToken ct);

    /// <summary>Xuất báo cáo ra file theo bộ lọc, lưu lần xuất và ghi nhật ký (FR-RPT-04, FR-RPT-05, FR-RPT-06).</summary>
    Task<ExportedFileResult> ExportAsync(ExportSupplyReportRequest request, CancellationToken ct);
}
