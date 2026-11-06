using HouseholdFarm.Api.Common.Auditing;
using HouseholdFarm.Api.Common.Auth;
using HouseholdFarm.Api.Common.Errors;
using HouseholdFarm.Api.Common.Time;
using HouseholdFarm.Api.Data;
using HouseholdFarm.Api.Domain.Entities;
using HouseholdFarm.Api.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace HouseholdFarm.Api.Features.Supplies;

/// <summary>
/// Thao tác xuất kho và hủy giao dịch, dùng chung cho API giao dịch kho và <see cref="IStockService"/> để công thức, kiểm tra
/// chỉ nằm ở một nơi. Bên gọi chịu trách nhiệm kiểm tra quyền, mở giao dịch database, khóa dòng, lưu và commit.
/// </summary>
public sealed class StockOperations(
    AppDbContext db,
    ICurrentUser currentUser,
    IAppClock clock,
    IAuditLogger audit,
    StockLedger ledger)
{
    /// <summary>
    /// BR-SUP-06: thêm giao dịch <c>Out</c>, đơn giá là giá bình quân hiện tại, giá bình quân không đổi.
    /// Bên gọi đã khóa dòng <c>seasons</c> rồi <c>supplies</c>. Lỗi: <c>INSUFFICIENT_STOCK</c>.
    /// </summary>
    /// <param name="quantity">Lượng xuất, số dương; giao dịch lưu thành số âm.</param>
    public async Task<StockTransaction> AddStockOutAsync(
        Supply supply,
        Guid seasonId,
        decimal quantity,
        DateOnly transactionDate,
        string? note,
        Guid requestId,
        CancellationToken ct)
    {
        var stock = await db.StockTransactions.StockQuantityOfAsync(supply.Id, ct);
        await ledger.CheckDecreaseAsync(supply, stock, quantity, "xuất", ct);

        var transaction = new StockTransaction
        {
            SupplyId = supply.Id,
            Type = StockTransactionType.Out,
            TransactionDate = transactionDate,
            Quantity = -quantity,
            UnitCost = supply.AverageUnitCost,
            AverageUnitCostBefore = supply.AverageUnitCost,
            TotalCost = StockCosting.TotalCost(quantity, supply.AverageUnitCost),
            SeasonId = seasonId,
            Note = note,
            RequestId = requestId,
        };
        db.StockTransactions.Add(transaction);

        audit.Log(AuditActions.StockTransactionStockOut, transaction.Id, oldValues: null, AuditValues(transaction), reason: null);
        return transaction;
    }

    /// <summary>
    /// Đọc giao dịch cần hủy sau khi đã khóa và kiểm tra lại chưa bị hủy: người khác có thể đã hủy trong lúc chờ khóa (BR-GEN-09).
    /// Lỗi: <c>ALREADY_VOIDED</c>.
    /// </summary>
    public async Task<StockTransaction> ReadForVoidAsync(Guid id, CancellationToken ct)
    {
        // Bên gọi lồng có thể đã nạp giao dịch từ trước khi có khóa; DbContext không tự cập nhật entity đã theo dõi.
        var wasTracked = db.StockTransactions.Local.Any(t => t.Id == id);
        var transaction = await db.StockTransactions.SingleAsync(t => t.Id == id, ct);
        if (wasTracked)
        {
            await db.Entry(transaction).ReloadAsync(ct);
        }

        if (transaction.VoidedAt is not null)
        {
            throw AlreadyVoided();
        }

        return transaction;
    }

    /// <summary>
    /// Hủy giao dịch theo BR-SUP-10 (làm tăng tồn) hoặc BR-SUP-11 (làm giảm tồn). Bên gọi đã khóa dòng và kiểm tra quyền,
    /// trạng thái mùa vụ. Lỗi: <c>STOCK_TX_NOT_LAST</c>, <c>INSUFFICIENT_STOCK</c>.
    /// </summary>
    public async Task VoidAsync(StockTransaction transaction, Supply supply, string reason, CancellationToken ct)
    {
        var stock = await db.StockTransactions.StockQuantityOfAsync(supply.Id, ct);

        if (transaction.Quantity > 0)
        {
            await EnsureLastAsync(transaction, ct);
            await ledger.CheckDecreaseAsync(supply, stock, transaction.Quantity, "hủy", ct);

            // BR-SUP-10: không còn thao tác nào sau nó nên trả về đúng giá trước giao dịch.
            supply.AverageUnitCost = transaction.AverageUnitCostBefore;
        }
        else
        {
            // BR-SUP-11: coi như nhập lại với đơn giá của chính giao dịch bị hủy.
            supply.AverageUnitCost = StockCosting.AverageAfterReceipt(
                stock,
                supply.AverageUnitCost,
                -transaction.Quantity,
                transaction.UnitCost);
        }

        var trimmedReason = reason.Trim();
        transaction.VoidedAt = clock.UtcNow;
        transaction.VoidedById = currentUser.UserId;
        transaction.VoidReason = trimmedReason;

        audit.Log(AuditActions.StockTransactionVoid, transaction.Id, oldValues: null, AuditValues(transaction), trimmedReason);
    }

    /// <summary>Giá trị ghi nhật ký của một giao dịch kho.</summary>
    public static object AuditValues(StockTransaction t) => new
    {
        t.SupplyId,
        t.Type,
        t.SeasonId,
        t.TransactionDate,
        t.Quantity,
        t.UnitCost,
        t.AverageUnitCostBefore,
        t.TotalCost,
    };

    public static AppException AlreadyVoided() =>
        new(ErrorCodes.AlreadyVoided, "Giao dịch kho này đã được hủy trước đó.");

    // BR-SUP-10: giá bình quân chỉ khôi phục đúng khi sau giao dịch không có gì tính trên nó, kể cả thao tác hủy.
    private async Task EnsureLastAsync(StockTransaction transaction, CancellationToken ct)
    {
        var createdAt = transaction.CreatedAt;
        var hasLaterOperation = await db.StockTransactions.AnyAsync(
            t => t.SupplyId == transaction.SupplyId
                && t.Id != transaction.Id
                && (t.CreatedAt >= createdAt || t.VoidedAt >= createdAt),
            ct);

        if (hasLaterOperation)
        {
            throw new AppException(
                ErrorCodes.StockTxNotLast,
                "Không hủy được vì sau giao dịch này vật tư đã có thao tác khác. Hãy tạo điều chỉnh tồn để sửa số lượng.");
        }
    }
}
