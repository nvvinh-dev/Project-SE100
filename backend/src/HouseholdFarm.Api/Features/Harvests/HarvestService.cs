using HouseholdFarm.Api.Common.Auditing;
using HouseholdFarm.Api.Common.Auth;
using HouseholdFarm.Api.Common.Errors;
using HouseholdFarm.Api.Common.Persistence;
using HouseholdFarm.Api.Common.Responses;
using HouseholdFarm.Api.Common.Time;
using HouseholdFarm.Api.Data;
using HouseholdFarm.Api.Domain.Entities;
using HouseholdFarm.Api.Domain.Enums;
using HouseholdFarm.Api.Features.Sales;
using HouseholdFarm.Api.Features.Seasons;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HouseholdFarm.Api.Features.Harvests;

public sealed class HarvestService(
    AppDbContext db,
    ICurrentUser currentUser,
    IAppClock clock,
    IAuditLogger audit,
    ISeasonStateGuard seasonStateGuard,
    IProduceStockService produceStockService,
    ISaleHarvestLinkService saleHarvestLinkService) : IHarvestService
{
    public async Task<PagedResult<HarvestRecordListItem>> GetListAsync(HarvestRecordQuery query, CancellationToken ct)
    {
        // Ma trận phân quyền: Nhân công và Quản trị không xem được phiếu thu hoạch.
        currentUser.EnsureNotWorker();

        var forSaleLink = query.ForSaleLink == true;
        // BR-USR-13: Chủ hộ xem cả hộ; thành viên xem mùa vụ được phân công; người có ManageFinance chỉ mở rộng khi chọn liên kết bán (forSaleLink=true).
        var harvests = forSaleLink
            ? db.HarvestRecords.AsNoTracking().VisibleTo(currentUser, ExtraPermission.ManageFinance)
            : db.HarvestRecords.AsNoTracking().VisibleTo(currentUser);

        if (forSaleLink || query.IncludeVoided != true)
        {
            harvests = harvests.Where(h => h.VoidedAt == null);
        }

        if (query.SeasonId is { } seasonId)
        {
            harvests = harvests.Where(h => h.SeasonId == seasonId);
        }

        if (query.QualityGrade is { } qualityGrade)
        {
            harvests = harvests.Where(h => h.QualityGrade == qualityGrade);
        }

        if (query.From is { } from)
        {
            harvests = harvests.Where(h => h.HarvestDate >= from);
        }

        if (query.To is { } to)
        {
            harvests = harvests.Where(h => h.HarvestDate <= to);
        }

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var pattern = SearchPatterns.Contains(query.Search);
            harvests = harvests.Where(h =>
                EF.Functions.ILike(h.Season.Crop.Name, pattern, SearchPatterns.EscapeCharacter)
                || EF.Functions.ILike(h.Season.Plot.Code, pattern, SearchPatterns.EscapeCharacter)
                || (h.Note != null && EF.Functions.ILike(h.Note, pattern, SearchPatterns.EscapeCharacter)));
        }

        return await harvests
            .OrderByDescending(h => h.HarvestDate)
            .ThenByDescending(h => h.CreatedAt)
            .ThenBy(h => h.Id)
            .Select(h => new HarvestRecordListItem(
                h.Id,
                h.SeasonId,
                h.Season.Crop.Name,
                h.Season.Plot.Code,
                h.Season.StartDate,
                h.Season.Crop.YieldUnit,
                h.HarvestDate,
                h.QualityGrade,
                h.Quantity,
                h.Note,
                h.VoidedAt))
            .ToPagedResultAsync(query, ct);
    }

    public async Task<HarvestRecordResponse> GetAsync(Guid id, CancellationToken ct)
    {
        currentUser.EnsureNotWorker();
        return await ReadResponseAsync(id, allowFinanceScope: false, ct);
    }

    public async Task<HarvestRecordResponse> CreateAsync(CreateHarvestRecordRequest request, CancellationToken ct)
    {
        currentUser.EnsureNotWorker();

        // BR-GEN-10: yêu cầu gửi lại vẫn trả bản ghi lần trước dù mùa vụ hiện tại đã chốt.
        if (await db.HarvestRecords.AsNoTracking().VisibleTo(currentUser).FindByRequestIdAsync(request.RequestId, ct) is { } earlier)
        {
            return await ReadResponseAsync(earlier.Id, allowFinanceScope: false, ct);
        }

        await using var tx = await db.Database.BeginTransactionAsync(ct);

        // BR-HAR-01, BR-SEA-10, BR-GEN-12: chỉ Chủ hộ hoặc thành viên trong phân công mùa vụ ghi được trên mùa vụ InProgress.
        var season = await seasonStateGuard.EnsureOperationAllowedAsync(
            request.SeasonId,
            SeasonOperation.ManageHarvests,
            forUpdate: true,
            ct);

        // Kiểm tra lại sau khi khóa dòng mùa vụ để yêu cầu trùng chạy song song nhận bản ghi vừa lưu.
        if (await db.HarvestRecords.AsNoTracking().FindByRequestIdAsync(request.RequestId, ct) is { } existing)
        {
            return await ReadResponseAsync(existing.Id, allowFinanceScope: false, ct);
        }

        var harvestDate = request.HarvestDate!.Value;
        // BR-HAR-01: ngày thu hoạch không được trước ngày bắt đầu mùa vụ.
        if (harvestDate < season.StartDate)
        {
            throw new AppException(
                ErrorCodes.ValidationError,
                $"Ngày thu hoạch không được trước ngày bắt đầu mùa vụ ({season.StartDate:dd/MM/yyyy}).");
        }

        var record = new HarvestRecord
        {
            SeasonId = request.SeasonId,
            HarvestDate = harvestDate,
            QualityGrade = request.QualityGrade!.Value,
            Quantity = request.Quantity,
            Note = TrimToNull(request.Note),
            RequestId = request.RequestId,
        };
        db.HarvestRecords.Add(record);

        audit.Log(AuditActions.HarvestRecordCreate, record.Id, oldValues: null, AuditValues(record), reason: null);

        if (await db.SaveChangesOrFindDuplicateAsync(record, ct) is { } duplicate)
        {
            return await ReadResponseAsync(duplicate.Id, allowFinanceScope: false, ct);
        }

        await tx.CommitAsync(ct);
        return await ReadResponseAsync(record.Id, allowFinanceScope: false, ct);
    }

    public async Task<HarvestRecordResponse> CorrectAsync(Guid id, CorrectHarvestRecordRequest request, CancellationToken ct)
    {
        currentUser.EnsureNotWorker();

        var targetSeasonId = await ReadTargetSeasonIdAsync(id, ct);

        await using var tx = await db.Database.BeginTransactionAsync(ct);

        // BR-GEN-12: khóa dòng mùa vụ trước khi khóa phiếu thu hoạch và kiểm tra tồn nông sản.
        var season = await seasonStateGuard.EnsureOperationAllowedAsync(
            targetSeasonId,
            SeasonOperation.ManageHarvests,
            forUpdate: true,
            ct);

        var record = await LockHarvestRecordAsync(id, ct);

        // BR-GEN-11: chặn ghi đè khi người khác vừa sửa hoặc hủy.
        if (record.Version != request.Version || record.SeasonId != targetSeasonId)
        {
            throw ConcurrencyConflict();
        }

        db.Entry(record).Property(r => r.Version).OriginalValue = request.Version;

        var newDate = request.HarvestDate!.Value;
        if (newDate < season.StartDate)
        {
            throw new AppException(
                ErrorCodes.ValidationError,
                $"Ngày thu hoạch không được trước ngày bắt đầu mùa vụ ({season.StartDate:dd/MM/yyyy}).");
        }

        var oldGrade = record.QualityGrade;
        var oldQuantity = record.Quantity;
        var newGrade = request.QualityGrade!.Value;
        var newQuantity = request.Quantity;

        // BR-HAR-02, BR-HAR-03: không thao tác nào được làm tồn nông sản của loại chất lượng cũ xuống âm.
        var removedFromOldGrade = newGrade != oldGrade ? oldQuantity : Math.Max(0m, oldQuantity - newQuantity);
        if (removedFromOldGrade > 0m)
        {
            await EnsureStockRemainsNonNegativeAsync(record.SeasonId, oldGrade, removedFromOldGrade, ct);
        }

        record.HarvestDate = newDate;
        record.QualityGrade = newGrade;
        record.Quantity = newQuantity;
        record.Note = TrimToNull(request.Note);

        // BR-GEN-08: đọc các trường đổi trước SaveChangesAsync; không có gì đổi thì không ghi nhật ký.
        var changes = ChangedValues.Of(db.Entry(record));
        if (changes.IsEmpty)
        {
            await tx.CommitAsync(ct);
            return await ReadResponseAsync(id, allowFinanceScope: false, ct);
        }

        IReadOnlyList<Guid> unlinkedSaleIds = [];
        if (newGrade != oldGrade)
        {
            // BR-HAR-03: đổi loại chất lượng thì tự gỡ liên kết giao dịch bán cũ của phiếu thu.
            unlinkedSaleIds = await RemoveLinksSafeAsync(record.Id, ct);
        }

        object newValues = changes.NewValues;
        if (unlinkedSaleIds.Count > 0)
        {
            var merged = new Dictionary<string, object?>(changes.NewValues)
            {
                ["unlinkedSaleIds"] = unlinkedSaleIds,
            };
            newValues = merged;
        }

        audit.Log(AuditActions.HarvestRecordCorrect, record.Id, changes.OldValues, newValues, request.Reason.Trim());
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);

        return await ReadResponseAsync(id, allowFinanceScope: false, ct);
    }

    public async Task<HarvestRecordResponse> VoidAsync(Guid id, VoidHarvestRecordRequest request, CancellationToken ct)
    {
        currentUser.EnsureNotWorker();

        var targetSeasonId = await ReadTargetSeasonIdAsync(id, ct);

        await using var tx = await db.Database.BeginTransactionAsync(ct);

        // BR-GEN-12: khóa mùa vụ trước khi kiểm tra tồn nông sản và khóa phiếu thu.
        await seasonStateGuard.EnsureOperationAllowedAsync(
            targetSeasonId,
            SeasonOperation.ManageHarvests,
            forUpdate: true,
            ct);

        var record = await LockHarvestRecordAsync(id, ct);
        if (record.SeasonId != targetSeasonId)
        {
            throw ConcurrencyConflict();
        }

        // BR-HAR-02, BR-HAR-04: không cho hủy nếu làm tồn của cặp (mùa vụ, loại chất lượng) xuống âm.
        await EnsureStockRemainsNonNegativeAsync(record.SeasonId, record.QualityGrade, record.Quantity, ct);

        var reason = request.Reason.Trim();
        record.VoidedAt = clock.UtcNow;
        record.VoidedById = currentUser.UserId;
        record.VoidReason = reason;

        // BR-HAR-04: tự gỡ các liên kết giao dịch bán của phiếu thu vừa hủy.
        var unlinkedSaleIds = await RemoveLinksSafeAsync(record.Id, ct);

        audit.Log(AuditActions.HarvestRecordVoid, record.Id, oldValues: null, AuditValues(record, unlinkedSaleIds), reason);

        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);

        return await ReadResponseAsync(id, allowFinanceScope: false, ct);
    }

    private async Task<Guid> ReadTargetSeasonIdAsync(Guid id, CancellationToken ct)
    {
        // Chỉ người có quyền sửa/hủy trong phạm vi mùa vụ (không mở rộng bằng ManageFinance) mới thấy ở bước ghi.
        var target = await db.HarvestRecords
            .AsNoTracking()
            .VisibleTo(currentUser)
            .Where(h => h.Id == id)
            .Select(h => new { h.SeasonId, h.VoidedAt })
            .SingleOrDefaultAsync(ct)
            ?? throw AppException.NotFound("phiếu thu hoạch");

        if (target.VoidedAt is not null)
        {
            throw AlreadyVoided();
        }

        return target.SeasonId;
    }

    private async Task<HarvestRecord> LockHarvestRecordAsync(Guid id, CancellationToken ct)
    {
        var record = await db.HarvestRecords
            .FromSql($"SELECT *, xmin FROM harvest_records WHERE id = {id} FOR UPDATE")
            .SingleOrDefaultAsync(ct)
            ?? throw AppException.NotFound("phiếu thu hoạch");

        if (record.VoidedAt is not null)
        {
            throw AlreadyVoided();
        }

        return record;
    }

    private async Task EnsureStockRemainsNonNegativeAsync(
        Guid seasonId,
        QualityGrade qualityGrade,
        decimal removedQuantity,
        CancellationToken ct)
    {
        var stocks = await produceStockService.GetStockAsync(seasonId, ct);
        var currentStock = stocks.First(s => s.QualityGrade == qualityGrade).Quantity;
        var resultingStock = currentStock - removedQuantity;
        if (resultingStock < 0m)
        {
            throw new AppException(
                ErrorCodes.ProduceStockNegative,
                $"Không thể thực hiện thao tác vì làm tồn nông sản {FormatGrade(qualityGrade)} xuống âm ({resultingStock:#,##0.###}).",
                new
                {
                    seasonId,
                    qualityGrade,
                    currentStock,
                    resultingStock,
                });
        }
    }

    private async Task<IReadOnlyList<Guid>> RemoveLinksSafeAsync(Guid harvestRecordId, CancellationToken ct)
    {
        try
        {
            return await saleHarvestLinkService.RemoveByHarvestRecordAsync(harvestRecordId, ct);
        }
        catch (NotImplementedException)
        {
            // Tạm thời cho FR-SAL-03: xóa khối try/catch này khi SaleHarvestLinkService hoàn thiện ở FR-SAL-03.
            if (await db.SaleHarvestLinks.AnyAsync(l => l.HarvestRecordId == harvestRecordId, ct))
            {
                throw;
            }

            return [];
        }
    }

    private async Task<HarvestRecordResponse> ReadResponseAsync(Guid id, bool allowFinanceScope, CancellationToken ct)
    {
        var query = db.HarvestRecords.AsNoTracking();
        query = allowFinanceScope
            ? query.VisibleTo(currentUser, ExtraPermission.ManageFinance)
            : query.VisibleTo(currentUser);

        var data = await query
            .Where(h => h.Id == id)
            .Select(h => new
            {
                h.Id,
                h.SeasonId,
                CropName = h.Season.Crop.Name,
                PlotCode = h.Season.Plot.Code,
                SeasonStartDate = h.Season.StartDate,
                SeasonStatus = h.Season.Status,
                YieldUnit = h.Season.Crop.YieldUnit,
                h.HarvestDate,
                h.QualityGrade,
                h.Quantity,
                h.Note,
                h.VoidedAt,
                h.VoidReason,
                h.CreatedAt,
                h.Version,
            })
            .SingleOrDefaultAsync(ct)
            ?? throw AppException.NotFound("phiếu thu hoạch");

        var isInModifyScope = currentUser.Role == UserRole.Owner || currentUser.AssignedSeasonIds.Contains(data.SeasonId);
        var canModify = isInModifyScope && data.VoidedAt is null && data.SeasonStatus == SeasonStatus.InProgress;

        // Chỉ Chủ hộ và người có ManageFinance xem được thông tin giao dịch bán liên kết.
        IReadOnlyList<HarvestLinkedSaleItem> linkedSales = [];
        if (currentUser.HasPermission(ExtraPermission.ManageFinance))
        {
            linkedSales = await db.SaleHarvestLinks
                .AsNoTracking()
                .Where(l => l.HarvestRecordId == id && l.Sale.VoidedAt == null)
                .OrderByDescending(l => l.Sale.SaleDate)
                .Select(l => new HarvestLinkedSaleItem(
                    l.SaleId,
                    l.Sale.SaleDate,
                    l.Sale.Quantity,
                    l.Sale.Customer == null ? null : l.Sale.Customer.Name,
                    l.Sale.Channel))
                .ToListAsync(ct);
        }

        return new HarvestRecordResponse(
            data.Id,
            data.SeasonId,
            data.CropName,
            data.PlotCode,
            data.SeasonStartDate,
            data.SeasonStatus,
            data.YieldUnit,
            data.HarvestDate,
            data.QualityGrade,
            data.Quantity,
            data.Note,
            data.VoidedAt,
            data.VoidReason,
            data.CreatedAt,
            data.Version,
            canModify,
            linkedSales);
    }

    private static object AuditValues(HarvestRecord r, IReadOnlyList<Guid>? unlinkedSaleIds = null)
    {
        if (unlinkedSaleIds is { Count: > 0 })
        {
            return new
            {
                r.SeasonId,
                r.HarvestDate,
                r.QualityGrade,
                r.Quantity,
                r.Note,
                unlinkedSaleIds,
            };
        }

        return new
        {
            r.SeasonId,
            r.HarvestDate,
            r.QualityGrade,
            r.Quantity,
            r.Note,
        };
    }

    private static string? TrimToNull(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string FormatGrade(QualityGrade grade) => grade switch
    {
        QualityGrade.Grade1 => "Loại 1",
        QualityGrade.Grade2 => "Loại 2",
        QualityGrade.Grade3 => "Loại 3",
        _ => grade.ToString(),
    };

    private static AppException AlreadyVoided() =>
        new(ErrorCodes.AlreadyVoided, "Phiếu thu hoạch này đã được hủy trước đó.");

    private static AppException ConcurrencyConflict() =>
        new(ErrorCodes.ConcurrencyConflict, "Phiếu thu hoạch đã bị người khác sửa hoặc hủy. Hãy tải lại rồi thử lại.");
}
