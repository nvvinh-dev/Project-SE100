using HouseholdFarm.Api.Features.Reports.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HouseholdFarm.Api.Features.Reports.FarmTasks;

[ApiController]
[Route("api/reports/farm-tasks")]
[Authorize]
public sealed class FarmTaskReportsController(IFarmTaskReportService farmTaskReportService) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<FarmTaskReportResponse>> GetAsync([FromQuery] FarmTaskReportQuery query, CancellationToken ct) =>
        Ok(await farmTaskReportService.GetAsync(query, ct));

    [HttpPost("export")]
    [Produces("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "application/pdf", "application/problem+json")]
    public async Task<IActionResult> ExportAsync(ExportFarmTaskReportRequest request, CancellationToken ct)
    {
        var file = await farmTaskReportService.ExportAsync(request, ct);
        return File(file.Content, file.ContentType, file.FileName);
    }
}
