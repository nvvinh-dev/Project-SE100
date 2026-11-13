using HouseholdFarm.Api.Common.Responses;
using HouseholdFarm.Api.Domain.Enums;

namespace HouseholdFarm.Api.Features.Sales;

/// <summary>Tạo khách hàng trong danh mục của hộ (FR-SAL-08).</summary>
/// <param name="Phone">Không bắt buộc; có thì được chuẩn hóa về 10 chữ số bắt đầu bằng 0.</param>
/// <param name="Status">Để trống thì khách hàng mới ở trạng thái <c>Active</c>.</param>
public sealed record CreateCustomerRequest(
    string Name,
    string? Phone,
    string? Note,
    RecordStatus? Status);

/// <summary>Sửa khách hàng, kể cả chuyển ngừng sử dụng. Không có API xóa khách hàng.</summary>
/// <param name="Version">Giá trị <c>version</c> nhận được khi đọc khách hàng (BR-GEN-11).</param>
public sealed record UpdateCustomerRequest(
    string Name,
    string? Phone,
    string? Note,
    RecordStatus? Status,
    uint Version);

/// <summary>Chi tiết một khách hàng.</summary>
public sealed record CustomerResponse(
    Guid Id,
    string Name,
    string? Phone,
    string? Note,
    RecordStatus Status,
    DateTime CreatedAt,
    uint Version);

/// <summary>Một dòng của danh sách khách hàng.</summary>
public sealed record CustomerListItem(
    Guid Id,
    string Name,
    string? Phone,
    RecordStatus Status);

/// <summary>Tham số của danh sách khách hàng: <c>search</c> tìm theo tên và số điện thoại, sắp theo tên.</summary>
public sealed record CustomerQuery : PageQuery
{
    public RecordStatus? Status { get; init; }
}
