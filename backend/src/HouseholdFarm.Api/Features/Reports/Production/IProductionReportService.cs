using HouseholdFarm.Api.Features.Reports.Common;

namespace HouseholdFarm.Api.Features.Reports.Production;

/// <summary>Báo cáo sản xuất (FR-RPT-PROD-01..04).</summary>
public interface IProductionReportService
{
    Task<ProductionReportResponse> GetAsync(ProductionReportQuery query, CancellationToken ct);

    Task<ExportedFileResult> ExportAsync(ExportProductionReportRequest request, CancellationToken ct);
}
