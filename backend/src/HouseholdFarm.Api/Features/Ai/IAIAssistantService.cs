namespace HouseholdFarm.Api.Features.Ai;

/// <summary>Trợ lý hỏi đáp dữ liệu của hộ (FR-AI-02). Hội thoại không lưu ở máy chủ.</summary>
public interface IAIAssistantService
{
    /// <summary>
    /// Trả lời câu hỏi bằng dữ liệu lấy qua công cụ AI với danh tính người hỏi (BR-AI-02, BR-AI-09). Một câu hỏi là một lượt
    /// dù gọi nhiều công cụ (BR-AI-04). Chỉ Chủ hộ, Thành viên hộ (BR-AI-01).
    /// Lỗi: <c>FORBIDDEN</c>, <c>AI_QUOTA_EXCEEDED</c>, <c>AI_UNAVAILABLE</c>.
    /// </summary>
    Task<AskAssistantResponse> AskAsync(AskAssistantRequest request, CancellationToken ct);
}
