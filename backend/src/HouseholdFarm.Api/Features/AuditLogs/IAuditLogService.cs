using HouseholdFarm.Api.Common.Responses;

namespace HouseholdFarm.Api.Features.AuditLogs;

/// <summary>
/// Xem nhật ký thao tác của hộ (FR-AUD-03). Chỉ có hàm đọc: nhật ký do các module ghi qua <c>IAuditLogger</c>, không ai sửa,
/// xóa được (FR-AUD-04).
/// </summary>
public interface IAuditLogService
{
    /// <summary>
    /// Nhật ký của hộ người đang đăng nhập, mới nhất trước, gồm cả dòng do Quản trị hệ thống tạo cho hộ (BR-USR-16).
    /// Chỉ Chủ hộ. Lỗi: <c>FORBIDDEN</c>.
    /// </summary>
    Task<PagedResult<AuditLogListItem>> GetListAsync(AuditLogQuery query, CancellationToken ct);
}
