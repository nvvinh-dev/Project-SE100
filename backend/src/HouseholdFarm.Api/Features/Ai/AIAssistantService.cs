using HouseholdFarm.Api.Common.Auth;
using HouseholdFarm.Api.Common.Errors;
using HouseholdFarm.Api.Common.Time;
using HouseholdFarm.Api.Domain.Enums;

namespace HouseholdFarm.Api.Features.Ai;

public sealed class AIAssistantService(
    IAIClient aiClient,
    IAIUsageService usage,
    AssistantToolbox toolbox,
    ICurrentUser currentUser,
    IAppClock clock,
    ILogger<AIAssistantService> logger) : IAIAssistantService
{
    // Số vòng model được gọi công cụ trước khi buộc phải trả lời, để một câu hỏi không gọi dịch vụ AI quá nhiều lần.
    private const int MaxToolRounds = 4;

    // Số công cụ tối đa trong một vòng; model yêu cầu nhiều hơn thì chỉ chạy chừng này.
    private const int MaxToolCallsPerRound = 6;

    public async Task<AskAssistantResponse> AskAsync(AskAssistantRequest request, CancellationToken ct)
    {
        // BR-AI-01 trước, rồi mới kiểm tra lượt (BR-AI-04).
        currentUser.EnsureNotWorker();
        await usage.EnsureAvailableAsync(ct);

        var turns = new List<AITurn>();
        foreach (var exchange in request.History ?? [])
        {
            turns.Add(new AIUserTurn(exchange.Question));
            turns.Add(new AIModelTurn(exchange.Answer, []));
        }

        turns.Add(new AIUserTurn(request.Question.Trim()));

        var instruction = SystemInstruction(clock.Today);
        string? answer = null;
        for (var round = 0; round <= MaxToolRounds && answer is null; round++)
        {
            // Vòng cuối không đưa công cụ để model trả lời bằng dữ liệu đã có.
            var tools = round < MaxToolRounds ? AssistantToolbox.Definitions : [];
            var reply = await aiClient.GenerateAsync(new AIRequest(instruction, turns, tools), ct);

            if (reply.ToolCalls.Count == 0 || tools.Count == 0)
            {
                answer = reply.Text;
                break;
            }

            turns.Add(new AIModelTurn(reply.Text, reply.ToolCalls, reply.ProviderState));
            var results = new List<AIToolResult>();
            foreach (var call in reply.ToolCalls.Take(MaxToolCallsPerRound))
            {
                // Chỉ ghi tên công cụ, không ghi tham số hay câu hỏi (NFR-13).
                logger.LogInformation("Trợ lý AI gọi công cụ {ToolName}", call.Name);
                results.Add(new AIToolResult(call.Id, call.Name, await toolbox.InvokeAsync(call, ct)));
            }

            turns.Add(new AIToolResultsTurn(results));
        }

        if (string.IsNullOrWhiteSpace(answer))
        {
            throw new AppException(
                ErrorCodes.AIUnavailable,
                "Trợ lý AI chưa trả lời được câu này. Thử hỏi lại ngắn gọn hơn; các chức năng khác vẫn dùng bình thường.");
        }

        answer = answer.Trim();
        if (answer.Length > AskAssistantRequestValidator.AnswerMaxLength)
        {
            answer = answer[..AskAssistantRequestValidator.AnswerMaxLength];
        }

        // BR-AI-04: tính một lượt khi dịch vụ AI đã trả kết quả, dù đã gọi nhiều công cụ.
        var quota = await usage.RecordAsync(AIFeature.Assistant, ct);
        return new AskAssistantResponse(answer, quota);
    }

    // Không đưa tên hộ, số điện thoại, địa chỉ hay thông tin liên hệ vào chỉ dẫn (BR-AI-07).
    private static string SystemInstruction(DateOnly today) => $"""
        Bạn là trợ lý hỏi đáp dữ liệu của một hộ gia đình làm nông nghiệp. Hôm nay là {today:yyyy-MM-dd} (giờ Việt Nam).
        Quy tắc:
        1. Chỉ trả lời bằng dữ liệu lấy từ các công cụ được cung cấp. Không bịa số liệu, không ước đoán.
        2. Cần số liệu thì gọi công cụ; câu hỏi nhắc tới mùa vụ, cây trồng, thửa thì dùng list_seasons để tìm seasonId trước.
        3. Công cụ trả về rỗng thì nói rõ là chưa có dữ liệu đó.
        4. Công cụ trả về error FORBIDDEN thì trả lời là người hỏi không có quyền xem thông tin này, không nêu bất kỳ con số nào về nó.
        5. Từ chối lịch sự các câu hỏi ngoài dữ liệu đã ghi nhận của hộ: chẩn đoán bệnh cây, sâu bệnh; dự báo năng suất, sản lượng,
           giá cả; kết luận nguyên nhân tăng giảm năng suất; kiến thức chung không liên quan tới dữ liệu của hộ.
        6. Chỉ đọc dữ liệu, không tạo, sửa hay xóa gì. Người hỏi muốn ghi dữ liệu thì hướng dẫn dùng chức năng tương ứng của ứng dụng.
        7. Trả lời bằng tiếng Việt, ngắn gọn, đi thẳng vào số liệu, xưng hô trung tính (không dùng "bạn"). Tiền ghi theo đồng (₫), ngày ghi dạng dd/MM/yyyy.
           Không hiển thị id. Lợi nhuận của mùa vụ chưa chốt là tạm tính.
        """;
}
