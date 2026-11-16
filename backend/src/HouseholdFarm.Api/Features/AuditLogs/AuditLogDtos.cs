using System.Text.Json;
using HouseholdFarm.Api.Common.Responses;

namespace HouseholdFarm.Api.Features.AuditLogs;

/// <summary>Một dòng nhật ký thao tác của hộ (FR-AUD-02, FR-AUD-03).</summary>
/// <param name="ActorName">Họ tên người thực hiện; có thể là Quản trị hệ thống (khóa tài khoản, truy cập hỗ trợ).</param>
/// <param name="Action">Dạng <c>&lt;Entity&gt;.&lt;Thao tác&gt;</c>, ví dụ <c>Season.Close</c>.</param>
/// <param name="OldValues">Giá trị trước, chỉ các trường bị đổi, tên trường camelCase như API; trống nếu thao tác không đổi giá trị.</param>
/// <param name="NewValues">Giá trị sau, cùng quy ước với <paramref name="OldValues"/>.</param>
public sealed record AuditLogListItem(
    Guid Id,
    DateTime OccurredAt,
    Guid? ActorId,
    string? ActorName,
    string Action,
    string EntityType,
    Guid? EntityId,
    string? Reason,
    JsonElement? OldValues,
    JsonElement? NewValues);

/// <summary>
/// Tham số của nhật ký thao tác, luôn mới nhất trước. <c>from</c>, <c>to</c> là ngày theo giờ Việt Nam, tính cả hai đầu.
/// </summary>
public sealed record AuditLogQuery : PageQuery
{
    public DateOnly? From { get; init; }

    public DateOnly? To { get; init; }

    public Guid? ActorId { get; init; }

    /// <summary>Một giá trị <c>action</c> của nhật ký, ví dụ <c>Season.Close</c>.</summary>
    public string? Action { get; init; }
}
