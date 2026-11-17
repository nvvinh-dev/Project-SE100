using System.Globalization;
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

namespace HouseholdFarm.Api.Features.Harvests;

public sealed class ProduceStockAdjustmentService(
    AppDbContext db,
    ICurrentUser currentUser,
    IAppClock clock,
    IAuditLogger audit,
    ISeasonStateGuard seasonStateGuard,
    IProduceStockService produceStockService) : IProduceStockAdjustmentService
{
    private static readonly CultureInfo VietnameseCulture = new("vi-VN");

    public async Task<PagedResult<ProduceStockAdjustmentResponse>> GetListAsync(
        ProduceStockAdjustmentQuery query,
        CancellationToken ct)
    {
        // FR-HAR-07: chỉ Chủ hộ được xem lịch sử và thực hiện điều chỉnh tồn nông sản.
        currentUser.EnsureOwner();

        var adjustments = db.ProduceStockAdjustments.AsNoTracking();

        if (query.IncludeVoided == false)
        {
            adjustments = adjustments.Where(a => a.VoidedAt == null);
        }

        if (query.SeasonId is { } seasonId)
        {
            adjustments = adjustments.Where(a => a.SeasonId == seasonId);
        }

        if (query.QualityGrade is { } qualityGrade)
        {
            adjustments = adjustments.Where(a => a.QualityGrade == qualityGrade);
        }

        return await adjustments
            .OrderByDescending(a => a.AdjustmentDate)
            .ThenByDescending(a => a.CreatedAt)
            .ThenBy(a => a.Id)
            .Select(a => new ProduceStockAdjustmentResponse(
                a.Id,
                a.SeasonId,
                a.Season.Crop.Name,
                a.Season.Plot.Code,
                a.Season.StartDate,
                a.Season.Status,
                a.Season.Crop.YieldUnit,
                a.QualityGrade,
                a.Quantity,
                a.AdjustmentDate,
                a.Reason,
                a.ReasonOther,
                a.Note,
                a.VoidedAt,
                a.VoidReason,
                a.CreatedAt))
            .ToPagedResultAsync(query, ct);
    }

    public async Task<ProduceStockAdjustmentResponse> CreateAsync(
        CreateProduceStockAdjustmentRequest request,
        CancellationToken ct)
    {
        currentUser.EnsureOwner();

        // BR-GEN-10: yêu cầu gửi lại trả bản ghi cũ mà không trừ tồn hai lần.
        if (await db.ProduceStockAdjustments.AsNoTracking().FindByRequestIdAsync(request.RequestId, ct) is { } earlier)
        {
            return await ReadResponseAsync(earlier.Id, ct);
        }

        await using var tx = await db.Database.BeginTransactionAsync(ct);

        // BR-SEA-10, BR-GEN-12: khóa dòng mùa vụ (InProgress) trước khi đọc và kiểm tra tồn nông sản.
        await seasonStateGuard.EnsureOperationAllowedAsync(
            request.SeasonId,
            SeasonOperation.AdjustProduceStock,
            forUpdate: true,
            ct);

        if (await db.ProduceStockAdjustments.AsNoTracking().FindByRequestIdAsync(request.RequestId, ct) is { } existing)
        {
            return await ReadResponseAsync(existing.Id, ct);
        }

        var grade = request.QualityGrade!.Value;
        var stocks = await produceStockService.GetStockAsync(request.SeasonId, ct);
        var available = stocks.First(s => s.QualityGrade == grade).Quantity;

        // BR-HAR-05, BR-HAR-02: chỉ kiểm tra đúng cặp (mùa vụ, loại chất lượng), không vượt tồn hiện có.
        if (request.Quantity > available)
        {
            throw new AppException(
                ErrorCodes.InsufficientProduceStock,
                $"Tồn nông sản {FormatGrade(grade)} không đủ để điều chỉnh giảm (còn {available.ToString("#,##0.###", VietnameseCulture)}, cần giảm {request.Quantity.ToString("#,##0.###", VietnameseCulture)}).",
                new
                {
                    seasonId = request.SeasonId,
                    qualityGrade = grade,
                    available,
                    requested = request.Quantity,
                });
        }

        var reason = request.Reason!.Value;
        var adjustment = new ProduceStockAdjustment
        {
            SeasonId = request.SeasonId,
            QualityGrade = grade,
            Quantity = request.Quantity,
            AdjustmentDate = request.AdjustmentDate!.Value,
            Reason = reason,
            ReasonOther = reason == ProduceAdjustmentReason.Other ? TrimToNull(request.ReasonOther) : null,
            Note = TrimToNull(request.Note),
            RequestId = request.RequestId,
        };
        db.ProduceStockAdjustments.Add(adjustment);

        audit.Log(
            AuditActions.ProduceStockAdjustmentAdjust,
            adjustment.Id,
            oldValues: null,
            AuditValues(adjustment),
            BuildAuditReason(adjustment));

        if (await db.SaveChangesOrFindDuplicateAsync(adjustment, ct) is { } duplicate)
        {
            return await ReadResponseAsync(duplicate.Id, ct);
        }

        await tx.CommitAsync(ct);
        return await ReadResponseAsync(adjustment.Id, ct);
    }

    public async Task<ProduceStockAdjustmentResponse> VoidAsync(
        Guid id,
        VoidProduceStockAdjustmentRequest request,
        CancellationToken ct)
    {
        currentUser.EnsureOwner();

        var target = await db.ProduceStockAdjustments
            .AsNoTracking()
            .Where(a => a.Id == id)
            .Select(a => new { a.SeasonId, a.VoidedAt })
            .SingleOrDefaultAsync(ct)
            ?? throw AppException.NotFound("phiếu điều chỉnh tồn nông sản");

        if (target.VoidedAt is not null)
        {
            throw AlreadyVoided();
        }

        await using var tx = await db.Database.BeginTransactionAsync(ct);

        // BR-GEN-12: khóa dòng mùa vụ trước dòng điều chỉnh để tránh deadlock.
        await seasonStateGuard.EnsureOperationAllowedAsync(
            target.SeasonId,
            SeasonOperation.AdjustProduceStock,
            forUpdate: true,
            ct);

        var adjustment = await db.ProduceStockAdjustments
            .FromSql($"SELECT *, xmin FROM produce_stock_adjustments WHERE id = {id} FOR UPDATE")
            .SingleOrDefaultAsync(ct)
            ?? throw AppException.NotFound("phiếu điều chỉnh tồn nông sản");

        if (adjustment.VoidedAt is not null)
        {
            throw AlreadyVoided();
        }

        // BR-HAR-05: hủy phiếu điều chỉnh giảm làm tồn tăng lại nên không cần kiểm tra tồn âm.
        var reason = request.Reason.Trim();
        adjustment.VoidedAt = clock.UtcNow;
        adjustment.VoidedById = currentUser.UserId;
        adjustment.VoidReason = reason;

        audit.Log(
            AuditActions.ProduceStockAdjustmentVoid,
            adjustment.Id,
            oldValues: null,
            AuditValues(adjustment),
            reason);

        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);

        return await ReadResponseAsync(adjustment.Id, ct);
    }

    private async Task<ProduceStockAdjustmentResponse> ReadResponseAsync(Guid id, CancellationToken ct) =>
        await db.ProduceStockAdjustments
            .AsNoTracking()
            .Where(a => a.Id == id)
            .Select(a => new ProduceStockAdjustmentResponse(
                a.Id,
                a.SeasonId,
                a.Season.Crop.Name,
                a.Season.Plot.Code,
                a.Season.StartDate,
                a.Season.Status,
                a.Season.Crop.YieldUnit,
                a.QualityGrade,
                a.Quantity,
                a.AdjustmentDate,
                a.Reason,
                a.ReasonOther,
                a.Note,
                a.VoidedAt,
                a.VoidReason,
                a.CreatedAt))
            .SingleOrDefaultAsync(ct)
            ?? throw AppException.NotFound("phiếu điều chỉnh tồn nông sản");

    private static AppException AlreadyVoided() =>
        new(ErrorCodes.AlreadyVoided, "Phiếu điều chỉnh tồn nông sản này đã bị hủy trước đó.");

    private static object AuditValues(ProduceStockAdjustment adjustment) => new
    {
        seasonId = adjustment.SeasonId,
        qualityGrade = adjustment.QualityGrade,
        quantity = adjustment.Quantity,
        adjustmentDate = adjustment.AdjustmentDate,
        reason = adjustment.Reason,
        reasonOther = adjustment.ReasonOther,
        note = adjustment.Note,
        voidedAt = adjustment.VoidedAt,
        voidReason = adjustment.VoidReason,
    };

    private static string BuildAuditReason(ProduceStockAdjustment adjustment) =>
        adjustment.Reason == ProduceAdjustmentReason.Other && !string.IsNullOrWhiteSpace(adjustment.ReasonOther)
            ? adjustment.ReasonOther
            : FormatReason(adjustment.Reason);

    private static string FormatReason(ProduceAdjustmentReason reason) => reason switch
    {
        ProduceAdjustmentReason.Loss => "Hao hụt",
        ProduceAdjustmentReason.Damaged => "Hư hỏng",
        ProduceAdjustmentReason.HouseholdUse => "Gia đình sử dụng",
        ProduceAdjustmentReason.KeptForSeed => "Để giống",
        ProduceAdjustmentReason.Other => "Khác",
        _ => reason.ToString(),
    };

    private static string FormatGrade(QualityGrade grade) => grade switch
    {
        QualityGrade.Grade1 => "Loại 1",
        QualityGrade.Grade2 => "Loại 2",
        QualityGrade.Grade3 => "Loại 3",
        _ => grade.ToString(),
    };

    private static string? TrimToNull(string? text)
    {
        var trimmed = text?.Trim();
        return string.IsNullOrEmpty(trimmed) ? null : trimmed;
    }
}
