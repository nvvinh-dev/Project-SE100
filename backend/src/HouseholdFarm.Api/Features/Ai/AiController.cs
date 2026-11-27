using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HouseholdFarm.Api.Features.Ai;

// Kiểm tra quyền, giới hạn lượt nằm trong service (BR-AI-01, BR-AI-04).
[ApiController]
[Route("api/ai")]
[Authorize]
public sealed class AiController(IAIUsageService usageService, IAIAssistantService assistantService) : ControllerBase
{
    /// <summary>Số lượt AI đã dùng và còn lại hôm nay của hộ.</summary>
    [HttpGet("quota")]
    public async Task<ActionResult<AIQuotaResponse>> GetQuotaAsync(CancellationToken ct) =>
        Ok(await usageService.GetQuotaAsync(ct));

    /// <summary>Hỏi trợ lý AI về dữ liệu của hộ (FR-AI-02).</summary>
    [HttpPost("assistant")]
    public async Task<ActionResult<AskAssistantResponse>> AskAssistantAsync(AskAssistantRequest request, CancellationToken ct) =>
        Ok(await assistantService.AskAsync(request, ct));
}
