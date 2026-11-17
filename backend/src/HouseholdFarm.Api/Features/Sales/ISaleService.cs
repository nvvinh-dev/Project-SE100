using HouseholdFarm.Api.Common.Responses;

namespace HouseholdFarm.Api.Features.Sales;

/// <summary>
/// Giao dịch bán nông sản, do Chủ hộ, Thành viên hộ có quyền Quản lý tài chính thực hiện (mọi mùa vụ của hộ, BR-USR-13).
/// Tồn nông sản kiểm tra qua <c>IProduceStockService</c> của module thu hoạch, không tự tính ở đây.
/// </summary>
public interface ISaleService
{
    /// <summary>
    /// Danh sách giao dịch bán, mới nhất trước; mặc định không gồm giao dịch đã hủy. Lọc thời gian theo <c>sale_date</c>.
    /// Lỗi: <c>FORBIDDEN</c>.
    /// </summary>
    Task<PagedResult<SaleListItem>> GetListAsync(SaleQuery query, CancellationToken ct);

    /// <summary>Chi tiết một giao dịch bán. Lỗi: <c>FORBIDDEN</c>, <c>NOT_FOUND</c>.</summary>
    Task<SaleResponse> GetAsync(Guid id, CancellationToken ct);

    /// <summary>
    /// Tạo giao dịch bán (BR-SAL-01), có thể kèm liên kết phiếu thu (BR-SAL-05). Gửi lại cùng <c>requestId</c> thì trả về
    /// giao dịch đã tạo (BR-GEN-10).
    /// Lỗi: <c>FORBIDDEN</c>, <c>NOT_FOUND</c>, <c>VALIDATION_ERROR</c>, <c>CATALOG_INACTIVE</c>, <c>SEASON_INVALID_STATE</c>,
    /// <c>INSUFFICIENT_PRODUCE_STOCK</c>.
    /// </summary>
    Task<SaleResponse> CreateAsync(CreateSaleRequest request, CancellationToken ct);

    /// <summary>
    /// Điều chỉnh giao dịch bán (BR-SAL-03): kiểm tra tồn sau khi hoàn lại lượng của chính giao dịch này, doanh thu mới
    /// không nhỏ hơn số đã thu, danh sách phiếu thu gửi lên thay toàn bộ liên kết cũ.
    /// Lỗi: <c>FORBIDDEN</c>, <c>NOT_FOUND</c>, <c>VALIDATION_ERROR</c>, <c>ALREADY_VOIDED</c>, <c>CATALOG_INACTIVE</c>,
    /// <c>SEASON_INVALID_STATE</c>, <c>INSUFFICIENT_PRODUCE_STOCK</c>, <c>REVENUE_BELOW_PAID</c>, <c>CONCURRENCY_CONFLICT</c>.
    /// </summary>
    Task<SaleResponse> CorrectAsync(Guid id, CorrectSaleRequest request, CancellationToken ct);

    /// <summary>
    /// Hủy giao dịch bán khi mọi thanh toán của nó đã hủy (BR-SAL-04). Lỗi: <c>FORBIDDEN</c>, <c>NOT_FOUND</c>,
    /// <c>ALREADY_VOIDED</c>, <c>SEASON_INVALID_STATE</c>, <c>SALE_HAS_PAYMENTS</c>.
    /// </summary>
    Task<SaleResponse> VoidAsync(Guid id, VoidSaleRequest request, CancellationToken ct);

    /// <summary>
    /// Công nợ bán hàng chưa thu (FR-SAL-07): giao dịch chưa hủy còn nợ, nợ lâu nhất trước, kèm tổng số và tổng tiền.
    /// Lỗi: <c>FORBIDDEN</c>.
    /// </summary>
    Task<SaleDebtsResponse> GetDebtsAsync(CancellationToken ct);
}
