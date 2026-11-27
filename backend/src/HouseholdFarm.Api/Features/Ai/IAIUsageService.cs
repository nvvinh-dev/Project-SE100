using HouseholdFarm.Api.Domain.Enums;

namespace HouseholdFarm.Api.Features.Ai;

/// <summary>
/// Giới hạn lượt AI theo hộ, theo ngày giờ Việt Nam (BR-AI-04, D-19), dùng chung cho mọi tính năng AI.
/// Một lượt là một yêu cầu của người dùng, không đếm từng lần gọi công cụ bên trong.
/// </summary>
public interface IAIUsageService
{
    /// <summary>Số lượt đã dùng và còn lại hôm nay. Chỉ Chủ hộ, Thành viên hộ (BR-AI-01). Lỗi: <c>FORBIDDEN</c>.</summary>
    Task<AIQuotaResponse> GetQuotaAsync(CancellationToken ct);

    /// <summary>
    /// Gọi trước khi gửi yêu cầu cho dịch vụ AI. Không khóa dòng: vài yêu cầu cùng lúc có thể vượt nhẹ, chấp nhận được.
    /// Lỗi: <c>FORBIDDEN</c>, <c>AI_QUOTA_EXCEEDED</c>.
    /// </summary>
    Task EnsureAvailableAsync(CancellationToken ct);

    /// <summary>
    /// Ghi một lượt khi dịch vụ AI đã trả kết quả; lượt lỗi do phía dịch vụ AI thì không gọi hàm này (BR-AI-08).
    /// Trả số lượt sau khi ghi.
    /// </summary>
    Task<AIQuotaResponse> RecordAsync(AIFeature feature, CancellationToken ct);
}
