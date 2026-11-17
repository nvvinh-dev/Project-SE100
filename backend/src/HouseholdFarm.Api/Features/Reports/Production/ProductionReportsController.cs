using HouseholdFarm.Api.Features.Reports.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HouseholdFarm.Api.Features.Reports.Production;

[ApiController]
[Route("api/reports/production")]
[Authorize]
public sealed class ProductionReportsController(IProductionReportService productionReportService) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<ProductionReportResponse>> GetAsync([FromQuery] ProductionReportQuery query, CancellationToken ct) =>
        Ok(await productionReportService.GetAsync(query, ct));

    [HttpPost("export")]
    [Produces("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "application/pdf", "application/problem+json")]
    public async Task<IActionResult> ExportAsync(ExportProductionReportRequest request, CancellationToken ct)
    {
        var file = await productionReportService.ExportAsync(request, ct);
        return File(file.Content, file.ContentType, file.FileName);
    }
}
