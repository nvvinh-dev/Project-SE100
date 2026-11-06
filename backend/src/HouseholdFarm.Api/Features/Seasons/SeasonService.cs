using HouseholdFarm.Api.Common.Auth;
using HouseholdFarm.Api.Common.Errors;
using HouseholdFarm.Api.Common.Persistence;
using HouseholdFarm.Api.Data;
using HouseholdFarm.Api.Domain.Entities;
using HouseholdFarm.Api.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace HouseholdFarm.Api.Features.Seasons;

public sealed class SeasonService(AppDbContext db, ICurrentUser currentUser) : ISeasonService
{
    public async Task<SeasonResponse> CreateAsync(CreateSeasonRequest request, CancellationToken ct)
    {
        // Ma trận phân quyền: chỉ Chủ hộ có quyền tạo mùa vụ
        currentUser.EnsureOwner();

        // BR-SEA-01, BR-GEN-13: Thửa đất và cây trồng phải tồn tại trong hộ và đang ở trạng thái Active
        var plot = await EnsurePlotActiveAsync(request.PlotId, ct);
        var crop = await EnsureCropActiveAsync(request.CropId, ct);

        // BR-SEA-02, BR-SEA-04: Kiểm tra chồng lấn mùa vụ lớp 1 trên cùng thửa
        await EnsureNoPlannedOverlapAsync(request.PlotId, request.StartDate, request.ExpectedHarvestDate, excludeSeasonId: null, ct);

        var season = new Season
        {
            PlotId = request.PlotId,
            CropId = request.CropId,
            StartDate = request.StartDate,
            ExpectedHarvestDate = request.ExpectedHarvestDate,
            TargetYield = request.TargetYield,
            Status = SeasonStatus.Planned,
        };

        db.Seasons.Add(season);
        await db.SaveChangesAsync(ct);

        return new SeasonResponse(
            season.Id,
            plot.Id,
            plot.Code,
            crop.Id,
            crop.Name,
            crop.YieldUnit,
            season.StartDate,
            season.ExpectedHarvestDate,
            season.TargetYield,
            season.Status,
            season.Version);
    }

    private async Task<Plot> EnsurePlotActiveAsync(Guid plotId, CancellationToken ct)
    {
        var plot = await db.Plots
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == plotId, ct)
            ?? throw AppException.NotFound("thửa đất");

        // BR-SEA-01, BR-GEN-13: Thửa đất phải ở trạng thái Active khi chọn cho mùa vụ
        if (plot.Status != RecordStatus.Active)
        {
            throw new AppException(
                ErrorCodes.CatalogInactive,
                $"Thửa đất '{plot.Code}' đang ngừng sử dụng, không thể chọn cho mùa vụ.",
                new { plotId = plot.Id, plotCode = plot.Code });
        }

        return plot;
    }

    private async Task<Crop> EnsureCropActiveAsync(Guid cropId, CancellationToken ct)
    {
        var crop = await db.Crops
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == cropId, ct)
            ?? throw AppException.NotFound("cây trồng");

        // BR-SEA-01, BR-GEN-13: Cây trồng phải ở trạng thái Active khi chọn cho mùa vụ
        if (crop.Status != RecordStatus.Active)
        {
            throw new AppException(
                ErrorCodes.CatalogInactive,
                $"Cây trồng '{crop.Name}' đang ngừng sử dụng, không thể chọn cho mùa vụ.",
                new { cropId = crop.Id, cropName = crop.Name });
        }

        return crop;
    }

    public async Task<SeasonResponse> GetAsync(Guid id, CancellationToken ct)
    {
        currentUser.EnsureNotWorker();

        var season = await db.Seasons
            .AsNoTracking()
            .VisibleTo(currentUser)
            .Where(s => s.Id == id)
            .Select(s => new SeasonResponse(
                s.Id,
                s.PlotId,
                s.Plot.Code,
                s.CropId,
                s.Crop.Name,
                s.Crop.YieldUnit,
                s.StartDate,
                s.ExpectedHarvestDate,
                s.TargetYield,
                s.Status,
                s.Version))
            .FirstOrDefaultAsync(ct)
            ?? throw AppException.NotFound("mùa vụ");

        return season;
    }

    public async Task EnsureNoPlannedOverlapAsync(
        Guid plotId,
        DateOnly startDate,
        DateOnly expectedHarvestDate,
        Guid? excludeSeasonId,
        CancellationToken ct)
    {
        // BR-SEA-04: Bỏ qua kiểm tra khi hộ bật AllowOverlappingSeasons
        if (await IsAllowOverlappingSeasonsAsync(ct))
        {
            return;
        }

        // BR-SEA-02: So khoảng [startDate, expectedHarvestDate] với các mùa vụ khác đang Planned hoặc InProgress trên cùng thửa
        var conflict = await db.Seasons
            .AsNoTracking()
            .Where(s => s.PlotId == plotId
                        && (excludeSeasonId == null || s.Id != excludeSeasonId)
                        && (s.Status == SeasonStatus.Planned || s.Status == SeasonStatus.InProgress)
                        && s.StartDate <= expectedHarvestDate
                        && startDate <= s.ExpectedHarvestDate)
            .Select(s => new
            {
                s.Id,
                PlotCode = s.Plot.Code,
                CropName = s.Crop.Name,
                s.StartDate,
                s.ExpectedHarvestDate,
            })
            .FirstOrDefaultAsync(ct);

        if (conflict is not null)
        {
            throw new AppException(
                ErrorCodes.SeasonOverlap,
                $"Thửa đất '{conflict.PlotCode}' đã có mùa vụ '{conflict.CropName}' ({conflict.StartDate:dd/MM/yyyy} – {conflict.ExpectedHarvestDate:dd/MM/yyyy}) trùng thời gian.",
                new
                {
                    conflictingSeasonId = conflict.Id,
                    plotCode = conflict.PlotCode,
                    cropName = conflict.CropName,
                    startDate = conflict.StartDate,
                    expectedHarvestDate = conflict.ExpectedHarvestDate,
                });
        }
    }

    public async Task EnsurePlotNotBusyAsync(
        Guid plotId,
        Guid? excludeSeasonId,
        CancellationToken ct)
    {
        // BR-SEA-04: Bỏ qua kiểm tra khi hộ bật AllowOverlappingSeasons
        if (await IsAllowOverlappingSeasonsAsync(ct))
        {
            return;
        }

        // BR-SEA-03: Kiểm tra thửa có mùa vụ khác đang InProgress không
        var busy = await db.Seasons
            .AsNoTracking()
            .Where(s => s.PlotId == plotId
                        && (excludeSeasonId == null || s.Id != excludeSeasonId)
                        && s.Status == SeasonStatus.InProgress)
            .Select(s => new
            {
                s.Id,
                PlotCode = s.Plot.Code,
                CropName = s.Crop.Name,
                s.StartDate,
                s.ExpectedHarvestDate,
            })
            .FirstOrDefaultAsync(ct);

        if (busy is not null)
        {
            throw new AppException(
                ErrorCodes.SeasonPlotBusy,
                $"Thửa đất '{busy.PlotCode}' đang có mùa vụ '{busy.CropName}' đang canh tác, chưa thể bắt đầu hoặc mở lại mùa vụ khác.",
                new
                {
                    busySeasonId = busy.Id,
                    plotCode = busy.PlotCode,
                    cropName = busy.CropName,
                    startDate = busy.StartDate,
                    expectedHarvestDate = busy.ExpectedHarvestDate,
                });
        }
    }

    private async Task<bool> IsAllowOverlappingSeasonsAsync(CancellationToken ct)
    {
        if (currentUser.HouseholdId is null)
        {
            return false;
        }

        return await db.Households
            .AsNoTracking()
            .Where(h => h.Id == currentUser.HouseholdId.Value)
            .Select(h => h.Settings.AllowOverlappingSeasons)
            .FirstOrDefaultAsync(ct);
    }
}

