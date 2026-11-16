using HouseholdFarm.Api.Domain.Enums;
using HouseholdFarm.Api.Features.Reports.Common;

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

/// <summary>Bộ lọc của báo cáo chi phí - doanh thu (FR-RPT-01): khoảng thời gian, mùa vụ, thửa, cây trồng.</summary>
public sealed record FinanceReportQuery : ReportFilter;

/// <summary>
/// Báo cáo chi phí - doanh thu (FR-RPT-FIN-01, FR-RPT-FIN-03, FR-RPT-FIN-04, FR-RPT-FIN-06).
/// Các tổng theo nhóm, theo kênh, theo tháng tính theo ngày nghiệp vụ trong kỳ, chỉ của mùa vụ khớp bộ lọc;
/// số liệu từng mùa vụ tính cho cả mùa (BR-RPT-13).
/// </summary>
/// <param name="SupplyCosts">Chi phí vật tư theo nhóm vật tư, sắp giảm dần.</param>
/// <param name="ExpenseCosts">Chi phí ghi tay của mùa vụ theo nhóm chi phí, sắp giảm dần.</param>
/// <param name="GeneralExpenseAmount">
/// Chi phí chung của hộ trong kỳ; <c>null</c> khi lọc theo mùa vụ, thửa hoặc cây trồng (BR-RPT-13 ý 3).
/// </param>
/// <param name="TotalCost">Tổng chi phí trong kỳ = vật tư + ghi tay + chi phí chung (nếu có).</param>
/// <param name="RevenueByChannel">Doanh thu theo kênh bán; <c>Channel = null</c> là giao dịch không ghi kênh.</param>
/// <param name="TotalRevenue">Tổng doanh thu trong kỳ.</param>
/// <param name="Seasons">Lợi nhuận theo mùa vụ chưa hủy, tính cho cả mùa.</param>
/// <param name="CancelledSeasons">Mùa vụ đã hủy, tách riêng (BR-RPT-03).</param>
/// <param name="ClosedProfit">Tổng lợi nhuận của các mùa vụ đã chốt (FR-RPT-FIN-04).</param>
/// <param name="ProvisionalProfit">Tổng lợi nhuận tạm tính của các mùa vụ chưa chốt (FR-RPT-FIN-04).</param>
/// <param name="CashFlow">Dòng tiền trong kỳ, chỉ lọc theo thời gian (BR-RPT-07).</param>
/// <param name="Monthly">Doanh thu và chi phí theo từng tháng trong kỳ (FR-RPT-FIN-06).</param>
public sealed record FinanceReportResponse(
    IReadOnlyList<FinanceSupplyCostItem> SupplyCosts,
    IReadOnlyList<FinanceExpenseCostItem> ExpenseCosts,
    decimal? GeneralExpenseAmount,
    decimal TotalCost,
    IReadOnlyList<FinanceRevenueChannelItem> RevenueByChannel,
    decimal TotalRevenue,
    IReadOnlyList<FinanceSeasonItem> Seasons,
    IReadOnlyList<FinanceSeasonItem> CancelledSeasons,
    decimal ClosedProfit,
    decimal ProvisionalProfit,
    FinanceCashFlow CashFlow,
    IReadOnlyList<FinanceMonthItem> Monthly);

public sealed record FinanceSupplyCostItem(SupplyGroup Group, decimal Amount);

public sealed record FinanceExpenseCostItem(ExpenseCategory Category, decimal Amount);

public sealed record FinanceRevenueChannelItem(SalesChannel? Channel, decimal Amount);

/// <summary>Một mùa vụ trong bảng lợi nhuận: thông tin mùa vụ dùng chung và số liệu tài chính cả mùa.</summary>
public sealed record FinanceSeasonItem(
    ReportSeasonSummaryDto Season,
    decimal SupplyCost,
    decimal ExpenseAmount,
    decimal Revenue,
    decimal PaidAmount,
    decimal OutstandingAmount,
    decimal Profit);

/// <summary>
/// Dòng tiền (FR-RPT-FIN-03, BR-RPT-07). Giả định: vật tư nhập kho được trả tiền ngay tại ngày nhập.
/// </summary>
/// <param name="StockInAmount">Tiền chi cho nhập kho: tổng <c>total_cost</c> giao dịch <c>In</c> trong kỳ.</param>
/// <param name="ExpenseAmount">Tiền chi cho chi phí ghi tay trong kỳ, gồm chi phí chung của hộ.</param>
/// <param name="CashOut">Tổng tiền chi.</param>
/// <param name="CashIn">Tiền thu: tổng thanh toán trong kỳ.</param>
/// <param name="UnpaidRevenue">Doanh thu chưa thu tính đến ngày cuối kỳ.</param>
public sealed record FinanceCashFlow(
    decimal StockInAmount,
    decimal ExpenseAmount,
    decimal CashOut,
    decimal CashIn,
    decimal UnpaidRevenue);

/// <param name="Month">Tháng dạng <c>yyyy-MM</c>.</param>
/// <param name="Cost">Giá trị xuất kho cộng chi phí ghi tay của tháng, không phải tiền chi của dòng tiền (BR-RPT-13 ý 6).</param>
public sealed record FinanceMonthItem(string Month, decimal Revenue, decimal Cost);
