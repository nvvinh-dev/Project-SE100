using HouseholdFarm.Api.Common.Auth;
using HouseholdFarm.Api.Common.Errors;
using HouseholdFarm.Api.Common.Persistence;
using HouseholdFarm.Api.Common.Responses;
using HouseholdFarm.Api.Data;
using HouseholdFarm.Api.Domain.Entities;
using HouseholdFarm.Api.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace HouseholdFarm.Api.Features.Seasons;

public sealed class SeasonService(AppDbContext db, ICurrentUser currentUser) : ISeasonService
{
    public async Task<PagedResult<SeasonListItem>> GetListAsync(SeasonQuery query, CancellationToken ct)
    {
        // Ma trận phân quyền: Nhân công không được xem danh sách mùa vụ
        currentUser.EnsureNotWorker();

        // Lọc theo phạm vi làm việc của người dùng (Chủ hộ thấy cả hộ, Thành viên chỉ thấy mùa vụ được phân công)
        var seasons = db.Seasons.AsNoTracking().VisibleTo(currentUser);

        if (query.PlotId.HasValue)
        {
            seasons = seasons.Where(s => s.PlotId == query.PlotId.Value);
        }

        if (query.CropId.HasValue)
        {
            seasons = seasons.Where(s => s.CropId == query.CropId.Value);
        }

        if (query.Status.HasValue)
        {
            seasons = seasons.Where(s => s.Status == query.Status.Value);
        }

        // Lọc các mùa vụ có khoảng [StartDate, ExpectedHarvestDate] giao với [From, To]
        if (query.From.HasValue)
        {
            seasons = seasons.Where(s => s.ExpectedHarvestDate >= query.From.Value);
        }

        if (query.To.HasValue)
        {
            seasons = seasons.Where(s => s.StartDate <= query.To.Value);
        }

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var pattern = SearchPatterns.Contains(query.Search);
            seasons = seasons.Where(s =>
                EF.Functions.ILike(s.Plot.Code, pattern, SearchPatterns.EscapeCharacter)
                || EF.Functions.ILike(s.Crop.Name, pattern, SearchPatterns.EscapeCharacter));
        }

        return await Sort(seasons, query.Sort)
            .ThenBy(s => s.Id)
            .Select(s => new SeasonListItem(
                s.Id,
                s.PlotId,
                s.Plot.Code,
                s.CropId,
                s.Crop.Name,
                s.Crop.YieldUnit,
                s.StartDate,
                s.ExpectedHarvestDate,
                s.TargetYield,
                s.Status))
            .ToPagedResultAsync(query, ct);
    }

    public async Task<IReadOnlyList<SeasonOptionItem>> GetOptionsAsync(SeasonOptionQuery query, CancellationToken ct)
    {
        // Ma trận phân quyền: Nhân công không được xem danh sách mùa vụ
        currentUser.EnsureNotWorker();

        if (query.Operation is null)
        {
            throw new AppException(ErrorCodes.ValidationError, "Vui lòng chọn loại thao tác.");
        }

        var (allowedStatuses, outOfScopePermission) = SeasonStateRules.GetRule(query.Operation.Value);

        // BR-USR-13: Thành viên có quyền Kho hoặc TC thấy thêm mùa vụ ngoài phân công khi thực hiện thao tác tương ứng
        var seasons = db.Seasons.AsNoTracking();
        seasons = outOfScopePermission.HasValue
            ? seasons.VisibleTo(currentUser, outOfScopePermission.Value)
            : seasons.VisibleTo(currentUser);

        // Lọc theo các trạng thái được phép thao tác theo BR-SEA-10
        var items = await seasons
            .Where(s => allowedStatuses.Contains(s.Status))
            .OrderByDescending(s => s.StartDate)
            .ThenBy(s => s.Id)
            .Select(s => new
            {
                s.Id,
                s.Status,
                s.PlotId,
                PlotCode = s.Plot.Code,
                s.CropId,
                CropName = s.Crop.Name,
                s.Crop.YieldUnit,
                s.StartDate,
                s.ExpectedHarvestDate,
            })
            .ToListAsync(ct);

        return items.Select(s => new SeasonOptionItem(
            s.Id,
            $"{s.CropName} · {s.PlotCode} · {s.StartDate:dd/MM/yyyy}",
            s.Status,
            s.PlotId,
            s.PlotCode,
            s.CropId,
            s.CropName,
            s.YieldUnit,
            s.StartDate,
            s.ExpectedHarvestDate)).ToList();
    }

    private static IOrderedQueryable<Season> Sort(IQueryable<Season> seasons, string? sort)
    {
        var descending = sort?.StartsWith('-') == true;
        return sort?.TrimStart('-') switch
        {
            "expectedHarvestDate" => descending ? seasons.OrderByDescending(s => s.ExpectedHarvestDate) : seasons.OrderBy(s => s.ExpectedHarvestDate),
            "targetYield" => descending ? seasons.OrderByDescending(s => s.TargetYield) : seasons.OrderBy(s => s.TargetYield),
            "createdAt" => descending ? seasons.OrderByDescending(s => s.CreatedAt) : seasons.OrderBy(s => s.CreatedAt),
            _ => (sort is null || descending) ? seasons.OrderByDescending(s => s.StartDate) : seasons.OrderBy(s => s.StartDate),
        };
    }
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

    public async Task<SeasonResponse> UpdateAsync(Guid id, UpdateSeasonRequest request, CancellationToken ct)
    {
        // Ma trận phân quyền: chỉ Chủ hộ có quyền sửa mùa vụ
        currentUser.EnsureOwner();

        var season = await db.Seasons
            .VisibleTo(currentUser)
            .Include(s => s.Plot)
            .Include(s => s.Crop)
            .FirstOrDefaultAsync(s => s.Id == id, ct)
            ?? throw AppException.NotFound("mùa vụ");

        // BR-SEA-10: Kiểm tra trạng thái theo bảng quy tắc trạng thái
        if (season.Status is SeasonStatus.Closed or SeasonStatus.Cancelled)
        {
            throw new AppException(ErrorCodes.SeasonInvalidState, "Không thể sửa mùa vụ đã chốt hoặc đã hủy.");
        }

        if (season.Status == SeasonStatus.InProgress && (request.PlotId != season.PlotId || request.CropId != season.CropId))
        {
            throw new AppException(
                ErrorCodes.SeasonInvalidState,
                "Mùa vụ đang canh tác chỉ được sửa ngày và mục tiêu sản lượng, không được đổi thửa đất hoặc cây trồng.");
        }

        var plotChanged = request.PlotId != season.PlotId;
        var dateChanged = request.StartDate != season.StartDate || request.ExpectedHarvestDate != season.ExpectedHarvestDate;

        // BR-SEA-01, BR-GEN-13: Khi đổi thửa hoặc đổi cây, thửa/cây mới phải Active
        if (plotChanged)
        {
            var newPlot = await EnsurePlotActiveAsync(request.PlotId, ct);
            season.PlotId = newPlot.Id;
            season.Plot = newPlot;
        }

        if (request.CropId != season.CropId)
        {
            var newCrop = await EnsureCropActiveAsync(request.CropId, ct);
            season.CropId = newCrop.Id;
            season.Crop = newCrop;
        }

        // BR-SEA-02: Kiểm tra chồng lấn kế hoạch khi đổi ngày hoặc thửa (bỏ qua chính mùa vụ đang sửa)
        if (plotChanged || dateChanged)
        {
            await EnsureNoPlannedOverlapAsync(request.PlotId, request.StartDate, request.ExpectedHarvestDate, excludeSeasonId: season.Id, ct);
        }

        season.StartDate = request.StartDate;
        season.ExpectedHarvestDate = request.ExpectedHarvestDate;
        season.TargetYield = request.TargetYield;

        // BR-GEN-11: Kiểm soát xung đột phiên bản
        db.Entry(season).Property(s => s.Version).OriginalValue = request.Version;

        await db.SaveChangesAsync(ct);

        return new SeasonResponse(
            season.Id,
            season.Plot.Id,
            season.Plot.Code,
            season.Crop.Id,
            season.Crop.Name,
            season.Crop.YieldUnit,
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

