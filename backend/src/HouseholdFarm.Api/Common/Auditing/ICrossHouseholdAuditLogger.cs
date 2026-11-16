namespace HouseholdFarm.Api.Common.Auditing;

/// <summary>
/// Ghi nhật ký vào một hộ chỉ định thay vì hộ của người đang đăng nhập. Chỉ dùng cho thao tác của Quản trị hệ thống lên một hộ
/// (khóa, mở khóa tài khoản, truy cập hỗ trợ): Quản trị không thuộc hộ nào, nhưng Chủ hộ phải thấy được các dòng này ở nhật ký
/// của hộ mình (BR-USR-16). Cách lưu giống <see cref="IAuditLogger"/>: cùng lần <c>SaveChangesAsync</c> với thao tác (BR-GEN-07).
/// </summary>
public interface ICrossHouseholdAuditLogger
{
    /// <param name="householdId">Hộ chứa bản ghi bị tác động.</param>
    /// <inheritdoc cref="IAuditLogger.Log" path="/param"/>
    void Log(Guid householdId, string action, Guid? entityId, object? oldValues, object? newValues, string? reason);
}
