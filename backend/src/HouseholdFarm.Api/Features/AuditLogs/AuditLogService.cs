using System.Text.Json;
using HouseholdFarm.Api.Common.Auth;
using HouseholdFarm.Api.Common.Responses;
using HouseholdFarm.Api.Common.Time;
using HouseholdFarm.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace HouseholdFarm.Api.Features.AuditLogs;

public sealed class AuditLogService(AppDbContext db, ICurrentUser currentUser, IAppClock clock) : IAuditLogService
{
    public async Task<PagedResult<AuditLogListItem>> GetListAsync(AuditLogQuery query, CancellationToken ct)
    {
        currentUser.EnsureOwner();

        // Global Query Filter giữ lại đúng các dòng của hộ, kể cả dòng Quản trị ghi cho hộ này.
        var logs = db.AuditLogs.AsNoTracking();

        // Ngày theo giờ Việt Nam: từ 0 giờ ngày "from" tới trước 0 giờ ngày sau "to".
        if (query.From is { } from)
        {
            var fromUtc = clock.StartOfDayUtc(from);
            logs = logs.Where(l => l.OccurredAt >= fromUtc);
        }

        if (query.To is { } to)
        {
            var toUtc = clock.StartOfDayUtc(to.AddDays(1));
            logs = logs.Where(l => l.OccurredAt < toUtc);
        }

        if (query.ActorId is { } actorId)
        {
            logs = logs.Where(l => l.ActorId == actorId);
        }

        if (query.Action is { } action)
        {
            logs = logs.Where(l => l.Action == action);
        }

        // Người thực hiện có thể là Quản trị hệ thống (không thuộc hộ), nên tra tên theo id, không lọc theo hộ.
        var page = await logs
            .OrderByDescending(l => l.OccurredAt)
            .ThenByDescending(l => l.Id)
            .Select(l => new
            {
                l.Id,
                l.OccurredAt,
                l.ActorId,
                ActorName = db.Users.Where(u => u.Id == l.ActorId).Select(u => u.FullName).FirstOrDefault(),
                l.Action,
                l.EntityType,
                l.EntityId,
                l.Reason,
                l.OldValues,
                l.NewValues,
            })
            .ToPagedResultAsync(query, ct);

        // Giá trị cũ, mới lưu dạng JSON: trả nguyên đối tượng JSON để giao diện đọc theo tên trường.
        var items = page.Items
            .Select(l => new AuditLogListItem(
                l.Id,
                l.OccurredAt,
                l.ActorId,
                l.ActorName,
                l.Action,
                l.EntityType,
                l.EntityId,
                l.Reason,
                ParseJson(l.OldValues),
                ParseJson(l.NewValues)))
            .ToList();
        return new PagedResult<AuditLogListItem>(items, page.Page, page.PageSize, page.TotalCount);
    }

    private static JsonElement? ParseJson(string? json) =>
        json is null ? null : JsonDocument.Parse(json).RootElement.Clone();
}
