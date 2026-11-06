using HouseholdFarm.Api.Common.Responses;

namespace HouseholdFarm.Api.Features.Supplies;

/// <summary>
/// Giao dịch kho do Chủ hộ, Thành viên hộ có quyền Quản lý kho thực hiện trực tiếp. Giao dịch xuất sinh từ công việc đi qua
/// <see cref="IStockService"/>.
/// </summary>
public interface IStockTransactionService
{
    /// <summary>
    /// Lịch sử giao dịch kho, mới nhất trước; mặc định không gồm giao dịch đã hủy.
    /// Lọc thời gian theo <c>transaction_date</c>. Lỗi: <c>FORBIDDEN</c>.
    /// </summary>
    Task<PagedResult<StockTransactionListItem>> GetListAsync(StockTransactionQuery query, CancellationToken ct);

    /// <summary>Chi tiết một giao dịch kho. Lỗi: <c>FORBIDDEN</c>, <c>NOT_FOUND</c>.</summary>
    Task<StockTransactionResponse> GetAsync(Guid id, CancellationToken ct);

    /// <summary>
    /// Tạo giao dịch kho theo <c>type</c>. Gửi lại cùng <c>requestId</c> thì trả về giao dịch đã tạo (BR-GEN-10).
    /// Lỗi: <c>FORBIDDEN</c>, <c>NOT_FOUND</c>, <c>CATALOG_INACTIVE</c>, <c>SEASON_INVALID_STATE</c>, <c>INSUFFICIENT_STOCK</c>.
    /// </summary>
    Task<StockTransactionResponse> CreateAsync(CreateStockTransactionRequest request, CancellationToken ct);

    /// <summary>
    /// Hủy giao dịch kho (BR-SUP-10, BR-SUP-11). Lỗi: <c>FORBIDDEN</c>, <c>NOT_FOUND</c>, <c>ALREADY_VOIDED</c>,
    /// <c>STOCK_TX_MANAGED_BY_TASK</c>, <c>STOCK_TX_NOT_LAST</c>, <c>SEASON_INVALID_STATE</c>, <c>INSUFFICIENT_STOCK</c>.
    /// </summary>
    Task<StockTransactionResponse> VoidAsync(Guid id, VoidStockTransactionRequest request, CancellationToken ct);
}
