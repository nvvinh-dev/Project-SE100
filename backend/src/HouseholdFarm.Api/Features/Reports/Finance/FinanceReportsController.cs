using HouseholdFarm.Api.Features.Reports.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HouseholdFarm.Api.Features.Reports.Finance;

[ApiController]
[Route("api/reports/finance")]
[Authorize]
public sealed class FinanceReportsController(IFinanceReportService financeReportService) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<FinanceReportResponse>> GetAsync([FromQuery] FinanceReportQuery query, CancellationToken ct) =>
        Ok(await financeReportService.GetAsync(query, ct));

    // Trả file thẳng cho người dùng tải, không lưu file ở đâu (FR-RPT-06).
    [HttpPost("export")]
    [Produces("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "application/problem+json")]
    public async Task<IActionResult> ExportAsync(ExportReportRequest request, CancellationToken ct)
    {
        var file = await financeReportService.ExportAsync(request, ct);
        return File(file.Content, file.ContentType, file.FileName);
    }
}
