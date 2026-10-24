using HouseholdFarm.Api.Common.Auth;
using HouseholdFarm.Api.Common.Time;
using HouseholdFarm.Api.Domain.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace HouseholdFarm.Api.Common.Persistence;

/// <summary>
/// Tự điền cột chung khi lưu: <c>household_id</c>, <c>created_by_id</c>, <c>updated_by_id</c> lấy từ người đang đăng nhập;
/// <c>created_at</c>, <c>updated_at</c> lấy từ <see cref="IAppClock"/>. Service không gán tay các cột này.
/// </summary>
/// <remarks>
/// <c>created_at</c>, <c>updated_at</c> điền theo tên thuộc tính cho mọi entity có thuộc tính đó, kể cả entity không mang
/// interface đánh dấu (<c>Household</c>, <c>ApplicationUser</c>, <c>RefreshToken</c>, <c>AIUsage</c>).
/// <c>AuditLog</c> và <c>ReportExport</c> có cột thời điểm, người thực hiện riêng, do service của chúng tự điền.
/// </remarks>
public sealed class CommonColumnsInterceptor(ICurrentUser currentUser, IAppClock clock) : SaveChangesInterceptor
{
    private static readonly string[] _creationColumns =
        [nameof(IAuditable.CreatedAt), nameof(IAuditable.CreatedById)];

    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        if (eventData.Context is not null)
        {
            FillCommonColumns(eventData.Context);
        }

        return base.SavingChanges(eventData, result);
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        if (eventData.Context is not null)
        {
            FillCommonColumns(eventData.Context);
        }

        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    private void FillCommonColumns(DbContext context)
    {
        var now = clock.UtcNow;

        // Gán qua EntityEntry để thay đổi được ghi nhận dù DetectChanges đã chạy trước interceptor.
        foreach (var entry in context.ChangeTracker.Entries().Where(e => !e.Metadata.IsOwned()))
        {
            if (entry.State == EntityState.Added)
            {
                FillOnAdd(entry, now);
                continue;
            }

            if (entry.State == EntityState.Modified)
            {
                KeepCreationColumns(entry);
            }

            // Bỏ thay đổi ở cột tạo có thể đưa bản ghi về Unchanged, khi đó không có gì để ghi người sửa.
            if (entry.State == EntityState.Modified || HasChangedOwnedReference(entry))
            {
                FillOnUpdate(entry, now);
            }
        }
    }

    private void FillOnAdd(EntityEntry entry, DateTime now)
    {
        if (entry.Entity is IHouseholdOwned)
        {
            // BR-GEN-01: người đã đăng nhập thì hộ luôn lấy từ người đó, ghi đè giá trị đã gán sẵn.
            SetFromCurrentUserOrRequirePreset(entry, nameof(IHouseholdOwned.HouseholdId), currentUser.HouseholdId);
        }

        if (entry.Entity is IAuditable)
        {
            SetFromCurrentUserOrRequirePreset(
                entry,
                nameof(IAuditable.CreatedById),
                currentUser.IsAuthenticated ? currentUser.UserId : null);
        }

        if (entry.Metadata.FindProperty(nameof(IAuditable.CreatedAt)) is not null)
        {
            entry.Property(nameof(IAuditable.CreatedAt)).CurrentValue = now;
        }
    }

    private void FillOnUpdate(EntityEntry entry, DateTime now)
    {
        if (entry.Metadata.FindProperty(nameof(IVersioned.UpdatedAt)) is not null)
        {
            entry.Property(nameof(IVersioned.UpdatedAt)).CurrentValue = now;
        }

        // Không có người đăng nhập (dữ liệu mẫu, việc nền) thì giữ người sửa cũ, vì không biết ai sửa.
        if (entry.Entity is IVersioned && currentUser.IsAuthenticated)
        {
            entry.Property(nameof(IVersioned.UpdatedById)).CurrentValue = currentUser.UserId;
        }
    }

    private void SetFromCurrentUserOrRequirePreset(EntityEntry entry, string propertyName, Guid? valueFromCurrentUser)
    {
        var property = entry.Property(propertyName);
        if (valueFromCurrentUser is { } value)
        {
            property.CurrentValue = value;
            return;
        }

        // BR-GEN-01: người đã đăng nhập mà không thuộc hộ nào (Quản trị hệ thống) không được ghi dữ liệu của hộ,
        // kể cả khi bên tạo đã gán hộ: truy cập hỗ trợ chỉ xem (BR-USR-16).
        if (currentUser.IsAuthenticated)
        {
            throw new InvalidOperationException(
                $"Người dùng không thuộc hộ nào nên không được thêm bản ghi {entry.Metadata.ClrType.Name}.");
        }

        // Không có người đăng nhập (dữ liệu mẫu, dữ liệu test) thì dùng giá trị bên tạo đã gán; chưa gán là lỗi lập trình,
        // không để lọt một dòng có Guid rỗng vào database.
        if (property.CurrentValue is Guid preset && preset == Guid.Empty)
        {
            throw new InvalidOperationException(
                $"Không xác định được {propertyName} cho bản ghi {entry.Metadata.ClrType.Name} mới: "
                + "chưa có người đăng nhập và bên tạo chưa gán giá trị.");
        }
    }

    // BR-GEN-01: hộ, người tạo, thời điểm tạo không đổi sau khi tạo; service lỡ gán (ví dụ chép giá trị từ request)
    // thì trả về giá trị cũ để không chuyển được bản ghi sang hộ khác.
    private static void KeepCreationColumns(EntityEntry entry)
    {
        var columns = entry.Entity is IHouseholdOwned
            ? _creationColumns.Append(nameof(IHouseholdOwned.HouseholdId))
            : _creationColumns;

        foreach (var name in columns)
        {
            if (entry.Metadata.FindProperty(name) is not null && entry.Property(name) is { IsModified: true } property)
            {
                property.CurrentValue = property.OriginalValue;
                property.IsModified = false;
            }
        }
    }

    // Đổi cài đặt hộ (owned type HouseholdSettings) chỉ làm dòng owned bị sửa, dòng Household vẫn Unchanged;
    // nếu không xét thì updated_at của hộ không đổi khi Chủ hộ đổi cài đặt.
    private static bool HasChangedOwnedReference(EntityEntry entry) =>
        entry.State == EntityState.Unchanged
        && entry.References.Any(r => r.TargetEntry is { } target
            && target.Metadata.IsOwned()
            && target.State is EntityState.Added or EntityState.Modified);
}
