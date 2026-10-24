namespace HouseholdFarm.Api.Common.Auditing;

/// <summary>
/// Ghi nhật ký thao tác (FR-AUD-01, FR-AUD-02). Chỉ thêm <c>AuditLog</c> vào <c>AppDbContext</c> dùng chung của yêu cầu, không tự lưu:
/// dòng nhật ký được lưu cùng lần <c>SaveChangesAsync</c> của thao tác, nên thao tác lỗi thì không có nhật ký,
/// ghi nhật ký lỗi thì thao tác bị hủy theo (BR-GEN-07).
/// </summary>
/// <example>
/// <code>
/// var changes = ChangedValues.Of(_db.Entry(sale));
/// _audit.Log(AuditActions.SaleCorrect, sale.Id, changes.OldValues, changes.NewValues, request.Reason);
/// await _db.SaveChangesAsync(ct);
/// </code>
/// </example>
public interface IAuditLogger
{
    /// <param name="action">Hằng số trong <see cref="AuditActions"/>; <c>entity_type</c> lấy phần trước dấu chấm.</param>
    /// <param name="entityId">Id của bản ghi bị tác động.</param>
    /// <param name="oldValues">
    /// Giá trị trước khi đổi, chỉ các trường bị đổi (BR-GEN-08); với <c>Correct</c> lấy từ <see cref="ChangedValues.Of"/>.
    /// Lưu thành JSON cùng định dạng với phản hồi API.
    /// </param>
    /// <param name="newValues">Giá trị sau khi đổi, cùng quy ước với <paramref name="oldValues"/>.</param>
    /// <param name="reason">Lý do; bắt buộc với các thao tác ở BR-GEN-06, validator của request đã kiểm tra.</param>
    void Log(string action, Guid? entityId, object? oldValues, object? newValues, string? reason);
}
