using System.Text.Json;
using HouseholdFarm.Api.Domain.Common;
using Microsoft.EntityFrameworkCore.ChangeTracking;

namespace HouseholdFarm.Api.Common.Auditing;

/// <summary>
/// Giá trị trước và sau của các trường bị đổi trên một bản ghi, để nhật ký <c>Correct</c> chỉ lưu phần thay đổi,
/// không lưu bản sao cả bản ghi (BR-GEN-08, FR-AUD-02). Tên trường dạng camelCase như phản hồi API.
/// </summary>
public sealed record ChangedValues(IReadOnlyDictionary<string, object?> OldValues, IReadOnlyDictionary<string, object?> NewValues)
{
    // Cột do hệ thống điền, không phải dữ liệu người dùng sửa; Version đổi theo mọi lần lưu nên không mang thông tin.
    private static readonly HashSet<string> _systemColumns =
    [
        nameof(IHouseholdOwned.HouseholdId),
        nameof(IAuditable.CreatedAt),
        nameof(IAuditable.CreatedById),
        nameof(IVersioned.UpdatedAt),
        nameof(IVersioned.UpdatedById),
        nameof(IVersioned.Version),
    ];

    /// <summary>Không có trường nào đổi.</summary>
    public bool IsEmpty => OldValues.Count == 0;

    /// <summary>
    /// So giá trị lúc đọc với giá trị hiện tại của bản ghi đang được theo dõi. Gọi sau khi gán giá trị mới và trước
    /// <c>SaveChangesAsync</c>: lưu xong thì giá trị lúc đọc đã thành giá trị mới.
    /// </summary>
    /// <param name="entry"><c>_db.Entry(entity)</c> của bản ghi được đọc lên để sửa (không dùng <c>AsNoTracking</c>).</param>
    public static ChangedValues Of(EntityEntry entry)
    {
        entry.DetectChanges();

        var oldValues = new Dictionary<string, object?>();
        var newValues = new Dictionary<string, object?>();
        foreach (var property in entry.Properties)
        {
            var metadata = property.Metadata;
            if (_systemColumns.Contains(metadata.Name)
                || metadata.IsShadowProperty()
                || metadata.GetValueComparer().Equals(property.OriginalValue, property.CurrentValue))
            {
                continue;
            }

            var name = JsonNamingPolicy.CamelCase.ConvertName(metadata.Name);
            oldValues[name] = property.OriginalValue;
            newValues[name] = property.CurrentValue;
        }

        return new ChangedValues(oldValues, newValues);
    }
}
