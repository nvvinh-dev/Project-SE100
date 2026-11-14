using System.Text.Json;
using HouseholdFarm.Api.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace HouseholdFarm.Api.Tests.Infrastructure;

/// <summary>Kiểm tra nhật ký thao tác trong test của mọi module (FR-AUD-01, BR-GEN-07).</summary>
public static class AuditLogAssertions
{
    /// <summary>
    /// Có đúng một dòng nhật ký <paramref name="action"/> cho bản ghi <paramref name="entityId"/>, thuộc hộ
    /// <paramref name="householdId"/>, kèm lý do <paramref name="reason"/> (bỏ qua nếu không truyền). Trả dòng đó để kiểm tra thêm.
    /// </summary>
    /// <example>
    /// <code>
    /// var log = await AuditLogAssertions.AssertSingleAsync(factory.Postgres, household.Household.Id,
    ///     AuditActions.SaleVoid, sale.Id, ct, reason: "Ghi nhầm");
    /// </code>
    /// </example>
    public static async Task<AuditLog> AssertSingleAsync(
        PostgresFixture postgres,
        Guid householdId,
        string action,
        Guid entityId,
        CancellationToken ct,
        string? reason = null)
    {
        await using var db = postgres.CreateDbContext();
        var logs = await db.AuditLogs.IgnoreQueryFilters()
            .Where(l => l.EntityId == entityId && l.Action == action)
            .ToListAsync(ct);

        var log = Assert.Single(logs);
        Assert.Equal(householdId, log.HouseholdId);
        if (reason is not null)
        {
            Assert.Equal(reason, log.Reason);
        }

        return log;
    }

    /// <summary>Không có dòng nhật ký nào cho bản ghi <paramref name="entityId"/>: thao tác lỗi thì không có nhật ký (BR-GEN-07).</summary>
    public static async Task AssertNoneAsync(PostgresFixture postgres, Guid entityId, CancellationToken ct)
    {
        await using var db = postgres.CreateDbContext();
        Assert.False(await db.AuditLogs.IgnoreQueryFilters().AnyAsync(l => l.EntityId == entityId, ct));
    }

    /// <summary>Tên các trường trong <c>old_values</c> hoặc <c>new_values</c>, sắp theo bảng chữ cái.</summary>
    public static IReadOnlyList<string> FieldNames(string? json) =>
        json is null
            ? []
            : JsonDocument.Parse(json).RootElement.EnumerateObject().Select(p => p.Name).Order().ToList();
}
