using System.Text.Json;
using System.Text.Json.Nodes;
using Google.GenAI;
using Google.GenAI.Types;
using HouseholdFarm.Api.Common.Errors;
using Microsoft.Extensions.Options;

namespace HouseholdFarm.Api.Features.Ai;

/// <summary>Bản hiện thực <see cref="IAIClient"/> cho Gemini qua SDK <c>Google.GenAI</c> (D-18).</summary>
public sealed class GeminiAIClient(IOptions<AIOptions> options, ILogger<GeminiAIClient> logger) : IAIClient, IDisposable
{
    // Quá thời gian này thì coi như dịch vụ AI không phản hồi (BR-AI-08), để người dùng không chờ mãi.
    private static readonly TimeSpan _timeout = TimeSpan.FromSeconds(30);

    private readonly Lazy<Client> _client = new(() => new Client(
        apiKey: options.Value.ApiKey,
        httpOptions: new HttpOptions { Timeout = (int)_timeout.TotalMilliseconds }));

    public async Task<AIReply> GenerateAsync(AIRequest request, CancellationToken ct)
    {
        var settings = options.Value;
        if (string.IsNullOrWhiteSpace(settings.ApiKey) || string.IsNullOrWhiteSpace(settings.Model))
        {
            logger.LogWarning("Chưa cấu hình Ai:ApiKey hoặc Ai:Model");
            throw Unavailable();
        }

        var config = new GenerateContentConfig
        {
            SystemInstruction = new Content { Parts = [new Part { Text = request.SystemInstruction }] },
            MaxOutputTokens = settings.MaxOutputTokens,
            Tools = request.Tools.Count == 0
                ? null
                : [new Tool { FunctionDeclarations = request.Tools.Select(ToDeclaration).ToList() }],
        };

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(_timeout);

        GenerateContentResponse response;
        try
        {
            response = await _client.Value.Models.GenerateContentAsync(
                settings.Model,
                request.Turns.Select(ToContent).ToList(),
                config,
                timeout.Token);
        }
        catch (Exception ex) when (!ct.IsCancellationRequested && IsServiceFailure(ex))
        {
            // Chỉ ghi loại lỗi, không ghi nội dung câu hỏi hay câu trả lời (NFR-13).
            logger.LogWarning("Dịch vụ AI lỗi: {ErrorType} {Status}", ex.GetType().Name, (ex as ApiException)?.StatusCode);
            throw Unavailable();
        }

        var content = response.Candidates?.FirstOrDefault()?.Content;
        var toolCalls = (response.FunctionCalls ?? [])
            .Where(call => !string.IsNullOrEmpty(call.Name))
            .Select(call => new AIToolCall(call.Id, call.Name!, ToJsonObject(call.Args)))
            .ToList();
        var text = response.Text;

        if (toolCalls.Count == 0 && string.IsNullOrWhiteSpace(text))
        {
            logger.LogWarning(
                "Dịch vụ AI không trả nội dung: {FinishReason}",
                response.Candidates?.FirstOrDefault()?.FinishReason);
            throw Unavailable();
        }

        return new AIReply(text, toolCalls, content);
    }

    public void Dispose()
    {
        if (_client.IsValueCreated)
        {
            _client.Value.Dispose();
        }
    }

    private static AppException Unavailable() =>
        new(ErrorCodes.AIUnavailable, "Trợ lý AI tạm thời không phản hồi. Thử lại sau ít phút; các chức năng khác vẫn dùng bình thường.");

    private static bool IsServiceFailure(Exception ex) =>
        ex is HttpRequestException or OperationCanceledException or TimeoutException or JsonException;

    private static FunctionDeclaration ToDeclaration(AIToolDefinition tool) => new()
    {
        Name = tool.Name,
        Description = tool.Description,
        ParametersJsonSchema = tool.ParametersSchema,
    };

    private static Content ToContent(AITurn turn) => turn switch
    {
        AIUserTurn user => new Content { Role = "user", Parts = [new Part { Text = user.Text }] },

        // Gửi lại nguyên lượt model đã trả để giữ chữ ký suy luận đi kèm lời gọi công cụ.
        AIModelTurn { ProviderState: Content original } => original,
        AIModelTurn model => new Content
        {
            Role = "model",
            Parts = model.ToolCalls.Count > 0
                ? model.ToolCalls.Select(call => new Part
                {
                    FunctionCall = new FunctionCall { Id = call.Id, Name = call.Name, Args = ToDictionary(call.Arguments) },
                }).ToList()
                : [new Part { Text = model.Text ?? string.Empty }],
        },
        AIToolResultsTurn results => new Content
        {
            Role = "user",
            Parts = results.Results.Select(result => new Part
            {
                FunctionResponse = new FunctionResponse
                {
                    Id = result.Id,
                    Name = result.Name,
                    Response = new Dictionary<string, object> { ["output"] = result.Output },
                },
            }).ToList(),
        },
        _ => throw new ArgumentOutOfRangeException(nameof(turn), turn, null),
    };

    private static JsonObject ToJsonObject(Dictionary<string, object>? args) =>
        args is null ? [] : JsonSerializer.SerializeToNode(args) as JsonObject ?? [];

    private static Dictionary<string, object> ToDictionary(JsonObject arguments) =>
        arguments.ToDictionary(p => p.Key, p => (object)p.Value?.DeepClone()!);
}
