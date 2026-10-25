using System.Text.Json;
using HouseholdFarm.Api.Common.Auth;
using HouseholdFarm.Api.Common.Time;
using HouseholdFarm.Api.Data;
using HouseholdFarm.Api.Domain.Entities;
using Microsoft.AspNetCore.Http.Json;
using Microsoft.Extensions.Options;

namespace HouseholdFarm.Api.Common.Auditing;

// Hộ, người thực hiện, thời điểm do hệ thống điền: audit_logs không có cột chung nên interceptor không điền thay.
public sealed class AuditLogger(
    AppDbContext db,
    ICurrentUser currentUser,
    IAppClock clock,
    IOptions<JsonOptions> jsonOptions) : IAuditLogger
{
    // Cùng cấu hình JSON với phản hồi API để màn hình nhật ký đọc giá trị theo đúng tên trường, tên enum frontend đã biết.
    private readonly JsonSerializerOptions _jsonOptions = jsonOptions.Value.SerializerOptions;

    public void Log(string action, Guid? entityId, object? oldValues, object? newValues, string? reason)
    {
        // Tên gõ sai thì hỏng ngay khi chạy test, không để lọt vào nhật ký một action mà bộ lọc theo loại hành động không biết.
        if (!AuditActions.IsDefined(action))
        {
            throw new ArgumentException($"Hành động {action} không có trong AuditActions.", nameof(action));
        }

        db.AuditLogs.Add(new AuditLog
        {
            HouseholdId = currentUser.HouseholdId,
            ActorId = currentUser.IsAuthenticated ? currentUser.UserId : null,
            Action = action,
            EntityType = action[..action.IndexOf('.')],
            EntityId = entityId,
            OldValues = ToJson(oldValues),
            NewValues = ToJson(newValues),
            Reason = reason,
            OccurredAt = clock.UtcNow,
        });
    }

    private string? ToJson(object? values) => values is null ? null : JsonSerializer.Serialize(values, _jsonOptions);
}
