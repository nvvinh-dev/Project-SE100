using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HouseholdFarm.Api.Features.Reports.Supplies;

[ApiController]
[Route("api/reports/supplies")]
[Authorize]
public sealed class SupplyReportsController(ISupplyReportService supplyReportService) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<SupplyReportResponse>> GetAsync([FromQuery] SupplyReportQuery query, CancellationToken ct) =>
        Ok(await supplyReportService.GetAsync(query, ct));

    // Trả file thẳng cho người dùng tải, không lưu file ở đâu (FR-RPT-06).
    [HttpPost("export")]
    [Produces(
        "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
        "application/pdf",
        "application/problem+json")]
    public async Task<IActionResult> ExportAsync(ExportSupplyReportRequest request, CancellationToken ct)
    {
        var file = await supplyReportService.ExportAsync(request, ct);
        return File(file.Content, file.ContentType, file.FileName);
    }
}
