using HouseholdFarm.Api.Common.Errors;
using HouseholdFarm.Api.Data;
using HouseholdFarm.Api.Domain.Enums;
using HouseholdFarm.Api.Features.Harvests;
using HouseholdFarm.Api.Tests.Infrastructure;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace HouseholdFarm.Api.Tests.Features.Sales;

/// <summary>
/// Ứng dụng thật, chỉ thay <see cref="IProduceStockService"/> bằng <see cref="TestProduceStockService"/> khi bản thật
/// của module thu hoạch chưa có. TC-11, TC-12 vẫn chạy với <see cref="ApiFactory"/> để kiểm tra trên bản thật.
/// </summary>
public sealed class SalesApiFactory(PostgresFixture postgres) : ApiFactory(postgres)
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IProduceStockService>();
            services.AddScoped<IProduceStockService, TestProduceStockService>();
        });
    }
}

/// <summary>
/// Bản thay thế trong test: tính tồn theo công thức của mô hình dữ liệu (thu − bán − điều chỉnh giảm, bỏ bản ghi đã Void)
/// và kiểm tra phiếu liên kết, để test của bán hàng không phụ thuộc lúc bản thật hoàn tất.
/// </summary>
internal sealed class TestProduceStockService(AppDbContext db) : IProduceStockService
{
    public Task<IReadOnlyList<ProduceStockResponse>> GetStockAsync(Guid seasonId, CancellationToken ct) =>
        throw new NotSupportedException();

    public Task<IReadOnlyList<ProduceStockResponse>> GetStockAsync(IReadOnlyCollection<Guid> seasonIds, CancellationToken ct) =>
        throw new NotSupportedException();

    public async Task EnsureSufficientStockForSaleAsync(
        Guid seasonId,
        QualityGrade qualityGrade,
        decimal quantity,
        Guid? currentSaleId,
        CancellationToken ct)
    {
        var harvested = await db.HarvestRecords
            .Where(h => h.SeasonId == seasonId && h.QualityGrade == qualityGrade && h.VoidedAt == null)
            .SumAsync(h => h.Quantity, ct);
        var sold = await db.Sales
            .Where(s => s.SeasonId == seasonId && s.QualityGrade == qualityGrade && s.VoidedAt == null && s.Id != currentSaleId)
            .SumAsync(s => s.Quantity, ct);
        var adjusted = await db.ProduceStockAdjustments
            .Where(a => a.SeasonId == seasonId && a.QualityGrade == qualityGrade && a.VoidedAt == null)
            .SumAsync(a => a.Quantity, ct);
        var available = harvested - sold - adjusted;

        if (quantity > available)
        {
            throw new AppException(
                ErrorCodes.InsufficientProduceStock,
                $"Không đủ tồn nông sản: còn {available}, cần {quantity}.",
                new { seasonId, qualityGrade, available, requested = quantity });
        }
    }

    public Task EnsureZeroStockAsync(Guid seasonId, CancellationToken ct) => throw new NotSupportedException();

    public async Task ValidateHarvestLinksForSaleAsync(
        Guid seasonId,
        QualityGrade qualityGrade,
        IReadOnlyCollection<Guid> harvestRecordIds,
        CancellationToken ct)
    {
        var ids = harvestRecordIds.Distinct().ToList();
        if (ids.Count == 0)
        {
            return;
        }

        var valid = await db.HarvestRecords
            .Where(h => ids.Contains(h.Id) && h.SeasonId == seasonId && h.QualityGrade == qualityGrade && h.VoidedAt == null)
            .Select(h => h.Id)
            .ToListAsync(ct);
        var invalid = ids.Except(valid).ToList();
        if (invalid.Count > 0)
        {
            throw new AppException(
                ErrorCodes.ValidationError,
                "Có phiếu thu hoạch không cùng mùa vụ, cùng loại chất lượng hoặc đã bị hủy.",
                new { invalidHarvestRecordIds = invalid });
        }
    }
}
