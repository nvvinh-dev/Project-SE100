using System.Text.Json.Nodes;

namespace HouseholdFarm.Api.Features.Ai;

/// <summary>
/// Dịch vụ AI của nhóm (D-44). Service nghiệp vụ chỉ phụ thuộc interface này, không gọi SDK của nhà cung cấp trực tiếp;
/// đổi nhà cung cấp chỉ cần thêm bản hiện thực mới. Test thay bằng bản giả lập.
/// </summary>
public interface IAIClient
{
    /// <summary>
    /// Gửi hội thoại cho model, nhận câu trả lời hoặc các yêu cầu gọi công cụ.
    /// Dịch vụ AI lỗi, quá thời gian chờ hoặc chưa cấu hình thì ném <c>AI_UNAVAILABLE</c> (BR-AI-08).
    /// </summary>
    Task<AIReply> GenerateAsync(AIRequest request, CancellationToken ct);
}

/// <param name="SystemInstruction">Chỉ dẫn cho model, không chứa dữ liệu cá nhân (BR-AI-07).</param>
/// <param name="Turns">Các lượt hội thoại theo thứ tự thời gian.</param>
/// <param name="Tools">Công cụ model được yêu cầu gọi; rỗng thì model phải trả lời bằng chữ.</param>
public sealed record AIRequest(string SystemInstruction, IReadOnlyList<AITurn> Turns, IReadOnlyList<AIToolDefinition> Tools);

/// <summary>Một lượt hội thoại gửi cho model.</summary>
public abstract record AITurn;

/// <summary>Câu người dùng viết.</summary>
public sealed record AIUserTurn(string Text) : AITurn;

/// <summary>
/// Lượt của model. <paramref name="ProviderState"/> là dữ liệu riêng của nhà cung cấp (ví dụ chữ ký suy luận),
/// phải gửi lại nguyên vẹn ở lần gọi sau; lượt dựng lại từ lịch sử phía giao diện thì để trống.
/// </summary>
public sealed record AIModelTurn(string? Text, IReadOnlyList<AIToolCall> ToolCalls, object? ProviderState = null) : AITurn;

/// <summary>Kết quả các công cụ model vừa yêu cầu gọi.</summary>
public sealed record AIToolResultsTurn(IReadOnlyList<AIToolResult> Results) : AITurn;

/// <param name="ParametersSchema">JSON Schema của tham số.</param>
public sealed record AIToolDefinition(string Name, string Description, JsonObject ParametersSchema);

public sealed record AIToolCall(string? Id, string Name, JsonObject Arguments);

public sealed record AIToolResult(string? Id, string Name, JsonNode Output);

/// <summary>Kết quả một lần gọi model: câu trả lời, hoặc danh sách công cụ cần gọi.</summary>
public sealed record AIReply(string? Text, IReadOnlyList<AIToolCall> ToolCalls, object? ProviderState = null);
