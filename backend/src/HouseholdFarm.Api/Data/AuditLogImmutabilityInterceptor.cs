using HouseholdFarm.Api.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace HouseholdFarm.Api.Data;

/// <summary>
/// FR-AUD-04: nhật ký thao tác chỉ được ghi thêm. Khi lưu mà có dòng <see cref="AuditLog"/> bị sửa hoặc xóa thì ném lỗi và
/// không lưu gì, để không module nào lỡ tay sửa nhật ký qua <see cref="AppDbContext"/>.
/// </summary>
/// <remarks>
/// <c>ExecuteUpdate</c>, <c>ExecuteDelete</c> và SQL viết tay không đi qua interceptor; không dùng chúng trên <c>audit_logs</c>.
/// </remarks>
public sealed class AuditLogImmutabilityInterceptor : SaveChangesInterceptor
{
    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        EnsureAppendOnly(eventData.Context);
        return result;
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        EnsureAppendOnly(eventData.Context);
        return ValueTask.FromResult(result);
    }

    private static void EnsureAppendOnly(DbContext? context)
    {
        if (context is null)
        {
            return;
        }

        var changed = context.ChangeTracker.Entries<AuditLog>()
            .Any(e => e.State is EntityState.Modified or EntityState.Deleted);
        if (changed)
        {
            // Lỗi lập trình, không phải lỗi người dùng: trả INTERNAL_ERROR và không lưu thao tác nào của lần lưu này.
            throw new InvalidOperationException("Nhật ký thao tác chỉ được ghi thêm, không được sửa hoặc xóa.");
        }
    }
}
