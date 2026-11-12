using System.Linq.Expressions;
using HouseholdFarm.Api.Common.Auditing;
using HouseholdFarm.Api.Common.Auth;
using HouseholdFarm.Api.Common.Errors;
using HouseholdFarm.Api.Common.Persistence;
using HouseholdFarm.Api.Common.Responses;
using HouseholdFarm.Api.Common.Time;
using HouseholdFarm.Api.Data;
using HouseholdFarm.Api.Domain.Entities;
using HouseholdFarm.Api.Domain.Enums;
using HouseholdFarm.Api.Features.Seasons;
using Microsoft.EntityFrameworkCore;

namespace HouseholdFarm.Api.Features.Expenses;

public sealed class ExpenseService(
    AppDbContext db,
    ICurrentUser currentUser,
    IAppClock clock,
    IAuditLogger audit,
    ISeasonStateGuard seasonStateGuard) : IExpenseService
{
    // Chuyển entity sang DTO bằng biểu thức để EF dịch thẳng ra SQL, đọc kèm nhãn mùa vụ trong cùng một truy vấn.
    private static readonly Expression<Func<Expense, ExpenseResponse>> _toResponse = e => new ExpenseResponse(
        e.Id,
        e.SeasonId,
        e.Season == null ? null : e.Season.Crop.Name,
        e.Season == null ? null : e.Season.Plot.Code,
        e.Season == null ? null : e.Season.StartDate,
        e.Season == null ? null : e.Season.Status,
        e.Category,
        e.CategoryOther,
        e.Amount,
        e.ExpenseDate,
        e.Note,
        e.VoidedAt,
        e.VoidReason,
        e.CreatedAt,
        e.Version);

    public async Task<PagedResult<ExpenseListItem>> GetListAsync(ExpenseQuery query, CancellationToken ct)
    {
        // TC-20 (a): Nhân công và Thành viên không có TC không xem được chi phí.
        currentUser.EnsureCanManageFinance();

        var expenses = db.Expenses.AsNoTracking();

        if (query.IncludeVoided != true)
        {
            expenses = expenses.Where(e => e.VoidedAt == null);
        }

        if (query.Category is { } category)
        {
            expenses = expenses.Where(e => e.Category == category);
        }

        if (query.General == true)
        {
            expenses = expenses.Where(e => e.SeasonId == null);
        }
        else if (query.SeasonId is { } seasonId)
        {
            expenses = expenses.Where(e => e.SeasonId == seasonId);
        }

        if (query.From is { } from)
        {
            expenses = expenses.Where(e => e.ExpenseDate >= from);
        }

        if (query.To is { } to)
        {
            expenses = expenses.Where(e => e.ExpenseDate <= to);
        }

        return await expenses
            .OrderByDescending(e => e.ExpenseDate)
            .ThenByDescending(e => e.CreatedAt)
            .ThenBy(e => e.Id)
            .Select(e => new ExpenseListItem(
                e.Id,
                e.Category,
                e.CategoryOther,
                e.Amount,
                e.ExpenseDate,
                e.SeasonId,
                e.Season == null ? null : e.Season.Crop.Name,
                e.Season == null ? null : e.Season.Plot.Code,
                e.Season == null ? null : e.Season.StartDate,
                e.Note,
                e.VoidedAt))
            .ToPagedResultAsync(query, ct);
    }

    public async Task<ExpenseResponse> GetAsync(Guid id, CancellationToken ct)
    {
        currentUser.EnsureCanManageFinance();
        return await ReadResponseAsync(id, ct);
    }

    public async Task<ExpenseResponse> CreateAsync(CreateExpenseRequest request, CancellationToken ct)
    {
        currentUser.EnsureCanManageFinance();

        // Yêu cầu gửi lại khi mùa vụ đã chốt vẫn phải nhận kết quả lần trước, nên tìm trước cả bước kiểm tra mùa vụ.
        if (await db.Expenses.AsNoTracking().FindByRequestIdAsync(request.RequestId, ct) is { } earlier)
        {
            return await ReadResponseAsync(earlier.Id, ct);
        }

        await using var tx = await db.Database.BeginTransactionAsync(ct);

        if (request.SeasonId is { } seasonId)
        {
            // BR-EXP-02: chỉ ghi cho mùa vụ InProgress; BR-USR-13: quyền TC chọn được mùa vụ ngoài phân công.
            // Khóa mùa vụ để nó không bị chốt, hủy giữa lúc kiểm tra và lúc ghi.
            await seasonStateGuard.EnsureOperationAllowedAsync(seasonId, SeasonOperation.ManageExpenses, forUpdate: true, ct);

            // Tìm lại sau khi khóa: yêu cầu trùng chạy song song đang chờ khóa phải thấy khoản chi vừa lưu (BR-GEN-10).
            if (await db.Expenses.AsNoTracking().FindByRequestIdAsync(request.RequestId, ct) is { } existing)
            {
                return await ReadResponseAsync(existing.Id, ct);
            }
        }

        // Validator đã bảo đảm nhóm và ngày có giá trị.
        var category = request.Category!.Value;
        var expense = new Expense
        {
            SeasonId = request.SeasonId,
            Category = category,
            CategoryOther = CategoryOtherOf(category, request.CategoryOther),
            Amount = request.Amount,
            ExpenseDate = request.ExpenseDate!.Value,
            Note = TrimToNull(request.Note),
            RequestId = request.RequestId,
        };
        db.Expenses.Add(expense);

        audit.Log(AuditActions.ExpenseCreate, expense.Id, oldValues: null, AuditValues(expense), reason: null);

        // Gặp yêu cầu trùng chạy song song thì hàm lưu đã hủy giao dịch database, trả bản ghi đã có và không commit (BR-GEN-10).
        if (await db.SaveChangesOrFindDuplicateAsync(expense, ct) is { } duplicate)
        {
            return await ReadResponseAsync(duplicate.Id, ct);
        }

        await tx.CommitAsync(ct);
        return await ReadResponseAsync(expense.Id, ct);
    }

    public async Task<ExpenseResponse> CorrectAsync(Guid id, CorrectExpenseRequest request, CancellationToken ct)
    {
        currentUser.EnsureCanManageFinance();

        var targetSeasonId = await ReadTargetSeasonIdAsync(id, ct);

        await using var tx = await db.Database.BeginTransactionAsync(ct);

        // BR-EXP-02: đổi mùa vụ thì cả mùa vụ cũ và mùa vụ mới đều phải cho phép.
        await LockSeasonsAsync([targetSeasonId, request.SeasonId], ct);
        var expense = await LockExpenseAsync(id, ct);

        // BR-GEN-11: người khác đã sửa, hủy sau lần đọc của người này thì không ghi đè.
        if (expense.Version != request.Version || expense.SeasonId != targetSeasonId)
        {
            throw ConcurrencyConflict();
        }

        db.Entry(expense).Property(e => e.Version).OriginalValue = request.Version;

        // BR-EXP-03: chỉ sửa mùa vụ, nhóm, số tiền, ngày phát sinh, ghi chú.
        var category = request.Category!.Value;
        expense.SeasonId = request.SeasonId;
        expense.Category = category;
        expense.CategoryOther = CategoryOtherOf(category, request.CategoryOther);
        expense.Amount = request.Amount;
        expense.ExpenseDate = request.ExpenseDate!.Value;
        expense.Note = TrimToNull(request.Note);

        // BR-GEN-08: nhật ký chỉ lưu các trường đổi; không có gì đổi thì không ghi.
        var changes = ChangedValues.Of(db.Entry(expense));
        if (!changes.IsEmpty)
        {
            audit.Log(AuditActions.ExpenseCorrect, expense.Id, changes.OldValues, changes.NewValues, request.Reason.Trim());
            await db.SaveChangesAsync(ct);
        }

        await tx.CommitAsync(ct);
        return await ReadResponseAsync(id, ct);
    }

    public async Task<ExpenseResponse> VoidAsync(Guid id, VoidExpenseRequest request, CancellationToken ct)
    {
        currentUser.EnsureCanManageFinance();

        var targetSeasonId = await ReadTargetSeasonIdAsync(id, ct);

        await using var tx = await db.Database.BeginTransactionAsync(ct);

        // BR-EXP-02: hủy chi phí gắn mùa vụ theo trạng thái mùa vụ; chi phí chung không phụ thuộc.
        await LockSeasonsAsync([targetSeasonId], ct);
        var expense = await LockExpenseAsync(id, ct);

        // Khoản chi vừa được chuyển sang mùa vụ khác trong lúc chờ khóa: mùa vụ đã kiểm tra không còn đúng.
        if (expense.SeasonId != targetSeasonId)
        {
            throw ConcurrencyConflict();
        }

        // BR-GEN-09: điền ba cột Void, bản ghi bị loại khỏi mọi phép tính tổng.
        var reason = request.Reason.Trim();
        expense.VoidedAt = clock.UtcNow;
        expense.VoidedById = currentUser.UserId;
        expense.VoidReason = reason;

        audit.Log(AuditActions.ExpenseVoid, expense.Id, oldValues: null, AuditValues(expense), reason);

        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return await ReadResponseAsync(id, ct);
    }

    // Đọc trước khi khóa để biết mùa vụ cần khóa (BR-GEN-12: khóa seasons trước). Báo đã hủy trước khi kiểm tra mùa vụ:
    // hủy lần hai thì lý do thật là ở đây, không phải vì mùa vụ đã chốt.
    private async Task<Guid?> ReadTargetSeasonIdAsync(Guid id, CancellationToken ct)
    {
        var target = await db.Expenses
            .AsNoTracking()
            .Where(e => e.Id == id)
            .Select(e => new { e.SeasonId, e.VoidedAt })
            .SingleOrDefaultAsync(ct)
            ?? throw AppException.NotFound("khoản chi");

        if (target.VoidedAt is not null)
        {
            throw AlreadyVoided();
        }

        return target.SeasonId;
    }

    // Khóa theo thứ tự id để hai thao tác cùng chuyển khoản chi giữa hai mùa vụ không khóa chéo nhau.
    private async Task LockSeasonsAsync(IEnumerable<Guid?> seasonIds, CancellationToken ct)
    {
        foreach (var seasonId in seasonIds.OfType<Guid>().Distinct().Order())
        {
            await seasonStateGuard.EnsureOperationAllowedAsync(seasonId, SeasonOperation.ManageExpenses, forUpdate: true, ct);
        }
    }

    // Khóa dòng khoản chi sau mùa vụ: hai người cùng hủy thì người sau thấy đã hủy, không ghi đè lý do của người trước.
    private async Task<Expense> LockExpenseAsync(Guid id, CancellationToken ct)
    {
        var expense = await db.Expenses
            .FromSql($"SELECT *, xmin FROM expenses WHERE id = {id} FOR UPDATE")
            .SingleOrDefaultAsync(ct)
            ?? throw AppException.NotFound("khoản chi");

        if (expense.VoidedAt is not null)
        {
            throw AlreadyVoided();
        }

        return expense;
    }

    private async Task<ExpenseResponse> ReadResponseAsync(Guid id, CancellationToken ct) =>
        await db.Expenses.AsNoTracking().Where(e => e.Id == id).Select(_toResponse).SingleOrDefaultAsync(ct)
            ?? throw AppException.NotFound("khoản chi");

    private static object AuditValues(Expense e) => new
    {
        e.SeasonId,
        e.Category,
        e.CategoryOther,
        e.Amount,
        e.ExpenseDate,
        e.Note,
    };

    // Ghi chú nhóm chỉ có nghĩa với nhóm "Khác"; đổi sang nhóm khác thì bỏ ghi chú cũ.
    private static string? CategoryOtherOf(ExpenseCategory category, string? categoryOther) =>
        category == ExpenseCategory.Other ? TrimToNull(categoryOther) : null;

    private static string? TrimToNull(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static AppException AlreadyVoided() =>
        new(ErrorCodes.AlreadyVoided, "Khoản chi này đã được hủy trước đó.");

    private static AppException ConcurrencyConflict() =>
        new(ErrorCodes.ConcurrencyConflict, "Khoản chi đã bị người khác sửa hoặc hủy. Hãy tải lại rồi thử lại.");
}
