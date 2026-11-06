namespace HouseholdFarm.Api.Features.Supplies;

/// <summary>
/// Giao dịch kho do Chủ hộ, Thành viên hộ có quyền Quản lý kho thực hiện trực tiếp. Giao dịch xuất sinh từ công việc đi qua
/// <see cref="IStockService"/>.
/// </summary>
public interface IStockTransactionService
{
    /// <summary>Chi tiết một giao dịch kho. Lỗi: <c>FORBIDDEN</c>, <c>NOT_FOUND</c>.</summary>
    Task<StockTransactionResponse> GetAsync(Guid id, CancellationToken ct);

    /// <summary>
    /// Tạo giao dịch kho theo <c>type</c>. Gửi lại cùng <c>requestId</c> thì trả về giao dịch đã tạo (BR-GEN-10).
    /// Lỗi: <c>FORBIDDEN</c>, <c>NOT_FOUND</c>, <c>CATALOG_INACTIVE</c>.
    /// </summary>
    Task<StockTransactionResponse> CreateAsync(CreateStockTransactionRequest request, CancellationToken ct);
}
