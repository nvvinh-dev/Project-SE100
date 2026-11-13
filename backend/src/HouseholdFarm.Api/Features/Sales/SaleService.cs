using HouseholdFarm.Api.Common.Auditing;
using HouseholdFarm.Api.Common.Auth;
using HouseholdFarm.Api.Common.Errors;
using HouseholdFarm.Api.Common.Persistence;
using HouseholdFarm.Api.Common.Responses;
using HouseholdFarm.Api.Data;
using HouseholdFarm.Api.Domain.Entities;
using HouseholdFarm.Api.Domain.Enums;
using HouseholdFarm.Api.Features.Harvests;
using HouseholdFarm.Api.Features.Seasons;
using Microsoft.EntityFrameworkCore;

namespace HouseholdFarm.Api.Features.Sales;

public sealed class SaleService(
    AppDbContext db,
    ICurrentUser currentUser,
    IAuditLogger audit,
    ISeasonStateGuard seasonStateGuard,
    IProduceStockService produceStock) : ISaleService
{
    public async Task<PagedResult<SaleListItem>> GetListAsync(SaleQuery query, CancellationToken ct)
    {
        currentUser.EnsureCanManageFinance();

        var sales = db.Sales.AsNoTracking();

        if (query.IncludeVoided != true)
        {
            sales = sales.Where(s => s.VoidedAt == null);
        }

        if (query.SeasonId is { } seasonId)
        {
            sales = sales.Where(s => s.SeasonId == seasonId);
        }

        if (query.CustomerId is { } customerId)
        {
            sales = sales.Where(s => s.CustomerId == customerId);
        }

        if (query.Channel is { } channel)
        {
            sales = sales.Where(s => s.Channel == channel);
        }

        if (query.From is { } from)
        {
            sales = sales.Where(s => s.SaleDate >= from);
        }

        if (query.To is { } to)
        {
            sales = sales.Where(s => s.SaleDate <= to);
        }

        var rows = SaleAmounts.Of(sales);

        // Trạng thái thanh toán là giá trị tính: lọc ngay trong truy vấn, không đọc ra rồi lọc trong bộ nhớ.
        rows = query.PaymentStatus switch
        {
            PaymentStatus.Paid => rows.Where(r => r.PaidAmount >= r.Revenue),
            PaymentStatus.Unpaid => rows.Where(r => r.PaidAmount < r.Revenue && r.PaidAmount == 0),
            PaymentStatus.PartiallyPaid => rows.Where(r => r.PaidAmount < r.Revenue && r.PaidAmount > 0),
            _ => rows,
        };

        return await rows
            .OrderByDescending(r => r.Sale.SaleDate)
            .ThenByDescending(r => r.Sale.CreatedAt)
            .ThenBy(r => r.Sale.Id)
            .Select(r => new SaleListItem(
                r.Sale.Id,
                r.Sale.SaleDate,
                r.Sale.SeasonId,
                r.Sale.Season.Crop.Name,
                r.Sale.Season.Plot.Code,
                r.Sale.CustomerId,
                r.Sale.Customer == null ? null : r.Sale.Customer.Name,
                r.Sale.Channel,
                r.Sale.ChannelOther,
                r.Sale.QualityGrade,
                r.Sale.Quantity,
                r.Sale.Season.Crop.YieldUnit,
                r.Revenue,
                r.PaidAmount,
                r.Revenue - r.PaidAmount,
                r.PaidAmount >= r.Revenue
                    ? PaymentStatus.Paid
                    : r.PaidAmount == 0 ? PaymentStatus.Unpaid : PaymentStatus.PartiallyPaid,
                r.Sale.VoidedAt))
            .ToPagedResultAsync(query, ct);
    }

    public async Task<SaleResponse> GetAsync(Guid id, CancellationToken ct)
    {
        currentUser.EnsureCanManageFinance();
        return await ReadResponseAsync(id, ct);
    }

    public async Task<SaleResponse> CreateAsync(CreateSaleRequest request, CancellationToken ct)
    {
        currentUser.EnsureCanManageFinance();

        // BR-GEN-10: yêu cầu gửi lại khi mùa vụ đã chốt vẫn phải nhận kết quả lần trước, nên tìm trước cả bước kiểm tra mùa vụ.
        if (await db.Sales.AsNoTracking().FindByRequestIdAsync(request.RequestId, ct) is { } earlier)
        {
            return await ReadResponseAsync(earlier.Id, ct);
        }

        // Validator đã bảo đảm các trường bắt buộc có giá trị.
        var seasonId = request.SeasonId!.Value;
        var qualityGrade = request.QualityGrade!.Value;
        var saleDate = request.SaleDate!.Value;
        var harvestRecordIds = (request.HarvestRecordIds ?? []).Distinct().ToList();

        await using var tx = await db.Database.BeginTransactionAsync(ct);

        // BR-GEN-12: khóa mùa vụ trước khi kiểm tra tồn, để hai giao dịch bán cùng lúc không cùng thấy đủ tồn.
        // BR-USR-13: quyền TC chọn được mùa vụ ngoài phân công; mùa vụ phải InProgress (mục 4.3).
        var season = await seasonStateGuard.EnsureOperationAllowedAsync(seasonId, SeasonOperation.ManageSales, forUpdate: true, ct);

        // BR-GEN-10: tìm lại sau khi khóa, yêu cầu trùng chạy song song đang chờ khóa phải thấy giao dịch vừa lưu.
        if (await db.Sales.AsNoTracking().FindByRequestIdAsync(request.RequestId, ct) is { } existing)
        {
            return await ReadResponseAsync(existing.Id, ct);
        }

        if (request.CustomerId is { } customerId)
        {
            await EnsureCustomerActiveAsync(customerId, ct);
        }

        EnsureSaleDateNotBeforeSeasonStart(saleDate, season.StartDate);

        // BR-SAL-01, BR-HAR-02: không bán vượt tồn của đúng cặp (mùa vụ, loại chất lượng).
        await produceStock.EnsureSufficientStockForSaleAsync(seasonId, qualityGrade, request.Quantity, currentSaleId: null, ct);

        // BR-SAL-05: phiếu cùng mùa vụ, cùng loại, chưa Void; không ràng buộc số lượng.
        await produceStock.ValidateHarvestLinksForSaleAsync(seasonId, qualityGrade, harvestRecordIds, ct);

        var channel = request.Channel;
        var sale = new Sale
        {
            SeasonId = seasonId,
            CustomerId = request.CustomerId,
            Channel = channel,
            ChannelOther = channel == SalesChannel.Other ? TrimToNull(request.ChannelOther) : null,
            QualityGrade = qualityGrade,
            Quantity = request.Quantity,
            UnitPrice = request.UnitPrice,
            SaleDate = saleDate,
            Note = TrimToNull(request.Note),
            RequestId = request.RequestId,
        };
        sale.HarvestLinks.AddRange(harvestRecordIds.Select(harvestRecordId => new SaleHarvestLink
        {
            SaleId = sale.Id,
            HarvestRecordId = harvestRecordId,
        }));
        db.Sales.Add(sale);

        audit.Log(AuditActions.SaleCreate, sale.Id, oldValues: null, AuditValues(sale, harvestRecordIds), reason: null);

        // Gặp yêu cầu trùng chạy song song thì hàm lưu đã hủy giao dịch database, trả bản ghi đã có và không commit (BR-GEN-10).
        if (await db.SaveChangesOrFindDuplicateAsync(sale, ct) is { } duplicate)
        {
            return await ReadResponseAsync(duplicate.Id, ct);
        }

        await tx.CommitAsync(ct);
        return await ReadResponseAsync(sale.Id, ct);
    }

    // BR-SAL-01, BR-GEN-13: khách hàng ngừng sử dụng không chọn được cho giao dịch bán mới.
    private async Task EnsureCustomerActiveAsync(Guid customerId, CancellationToken ct)
    {
        var customer = await db.Customers
            .AsNoTracking()
            .Where(c => c.Id == customerId)
            .Select(c => new { c.Name, c.Status })
            .SingleOrDefaultAsync(ct)
            ?? throw AppException.NotFound("khách hàng");

        if (customer.Status != RecordStatus.Active)
        {
            throw new AppException(
                ErrorCodes.CatalogInactive,
                $"Khách hàng {customer.Name} đang ngừng sử dụng, không chọn được cho giao dịch bán.");
        }
    }

    // BR-SAL-01: ngày bán không trước ngày bắt đầu mùa vụ. Cần ngày của mùa vụ nên kiểm tra ở service, không ở validator.
    private static void EnsureSaleDateNotBeforeSeasonStart(DateOnly saleDate, DateOnly seasonStartDate)
    {
        if (saleDate < seasonStartDate)
        {
            throw AppException.Validation(new Dictionary<string, string[]>
            {
                ["saleDate"] = [$"Ngày bán không được trước ngày bắt đầu mùa vụ ({seasonStartDate:dd/MM/yyyy})."],
            });
        }
    }

    private async Task<SaleResponse> ReadResponseAsync(Guid id, CancellationToken ct) =>
        await SaleAmounts.Of(db.Sales.AsNoTracking().Where(s => s.Id == id))
            .Select(r => new SaleResponse(
                r.Sale.Id,
                r.Sale.SeasonId,
                r.Sale.Season.Crop.Name,
                r.Sale.Season.Plot.Code,
                r.Sale.Season.StartDate,
                r.Sale.Season.Status,
                r.Sale.Season.Crop.YieldUnit,
                r.Sale.CustomerId,
                r.Sale.Customer == null ? null : r.Sale.Customer.Name,
                r.Sale.Channel,
                r.Sale.ChannelOther,
                r.Sale.QualityGrade,
                r.Sale.Quantity,
                r.Sale.UnitPrice,
                r.Sale.SaleDate,
                r.Sale.Note,
                r.Revenue,
                r.PaidAmount,
                r.Revenue - r.PaidAmount,
                r.PaidAmount >= r.Revenue
                    ? PaymentStatus.Paid
                    : r.PaidAmount == 0 ? PaymentStatus.Unpaid : PaymentStatus.PartiallyPaid,
                r.Sale.HarvestLinks
                    .OrderBy(l => l.HarvestRecord.HarvestDate)
                    .Select(l => new SaleHarvestLinkItem(l.HarvestRecordId, l.HarvestRecord.HarvestDate, l.HarvestRecord.Quantity))
                    .ToList(),
                r.Sale.VoidedAt,
                r.Sale.VoidReason,
                r.Sale.CreatedAt,
                r.Sale.Version))
            .SingleOrDefaultAsync(ct)
            ?? throw AppException.NotFound("giao dịch bán");

    private static object AuditValues(Sale s, IReadOnlyList<Guid> harvestRecordIds) => new
    {
        s.SeasonId,
        s.CustomerId,
        s.Channel,
        s.ChannelOther,
        s.QualityGrade,
        s.Quantity,
        s.UnitPrice,
        s.SaleDate,
        s.Note,
        HarvestRecordIds = harvestRecordIds,
    };

    private static string? TrimToNull(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
