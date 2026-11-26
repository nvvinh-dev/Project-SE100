namespace HouseholdFarm.Api.Features.Ai;

/// <summary>Cấu hình nhóm khóa <c>Ai</c>. <c>Ai:ApiKey</c> là bí mật, khai báo bằng <c>dotnet user-secrets</c>.</summary>
public sealed class AIOptions
{
    public const string SectionName = "Ai";

    public string? ApiKey { get; init; }

    /// <summary>Tên model loại Flash-Lite (D-18).</summary>
    public string? Model { get; init; }

    /// <summary>Số lượt mỗi hộ mỗi ngày theo giờ Việt Nam (D-19, BR-AI-04).</summary>
    public int DailyQuotaPerHousehold { get; init; } = 50;

    /// <summary>Giới hạn độ dài câu trả lời (NFR-14).</summary>
    public int MaxOutputTokens { get; init; } = 1024;
}
