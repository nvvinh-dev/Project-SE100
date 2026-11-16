using HouseholdFarm.Api.Features.Reports.Common;

namespace HouseholdFarm.Api.Features.Reports.FarmTasks;

/// <summary>Báo cáo công việc canh tác (FR-RPT-TASK-01, FR-RPT-TASK-02).</summary>
public interface IFarmTaskReportService
{
    Task<FarmTaskReportResponse> GetAsync(FarmTaskReportQuery query, CancellationToken ct);

    Task<ExportedFileResult> ExportAsync(ExportFarmTaskReportRequest request, CancellationToken ct);
}
