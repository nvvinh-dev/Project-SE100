namespace HouseholdFarm.Api.Features.Ai;

/// <summary>Số lượt AI của hộ trong ngày hôm nay theo giờ Việt Nam (BR-AI-04).</summary>
public sealed record AIQuotaResponse(DateOnly UsageDate, int Used, int Limit, int Remaining);

/// <summary>Một cặp câu hỏi, câu trả lời trước đó trong phiên trò chuyện; hội thoại không lưu ở máy chủ.</summary>
public sealed record AssistantExchange(string Question, string Answer);

/// <param name="Question">Câu hỏi bằng tiếng Việt.</param>
/// <param name="History">Các lượt hỏi đáp trước trong phiên, cũ nhất trước; để trống khi bắt đầu phiên.</param>
public sealed record AskAssistantRequest(string Question, IReadOnlyList<AssistantExchange>? History);

/// <param name="Answer">Câu trả lời do AI tạo.</param>
/// <param name="Quota">Số lượt sau khi đã tính lượt này.</param>
public sealed record AskAssistantResponse(string Answer, AIQuotaResponse Quota);
