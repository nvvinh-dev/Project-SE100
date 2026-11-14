using HouseholdFarm.Api.Common.Responses;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HouseholdFarm.Api.Features.AuditLogs;

// FR-AUD-04: chỉ có API đọc; không có PUT, correct, DELETE cho nhật ký. Kiểm tra quyền nằm trong service.
[ApiController]
[Route("api/audit-logs")]
[Authorize]
public sealed class AuditLogsController(IAuditLogService auditLogService) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<PagedResult<AuditLogListItem>>> GetListAsync(
        [FromQuery] AuditLogQuery query,
        CancellationToken ct) =>
        Ok(await auditLogService.GetListAsync(query, ct));
}
