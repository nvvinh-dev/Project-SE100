using HouseholdFarm.Api.Common.Auditing;
using HouseholdFarm.Api.Common.Auth;
using HouseholdFarm.Api.Common.Errors;
using HouseholdFarm.Api.Common.Persistence;
using HouseholdFarm.Api.Common.Time;
using HouseholdFarm.Api.Data;
using HouseholdFarm.Api.Domain.Entities;
using HouseholdFarm.Api.Features.Seasons;
using Microsoft.EntityFrameworkCore;

namespace HouseholdFarm.Api.Features.Sales;

public sealed class PaymentService(
    AppDbContext db,
    ICurrentUser currentUser,
    IAppClock clock,
    IAuditLogger audit,
    ISeasonStateGuard seasonStateGuard) : IPaymentService
{
    public async Task<PaymentResponse> CreateAsync(CreatePaymentRequest request, CancellationToken ct)
    {
        currentUser.EnsureCanManageFinance();

        // BR-GEN-10: yêu cầu gửi lại sau khi thanh toán đủ vẫn phải nhận kết quả lần trước, nên tìm trước mọi kiểm tra.
        if (await db.Payments.AsNoTracking().FindByRequestIdAsync(request.RequestId, ct) is { } earlier)
        {
            return await ReadResponseAsync(earlier.Id, ct);
        }

        // Validator đã bảo đảm có giao dịch bán và ngày.
        var saleId = request.SaleId!.Value;
        var seasonId = await ReadSeasonIdAsync(saleId, ct);

        await using var tx = await db.Database.BeginTransactionAsync(ct);

        // Mục 4.3: ghi thanh toán được khi mùa vụ InProgress hoặc Closed. Khóa seasons trước sales (BR-GEN-12).
        await seasonStateGuard.EnsureOperationAllowedAsync(seasonId, SeasonOperation.RecordPayment, forUpdate: true, ct);

        // BR-GEN-12: khóa giao dịch bán trước khi tính công nợ, để hai lần ghi cùng lúc không cùng vượt công nợ.
        var sale = await LockSaleAsync(saleId, ct);

        // BR-GEN-10: tìm lại sau khi khóa, yêu cầu trùng chạy song song đang chờ khóa phải thấy thanh toán vừa lưu.
        if (await db.Payments.AsNoTracking().FindByRequestIdAsync(request.RequestId, ct) is { } existing)
        {
            return await ReadResponseAsync(existing.Id, ct);
        }

        if (sale.VoidedAt is not null)
        {
            throw new AppException(ErrorCodes.AlreadyVoided, "Giao dịch bán này đã bị hủy, không ghi thanh toán được nữa.");
        }

        // BR-SAL-06: 0 < amount <= công nợ còn lại; thanh toán đã Void không tính vào đã thu.
        var outstanding = await OutstandingAsync(sale, ct);
        if (request.Amount > outstanding)
        {
            throw new AppException(
                ErrorCodes.PaymentExceedsOutstanding,
                $"Số tiền thanh toán {request.Amount:N0} ₫ vượt công nợ còn lại {outstanding:N0} ₫.",
                new { outstanding, requested = request.Amount });
        }

        var payment = new Payment
        {
            SaleId = saleId,
            Amount = request.Amount,
            PaymentDate = request.PaymentDate!.Value,
            Note = string.IsNullOrWhiteSpace(request.Note) ? null : request.Note.Trim(),
            RequestId = request.RequestId,
        };
        db.Payments.Add(payment);

        audit.Log(AuditActions.PaymentCreate, payment.Id, oldValues: null, AuditValues(payment), reason: null);

        if (await db.SaveChangesOrFindDuplicateAsync(payment, ct) is { } duplicate)
        {
            return await ReadResponseAsync(duplicate.Id, ct);
        }

        await tx.CommitAsync(ct);
        return await ReadResponseAsync(payment.Id, ct);
    }

    public async Task<PaymentResponse> VoidAsync(Guid id, VoidPaymentRequest request, CancellationToken ct)
    {
        currentUser.EnsureCanManageFinance();

        var target = await db.Payments
            .AsNoTracking()
            .Where(p => p.Id == id)
            .Select(p => new { p.SaleId, p.Sale.SeasonId, p.VoidedAt })
            .SingleOrDefaultAsync(ct)
            ?? throw AppException.NotFound("thanh toán");

        // Báo đã hủy trước khi kiểm tra mùa vụ: hủy lần hai thì lý do thật là ở đây.
        if (target.VoidedAt is not null)
        {
            throw AlreadyVoided();
        }

        await using var tx = await db.Database.BeginTransactionAsync(ct);

        // Mục 4.3: hủy thanh toán được khi mùa vụ InProgress hoặc Closed. Khóa seasons, rồi sales (BR-GEN-12).
        await seasonStateGuard.EnsureOperationAllowedAsync(target.SeasonId, SeasonOperation.RecordPayment, forUpdate: true, ct);
        await LockSaleAsync(target.SaleId, ct);

        var payment = await db.Payments.SingleAsync(p => p.Id == id, ct);
        // Hai người cùng hủy: người sau thấy đã hủy, không ghi đè lý do của người trước.
        if (payment.VoidedAt is not null)
        {
            throw AlreadyVoided();
        }

        var reason = request.Reason.Trim();
        payment.VoidedAt = clock.UtcNow;
        payment.VoidedById = currentUser.UserId;
        payment.VoidReason = reason;

        audit.Log(AuditActions.PaymentVoid, payment.Id, oldValues: null, AuditValues(payment), reason);

        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return await ReadResponseAsync(id, ct);
    }

    private async Task<Guid> ReadSeasonIdAsync(Guid saleId, CancellationToken ct) =>
        await db.Sales.AsNoTracking().Where(s => s.Id == saleId).Select(s => (Guid?)s.SeasonId).SingleOrDefaultAsync(ct)
            ?? throw AppException.NotFound("giao dịch bán");

    private async Task<Sale> LockSaleAsync(Guid saleId, CancellationToken ct) =>
        await db.Sales.FromSql($"SELECT *, xmin FROM sales WHERE id = {saleId} FOR UPDATE").SingleOrDefaultAsync(ct)
            ?? throw AppException.NotFound("giao dịch bán");

    // Đọc sau khi đã khóa dòng sales, nên không ai ghi, hủy thanh toán của giao dịch này xen vào giữa.
    private async Task<decimal> OutstandingAsync(Sale sale, CancellationToken ct)
    {
        var paid = await db.Payments.Where(p => p.SaleId == sale.Id && p.VoidedAt == null).SumAsync(p => p.Amount, ct);
        return Math.Round(sale.Quantity * sale.UnitPrice, 2) - paid;
    }

    private async Task<PaymentResponse> ReadResponseAsync(Guid id, CancellationToken ct) =>
        await db.Payments
            .AsNoTracking()
            .Where(p => p.Id == id)
            .Select(p => new PaymentResponse(p.Id, p.SaleId, p.Amount, p.PaymentDate, p.Note, p.VoidedAt, p.VoidReason, p.CreatedAt))
            .SingleOrDefaultAsync(ct)
            ?? throw AppException.NotFound("thanh toán");

    private static object AuditValues(Payment p) => new { p.SaleId, p.Amount, p.PaymentDate, p.Note };

    private static AppException AlreadyVoided() =>
        new(ErrorCodes.AlreadyVoided, "Thanh toán này đã được hủy trước đó.");
}
