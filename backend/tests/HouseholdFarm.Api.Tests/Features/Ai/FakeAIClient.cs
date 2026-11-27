using HouseholdFarm.Api.Common.Errors;
using HouseholdFarm.Api.Features.Ai;

namespace HouseholdFarm.Api.Tests.Features.Ai;

/// <summary>Bản giả lập dịch vụ AI: trả lần lượt các câu trả lời đã xếp, ghi lại mọi yêu cầu nhận được. Không gọi Gemini.</summary>
public sealed class FakeAIClient : IAIClient
{
    private readonly Queue<Func<AIRequest, AIReply>> _replies = new();

    private readonly List<AIRequest> _requests = [];

    public IReadOnlyList<AIRequest> Requests => _requests;

    public FakeAIClient ThenText(string text) => Then(_ => new AIReply(text, []));

    public FakeAIClient ThenToolCalls(params AIToolCall[] calls) => Then(_ => new AIReply(null, calls));

    public FakeAIClient ThenUnavailable() => Then(_ => throw new AppException(ErrorCodes.AIUnavailable, "Dịch vụ AI giả lập lỗi."));

    public FakeAIClient Then(Func<AIRequest, AIReply> reply)
    {
        _replies.Enqueue(reply);
        return this;
    }

    public Task<AIReply> GenerateAsync(AIRequest request, CancellationToken ct)
    {
        // Chụp lại danh sách lượt vì service tiếp tục thêm lượt vào cùng danh sách sau lần gọi này.
        var snapshot = request with { Turns = request.Turns.ToList() };
        _requests.Add(snapshot);
        if (!_replies.TryDequeue(out var reply))
        {
            throw new InvalidOperationException("Bản giả lập không còn câu trả lời nào được xếp sẵn.");
        }

        return Task.FromResult(reply(snapshot));
    }
}
