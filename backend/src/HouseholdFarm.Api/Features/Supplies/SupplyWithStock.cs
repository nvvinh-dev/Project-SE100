using HouseholdFarm.Api.Domain.Entities;

namespace HouseholdFarm.Api.Features.Supplies;

// Dạng trung gian của truy vấn: khai báo bằng thuộc tính init (không dùng record có hàm dựng) để EF lọc, sắp xếp được theo tồn.
public sealed class SupplyWithStock
{
    public required Supply Supply { get; init; }

    public required decimal StockQuantity { get; init; }

    public required bool HasStockTransactions { get; init; }
}
