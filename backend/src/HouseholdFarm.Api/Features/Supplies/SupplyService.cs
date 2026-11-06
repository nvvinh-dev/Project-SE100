using System.Linq.Expressions;
using HouseholdFarm.Api.Common.Auth;
using HouseholdFarm.Api.Common.Errors;
using HouseholdFarm.Api.Common.Persistence;
using HouseholdFarm.Api.Common.Responses;
using HouseholdFarm.Api.Data;
using HouseholdFarm.Api.Domain.Entities;
using HouseholdFarm.Api.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace HouseholdFarm.Api.Features.Supplies;

public sealed class SupplyService(AppDbContext db, ICurrentUser currentUser) : ISupplyService
{
    // BR-CAT-01: tiền tố mã vật tư tự sinh.
    private const string CodePrefix = "VT";

    // Độ dài cột supplies.code.
    private const int CodeMaxLength = 20;

    public async Task<PagedResult<SupplyListItem>> GetListAsync(SupplyQuery query, CancellationToken ct)
    {
        currentUser.EnsureNotWorker();

        var supplies = db.Supplies.AsNoTracking();

        if (query.Group is { } group)
        {
            supplies = supplies.Where(s => s.Group == group);
        }

        if (query.Status is { } status)
        {
            supplies = supplies.Where(s => s.Status == status);
        }

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var pattern = SearchPatterns.Contains(query.Search);
            supplies = supplies.Where(s =>
                EF.Functions.ILike(s.Code, pattern, SearchPatterns.EscapeCharacter)
                || EF.Functions.ILike(s.Name, pattern, SearchPatterns.EscapeCharacter));
        }

        var belowMin = query.BelowMin == true;
        var rows = supplies.WithStock(db.StockTransactions);
        if (belowMin)
        {
            // BR-SUP-12: chỉ vật tư Active. Ngưỡng không âm nên tồn âm luôn nằm trong điều kiện này.
            rows = rows.Where(r => r.Supply.Status == RecordStatus.Active && r.StockQuantity < r.Supply.MinStockLevel);
        }

        var canSeePrices = CanSeePrices();

        // Sắp thêm theo Id để thứ tự giữa các trang ổn định khi giá trị sắp xếp trùng nhau.
        return await Sort(rows.OrderBy(r => belowMin && r.StockQuantity < 0 ? 0 : 1), query.Sort)
            .ThenBy(r => r.Supply.Id)
            .Select(r => new SupplyListItem(
                r.Supply.Id,
                r.Supply.Code,
                r.Supply.Name,
                r.Supply.Group,
                r.Supply.Unit,
                r.Supply.MinStockLevel,
                r.StockQuantity,
                r.Supply.Status,
                canSeePrices ? r.Supply.ReferencePrice : null,
                canSeePrices ? r.Supply.AverageUnitCost : null))
            .ToPagedResultAsync(query, ct);
    }

    public async Task<SupplyResponse> GetAsync(Guid id, CancellationToken ct)
    {
        currentUser.EnsureNotWorker();

        return await db.Supplies
            .AsNoTracking()
            .WithStock(db.StockTransactions)
            .Where(r => r.Supply.Id == id)
            .Select(ToResponse(CanSeePrices()))
            .SingleOrDefaultAsync(ct)
            ?? throw AppException.NotFound("vật tư");
    }

    public async Task<SupplyResponse> CreateAsync(CreateSupplyRequest request, CancellationToken ct)
    {
        currentUser.EnsureOwner();

        // Không gán HouseholdId, CreatedAt, CreatedById: interceptor lấy từ người đang đăng nhập (BR-GEN-01).
        // Không nhận AverageUnitCost từ request: chỉ hệ thống cập nhật (BR-CAT-06).
        var supply = new Supply
        {
            Name = request.Name.Trim(),
            Group = request.Group!.Value,
            Unit = request.Unit!.Value,
            MinStockLevel = request.MinStockLevel,
            ReferencePrice = request.ReferencePrice,
            Status = RecordStatus.Active,
            Note = TrimToNull(request.Note),
        };
        db.Supplies.Add(supply);

        if (TrimToNull(request.Code) is { } code)
        {
            await EnsureCodeUnusedAsync(code, exceptId: null, ct);
            supply.Code = code;
            await SaveAsync(code, ct);
        }
        else
        {
            await SaveWithGeneratedCodeAsync(supply, ct);
        }

        return ToOwnerResponse(supply, stockQuantity: 0m, unitLocked: false);
    }

    public async Task<SupplyResponse> UpdateAsync(Guid id, UpdateSupplyRequest request, CancellationToken ct)
    {
        currentUser.EnsureOwner();

        var supply = await db.Supplies.SingleOrDefaultAsync(s => s.Id == id, ct) ?? throw AppException.NotFound("vật tư");

        // BR-GEN-11: người khác đã sửa sau lần đọc của người này thì lưu báo CONCURRENCY_CONFLICT, không ghi đè.
        db.Entry(supply).Property(s => s.Version).OriginalValue = request.Version;

        var code = request.Code.Trim();
        if (code != supply.Code)
        {
            await EnsureCodeUnusedAsync(code, exceptId: supply.Id, ct);
        }

        // Validator đã bảo đảm các enum có giá trị.
        var unit = request.Unit!.Value;
        var unitLocked = await db.StockTransactions.AnyAsync(t => t.SupplyId == id, ct);
        if (unitLocked && unit != supply.Unit)
        {
            // BR-CAT-05: giao dịch cũ tính theo đơn vị cũ, hệ thống không quy đổi giữa các đơn vị.
            throw new AppException(
                ErrorCodes.UnitLocked,
                $"Vật tư {supply.Name} đã có giao dịch kho nên không đổi được đơn vị tính.");
        }

        supply.Code = code;
        supply.Name = request.Name.Trim();
        supply.Group = request.Group!.Value;
        supply.Unit = unit;
        supply.MinStockLevel = request.MinStockLevel;
        supply.ReferencePrice = request.ReferencePrice;
        supply.Status = request.Status!.Value;
        supply.Note = TrimToNull(request.Note);
        await SaveAsync(code, ct);

        var stockQuantity = await db.StockTransactions.StockQuantityOfAsync(id, ct);
        return ToOwnerResponse(supply, stockQuantity, unitLocked);
    }

    // BR-CAT-01: kiểm tra trước để báo lỗi rõ ràng; ràng buộc duy nhất (household_id, code) chặn nốt khi tạo cùng lúc.
    private async Task EnsureCodeUnusedAsync(string code, Guid? exceptId, CancellationToken ct)
    {
        if (await db.Supplies.AnyAsync(s => s.Code == code && s.Id != exceptId, ct))
        {
            throw CodeAlreadyUsed(code);
        }
    }

    // Mã tự sinh có thể trùng khi hai người cùng tạo một lúc: sinh lại mã rồi lưu lại, người dùng không thấy lỗi.
    private async Task SaveWithGeneratedCodeAsync(Supply supply, CancellationToken ct)
    {
        for (var attempt = 1; ; attempt++)
        {
            supply.Code = await CatalogCodes.NextAsync(db.Supplies.Select(s => s.Code), CodePrefix, CodeMaxLength, ct);
            try
            {
                await db.SaveChangesAsync(ct);
                return;
            }
            catch (DbUpdateException ex) when (IsCodeViolation(ex))
            {
                // Thử lại với mã mới; vật tư vẫn ở trạng thái chờ thêm trong DbContext.
                if (attempt == CatalogCodes.MaxGenerateAttempts)
                {
                    throw CodeAlreadyUsed(supply.Code);
                }
            }
        }
    }

    private async Task SaveAsync(string code, CancellationToken ct)
    {
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (IsCodeViolation(ex))
        {
            throw CodeAlreadyUsed(code);
        }
    }

    private bool IsCodeViolation(DbUpdateException ex) =>
        ex.IsUniqueViolation<Supply>(db, nameof(Supply.HouseholdId), nameof(Supply.Code));

    // Ma trận phân quyền: Thành viên hộ không có quyền Kho vẫn xem được số lượng tồn, nhưng không thấy giá.
    private bool CanSeePrices() => currentUser.HasPermission(ExtraPermission.ManageStock);

    // Tham số sort đã được SupplyQueryValidator kiểm tra, chỉ còn các trường trong SupplyQuery.SortFields.
    private static IOrderedQueryable<SupplyWithStock> Sort(IOrderedQueryable<SupplyWithStock> rows, string? sort)
    {
        var descending = sort?.StartsWith('-') == true;
        return sort?.TrimStart('-') switch
        {
            "name" => descending ? rows.ThenByDescending(r => r.Supply.Name) : rows.ThenBy(r => r.Supply.Name),
            "stockQuantity" => descending ? rows.ThenByDescending(r => r.StockQuantity) : rows.ThenBy(r => r.StockQuantity),
            "createdAt" => descending ? rows.ThenByDescending(r => r.Supply.CreatedAt) : rows.ThenBy(r => r.Supply.CreatedAt),
            _ => descending ? rows.ThenByDescending(r => r.Supply.Code) : rows.ThenBy(r => r.Supply.Code),
        };
    }

    private static Expression<Func<SupplyWithStock, SupplyResponse>> ToResponse(bool canSeePrices) => r => new SupplyResponse(
        r.Supply.Id,
        r.Supply.Code,
        r.Supply.Name,
        r.Supply.Group,
        r.Supply.Unit,
        r.Supply.MinStockLevel,
        r.StockQuantity,
        r.Supply.Status,
        r.Supply.Note,
        r.HasStockTransactions,
        canSeePrices ? r.Supply.ReferencePrice : null,
        canSeePrices ? r.Supply.AverageUnitCost : null,
        r.Supply.Version);

    // Chỉ Chủ hộ tạo, sửa được vật tư nên phản hồi sau khi lưu luôn có giá.
    private static SupplyResponse ToOwnerResponse(Supply supply, decimal stockQuantity, bool unitLocked) => new(
        supply.Id,
        supply.Code,
        supply.Name,
        supply.Group,
        supply.Unit,
        supply.MinStockLevel,
        stockQuantity,
        supply.Status,
        supply.Note,
        unitLocked,
        supply.ReferencePrice,
        supply.AverageUnitCost,
        supply.Version);

    private static string? TrimToNull(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static AppException CodeAlreadyUsed(string code) =>
        new(ErrorCodes.CodeAlreadyUsed, $"Mã vật tư {code} đã có trong hộ. Hãy dùng mã khác.");
}
