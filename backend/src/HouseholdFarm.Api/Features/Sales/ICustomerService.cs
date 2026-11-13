using HouseholdFarm.Api.Common.Responses;

namespace HouseholdFarm.Api.Features.Sales;

/// <summary>
/// Danh mục khách hàng của hộ (FR-SAL-08), do Chủ hộ, Thành viên hộ có quyền Quản lý tài chính quản lý.
/// Kênh bán là danh sách cố định <c>SalesChannel</c>, không nằm trong danh mục này.
/// </summary>
public interface ICustomerService
{
    /// <summary>Danh sách khách hàng, sắp theo tên. Lỗi: <c>FORBIDDEN</c>.</summary>
    Task<PagedResult<CustomerListItem>> GetListAsync(CustomerQuery query, CancellationToken ct);

    /// <summary>Chi tiết một khách hàng. Lỗi: <c>FORBIDDEN</c>, <c>NOT_FOUND</c>.</summary>
    Task<CustomerResponse> GetAsync(Guid id, CancellationToken ct);

    /// <summary>Tạo khách hàng. Lỗi: <c>FORBIDDEN</c>.</summary>
    Task<CustomerResponse> CreateAsync(CreateCustomerRequest request, CancellationToken ct);

    /// <summary>
    /// Sửa khách hàng, kể cả chuyển <c>Inactive</c>; giao dịch bán cũ giữ nguyên khách hàng (BR-GEN-13).
    /// Lỗi: <c>FORBIDDEN</c>, <c>NOT_FOUND</c>, <c>CONCURRENCY_CONFLICT</c>.
    /// </summary>
    Task<CustomerResponse> UpdateAsync(Guid id, UpdateCustomerRequest request, CancellationToken ct);
}
