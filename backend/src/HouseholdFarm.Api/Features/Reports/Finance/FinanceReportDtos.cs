using HouseholdFarm.Api.Domain.Enums;

namespace HouseholdFarm.Api.Features.Reports.Finance;

/// <summary>
/// Số liệu tài chính của một mùa vụ, tính cho cả mùa vụ và không gồm bản ghi đã Void.
/// </summary>
/// <param name="SeasonId">Mùa vụ.</param>
/// <param name="Status">
/// Trạng thái hiện tại của mùa vụ (BR-RPT-03): khác <c>Closed</c> thì bên gọi ghi lợi nhuận là "tạm tính";
/// <c>Cancelled</c> thì bên gọi hiển thị tách riêng.
/// </param>
/// <param name="SupplyCost">Tổng <c>total_cost</c> các giao dịch kho <c>Out</c> của mùa vụ.</param>
/// <param name="ExpenseAmount">Tổng <c>amount</c> các khoản chi phí ghi tay của mùa vụ; không gồm chi phí chung của hộ.</param>
/// <param name="Revenue">Tổng doanh thu các giao dịch bán, mỗi giao dịch là <c>quantity × unit_price</c> làm tròn 2 chữ số.</param>
/// <param name="PaidAmount">Tổng các lần thanh toán của các giao dịch bán.</param>
/// <param name="OutstandingAmount"><c>Revenue − PaidAmount</c>.</param>
/// <param name="Profit"><c>Revenue − SupplyCost − ExpenseAmount</c>.</param>
public record SeasonFinanceResponse(
    Guid SeasonId,
    SeasonStatus Status,
    decimal SupplyCost,
    decimal ExpenseAmount,
    decimal Revenue,
    decimal PaidAmount,
    decimal OutstandingAmount,
    decimal Profit);
