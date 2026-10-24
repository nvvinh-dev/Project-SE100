using HouseholdFarm.Api.Domain.Enums;

namespace HouseholdFarm.Api.Features.Supplies;

// Không có giá để bên gọi trả thẳng được cho Thành viên không có quyền Quản lý kho.
public record SupplyStockResponse(Guid SupplyId, decimal StockQuantity, decimal MinStockLevel);

// Quantity giữ dấu như trong database (Out âm), giao diện tự hiển thị giá trị tuyệt đối.
// UnitCost, TotalCost chỉ được đưa ra API cho Chủ hộ và Thành viên có quyền Quản lý kho.
public record StockTransactionResponse(
    Guid Id,
    Guid SupplyId,
    StockTransactionType Type,
    Guid? SeasonId,
    DateOnly TransactionDate,
    decimal Quantity,
    decimal UnitCost,
    decimal TotalCost);
