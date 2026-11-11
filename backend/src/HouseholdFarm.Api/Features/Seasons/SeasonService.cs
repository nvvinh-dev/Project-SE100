using HouseholdFarm.Api.Common.Auditing;
using HouseholdFarm.Api.Common.Auth;
using HouseholdFarm.Api.Common.Errors;
using HouseholdFarm.Api.Common.Persistence;
using HouseholdFarm.Api.Common.Responses;
using HouseholdFarm.Api.Common.Time;
using HouseholdFarm.Api.Data;
using HouseholdFarm.Api.Domain.Entities;
using HouseholdFarm.Api.Domain.Enums;
using HouseholdFarm.Api.Features.Harvests;
using Microsoft.EntityFrameworkCore;

namespace HouseholdFarm.Api.Features.Seasons;

public sealed class SeasonService(
    AppDbContext db,
    ICurrentUser currentUser,
    IAppClock clock,
    IAuditLogger audit,
    IProduceStockService produceStockService) : ISeasonService
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

    public async Task<SeasonDetailResponse> GetDetailAsync(Guid id, CancellationToken ct)
    {
        // Ma trận phân quyền: Nhân công không được xem chi tiết mùa vụ
        currentUser.EnsureNotWorker();

        var isOwner = currentUser.Role == UserRole.Owner;
        var isInScope = isOwner || currentUser.AssignedSeasonIds.Contains(id);
        var hasFinanceScope = !isInScope && currentUser.CanManageFinance;
        var hasStockScope = !isInScope && currentUser.CanManageStock;

        // BR-GEN-02, BR-USR-13: Ngoài phạm vi và không có quyền Kho/TC thì báo không tìm thấy
        if (!isInScope && !hasFinanceScope && !hasStockScope)
        {
            throw AppException.NotFound("mùa vụ");
        }

        var season = await db.Seasons
            .AsNoTracking()
            .Include(s => s.Plot)
            .Include(s => s.Crop)
            .FirstOrDefaultAsync(s => s.Id == id, ct)
            ?? throw AppException.NotFound("mùa vụ");

        var isOutOfScope = !isInScope;
        decimal harvestedYield = 0m;
        var completedTasksCount = 0;
        var totalTasksCount = 0;
        IReadOnlyList<ProduceStockSummaryItem> produceStocks = [];
        SeasonFinancialSummary? financialSummary = null;
        IReadOnlyList<SeasonAssignmentResponse> assignments = [];

        if (!isOutOfScope)
        {
            // Danh sách người được phân công (FR-SEA-06)
            assignments = await db.SeasonAssignments
                .AsNoTracking()
                .Where(a => a.SeasonId == id)
                .OrderBy(a => a.CreatedAt)
                .Select(a => new SeasonAssignmentResponse(
                    a.Id,
                    a.SeasonId,
                    a.UserId,
                    a.User.FullName,
                    a.User.Role,
                    a.User.PhoneNumber ?? string.Empty,
                    a.CreatedAt))
                .ToListAsync(ct);

            // Tiến độ công việc (không tính công việc Đã hủy)
            var tasksQuery = db.FarmTasks
                .AsNoTracking()
                .Where(t => t.SeasonId == id && t.Status != FarmTaskStatus.Cancelled);
            totalTasksCount = await tasksQuery.CountAsync(ct);
            completedTasksCount = await tasksQuery.CountAsync(t => t.Status == FarmTaskStatus.Done, ct);

            // Tổng sản lượng thu hoạch chưa Void
            harvestedYield = await db.HarvestRecords
                .AsNoTracking()
                .Where(h => h.SeasonId == id && h.VoidedAt == null)
                .SumAsync(h => (decimal?)h.Quantity, ct) ?? 0m;

            // Tồn nông sản theo loại chất lượng
            try
            {
                var stocks = await produceStockService.GetStockAsync(id, ct);
                produceStocks = stocks.Select(p => new ProduceStockSummaryItem(
                    p.QualityGrade,
                    p.Quantity,
                    season.Crop.YieldUnit)).ToList();
            }
            catch (NotImplementedException)
            {
                produceStocks = [
                    new(QualityGrade.Grade1, 0, season.Crop.YieldUnit),
                    new(QualityGrade.Grade2, 0, season.Crop.YieldUnit),
                    new(QualityGrade.Grade3, 0, season.Crop.YieldUnit),
                ];
            }

            // Tóm tắt tài chính dành cho Chủ hộ hoặc người có quyền ManageFinance (BR-USR-13)
            if (isOwner || currentUser.CanManageFinance)
            {
                var totalExpense = await db.Expenses
                    .AsNoTracking()
                    .Where(e => e.SeasonId == id && e.VoidedAt == null)
                    .SumAsync(e => (decimal?)e.Amount, ct) ?? 0m;

                var totalRevenue = await db.Sales
                    .AsNoTracking()
                    .Where(s => s.SeasonId == id && s.VoidedAt == null)
                    .SumAsync(s => (decimal?)(s.Quantity * s.UnitPrice), ct) ?? 0m;

                financialSummary = new SeasonFinancialSummary(totalExpense, totalRevenue, totalRevenue - totalExpense);
            }
        }
        else if (hasFinanceScope)
        {
            // Ngoại lệ BR-USR-13: Thành viên có quyền ManageFinance thấy thêm sản lượng, tồn nông sản, phần tài chính
            harvestedYield = await db.HarvestRecords
                .AsNoTracking()
                .Where(h => h.SeasonId == id && h.VoidedAt == null)
                .SumAsync(h => (decimal?)h.Quantity, ct) ?? 0m;

            try
            {
                var stocks = await produceStockService.GetStockAsync(id, ct);
                produceStocks = stocks.Select(p => new ProduceStockSummaryItem(
                    p.QualityGrade,
                    p.Quantity,
                    season.Crop.YieldUnit)).ToList();
            }
            catch (NotImplementedException)
            {
                produceStocks = [
                    new(QualityGrade.Grade1, 0, season.Crop.YieldUnit),
                    new(QualityGrade.Grade2, 0, season.Crop.YieldUnit),
                    new(QualityGrade.Grade3, 0, season.Crop.YieldUnit),
                ];
            }

            var totalExpense = await db.Expenses
                .AsNoTracking()
                .Where(e => e.SeasonId == id && e.VoidedAt == null)
                .SumAsync(e => (decimal?)e.Amount, ct) ?? 0m;

            var totalRevenue = await db.Sales
                .AsNoTracking()
                .Where(s => s.SeasonId == id && s.VoidedAt == null)
                .SumAsync(s => (decimal?)(s.Quantity * s.UnitPrice), ct) ?? 0m;

            financialSummary = new SeasonFinancialSummary(totalExpense, totalRevenue, totalRevenue - totalExpense);
        }

        var canEdit = !isOutOfScope && isOwner && season.Status is SeasonStatus.Planned or SeasonStatus.InProgress;
        var canStart = !isOutOfScope && isOwner && season.Status == SeasonStatus.Planned;
        var canClose = !isOutOfScope && (isOwner || currentUser.CanCloseSeason) && season.Status == SeasonStatus.InProgress;
        var canCancel = !isOutOfScope && isOwner && season.Status is SeasonStatus.Planned or SeasonStatus.InProgress;
        var canReopen = !isOutOfScope && isOwner && season.Status == SeasonStatus.Closed;

        return new SeasonDetailResponse(
            season.Id,
            season.PlotId,
            season.Plot.Code,
            season.CropId,
            season.Crop.Name,
            season.Crop.YieldUnit,
            season.StartDate,
            season.ExpectedHarvestDate,
            season.TargetYield,
            season.Status,
            season.StartedAt,
            season.ClosedAt,
            season.CancelledAt,
            season.CancelReason,
            season.Version,
            harvestedYield,
            completedTasksCount,
            totalTasksCount,
            produceStocks,
            financialSummary,
            assignments,
            isOutOfScope,
            canEdit,
            canStart,
            canClose,
            canCancel,
            canReopen);
    }

    public async Task<SeasonResponse> StartAsync(Guid id, CancellationToken ct)
    {
        // Ma trận phân quyền: chỉ Chủ hộ có quyền bắt đầu mùa vụ
        currentUser.EnsureOwner();

        if (currentUser.HouseholdId is null)
        {
            throw AppException.NotFound("hộ");
        }

        await using var tx = db.Database.CurrentTransaction is null
            ? await db.Database.BeginTransactionAsync(ct)
            : null;

        // BR-GEN-12: Khóa dòng seasons trước khi đọc dữ liệu kiểm tra
        await db.Database.ExecuteSqlAsync(
            $"SELECT 1 FROM seasons WHERE id = {id} AND household_id = {currentUser.HouseholdId.Value} FOR UPDATE",
            ct);

        var season = await db.Seasons
            .Include(s => s.Plot)
            .Include(s => s.Crop)
            .FirstOrDefaultAsync(s => s.Id == id, ct)
            ?? throw AppException.NotFound("mùa vụ");

        if (season.Status != SeasonStatus.Planned)
        {
            throw new AppException(
                ErrorCodes.SeasonInvalidState,
                $"Chỉ có thể bắt đầu mùa vụ đang ở trạng thái 'Lên kế hoạch' (hiện tại: '{FormatStatus(season.Status)}').");
        }

        // BR-SEA-03: Kiểm tra thửa đang bận Lớp 2
        await EnsurePlotNotBusyAsync(season.PlotId, excludeSeasonId: season.Id, ct);

        season.Status = SeasonStatus.InProgress;
        season.StartedAt = clock.UtcNow;

        // Ghi nhật ký kiểm toán (BR-GEN-07, BR-GEN-08)
        audit.Log(
            AuditActions.SeasonStart,
            season.Id,
            oldValues: new { status = SeasonStatus.Planned },
            newValues: new { status = SeasonStatus.InProgress, startedAt = season.StartedAt },
            reason: null);

        await db.SaveChangesAsync(ct);

        if (tx is not null)
        {
            await tx.CommitAsync(ct);
        }

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

    public async Task<SeasonResponse> CloseAsync(Guid id, CloseSeasonRequest request, CancellationToken ct)
    {
        // Phân quyền: Chủ hộ HOẶC Thành viên có CanCloseSeason trong phân công (FR-SEA-09, BR-USR-13)
        var isOwner = currentUser.Role == UserRole.Owner;
        var hasPermission = isOwner || (currentUser.Role == UserRole.Member && currentUser.CanCloseSeason);
        if (!hasPermission)
        {
            throw new AppException(ErrorCodes.Forbidden, "Bạn không có quyền chốt mùa vụ này.");
        }

        if (!isOwner && !currentUser.AssignedSeasonIds.Contains(id))
        {
            throw AppException.NotFound("mùa vụ");
        }

        if (!currentUser.HouseholdId.HasValue)
        {
            throw AppException.NotFound("hộ");
        }

        await using var tx = db.Database.CurrentTransaction is null
            ? await db.Database.BeginTransactionAsync(ct)
            : null;

        // BR-GEN-12: Khóa dòng seasons trước khi đọc dữ liệu kiểm tra
        await db.Database.ExecuteSqlAsync(
            $"SELECT 1 FROM seasons WHERE id = {id} AND household_id = {currentUser.HouseholdId.Value} FOR UPDATE",
            ct);

        var season = await db.Seasons
            .Include(s => s.Plot)
            .Include(s => s.Crop)
            .FirstOrDefaultAsync(s => s.Id == id, ct)
            ?? throw AppException.NotFound("mùa vụ");

        if (season.Status != SeasonStatus.InProgress)
        {
            throw new AppException(
                ErrorCodes.SeasonInvalidState,
                $"Chỉ có thể chốt mùa vụ đang ở trạng thái 'Đang canh tác' (hiện tại: '{FormatStatus(season.Status)}').");
        }

        // BR-SEA-05: Kiểm tra tồn nông sản mọi loại chất lượng bằng 0 qua IProduceStockService
        await produceStockService.EnsureZeroStockAsync(season.Id, ct);

        // BR-GEN-14, BR-SEA-05: Kiểm tra công việc chưa thực hiện
        var unfinishedTasks = await db.FarmTasks
            .Where(t => t.SeasonId == season.Id && (t.Status == FarmTaskStatus.Pending || t.Status == FarmTaskStatus.WaitingForSupply))
            .OrderBy(t => t.ScheduledDate)
            .ToListAsync(ct);

        if (unfinishedTasks.Count > 0 && request.Confirmed != true)
        {
            var items = unfinishedTasks.Select(t => new
            {
                id = t.Id,
                label = $"{FormatTaskType(t.Type, t.TypeOther)} · {t.ScheduledDate:dd/MM/yyyy}",
            }).ToList();

            throw new AppException(
                ErrorCodes.ConfirmationRequired,
                $"Còn {unfinishedTasks.Count} công việc chưa thực hiện trong mùa vụ. Các công việc này sẽ tự động chuyển sang Đã hủy khi chốt mùa vụ.",
                new { items });
        }

        var now = clock.UtcNow;

        // Hủy các việc chưa làm sau khi xác nhận
        foreach (var task in unfinishedTasks)
        {
            task.Status = FarmTaskStatus.Cancelled;
            task.CancelledAt = now;
        }

        season.Status = SeasonStatus.Closed;
        season.ClosedAt = now;
        season.ClosedById = currentUser.UserId;

        audit.Log(
            AuditActions.SeasonClose,
            season.Id,
            oldValues: new { status = SeasonStatus.InProgress },
            newValues: new { status = SeasonStatus.Closed, closedAt = season.ClosedAt, closedById = season.ClosedById },
            reason: null);

        await db.SaveChangesAsync(ct);

        if (tx is not null)
        {
            await tx.CommitAsync(ct);
        }

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

    public async Task<SeasonResponse> CancelAsync(Guid id, CancelSeasonRequest request, CancellationToken ct)
    {
        // Quyền: chỉ Chủ hộ (FR-SEA-10)
        currentUser.EnsureOwner();

        if (!currentUser.HouseholdId.HasValue)
        {
            throw AppException.NotFound("hộ");
        }

        await using var tx = db.Database.CurrentTransaction is null
            ? await db.Database.BeginTransactionAsync(ct)
            : null;

        // BR-GEN-12: Khóa dòng seasons trước khi đọc dữ liệu kiểm tra
        await db.Database.ExecuteSqlAsync(
            $"SELECT 1 FROM seasons WHERE id = {id} AND household_id = {currentUser.HouseholdId.Value} FOR UPDATE",
            ct);

        var season = await db.Seasons
            .Include(s => s.Plot)
            .Include(s => s.Crop)
            .FirstOrDefaultAsync(s => s.Id == id, ct)
            ?? throw AppException.NotFound("mùa vụ");

        // BR-SEA-10: Chỉ từ Planned hoặc InProgress
        if (season.Status is not (SeasonStatus.Planned or SeasonStatus.InProgress))
        {
            throw new AppException(
                ErrorCodes.SeasonInvalidState,
                $"Chỉ có thể hủy mùa vụ đang ở trạng thái 'Lên kế hoạch' hoặc 'Đang canh tác' (hiện tại: '{FormatStatus(season.Status)}').");
        }

        // BR-SEA-06: Chưa có phiếu thu hoạch hoặc giao dịch bán chưa Void
        var hasHarvest = await db.HarvestRecords.AnyAsync(h => h.SeasonId == id && h.VoidedAt == null, ct);
        var hasSale = await db.Sales.AnyAsync(s => s.SeasonId == id && s.VoidedAt == null, ct);
        if (hasHarvest || hasSale)
        {
            throw new AppException(
                ErrorCodes.SeasonHasHarvest,
                "Không thể hủy mùa vụ vì đã có phiếu thu hoạch hoặc giao dịch bán.");
        }

        // BR-GEN-14: Kiểm tra công việc chưa thực hiện
        var unfinishedTasks = await db.FarmTasks
            .Where(t => t.SeasonId == season.Id && (t.Status == FarmTaskStatus.Pending || t.Status == FarmTaskStatus.WaitingForSupply))
            .OrderBy(t => t.ScheduledDate)
            .ToListAsync(ct);

        if (unfinishedTasks.Count > 0 && request.Confirmed != true)
        {
            var items = unfinishedTasks.Select(t => new
            {
                id = t.Id,
                label = $"{FormatTaskType(t.Type, t.TypeOther)} · {t.ScheduledDate:dd/MM/yyyy}",
            }).ToList();

            throw new AppException(
                ErrorCodes.ConfirmationRequired,
                $"Còn {unfinishedTasks.Count} công việc chưa thực hiện trong mùa vụ. Các công việc này sẽ tự động chuyển sang Đã hủy khi hủy mùa vụ.",
                new { items });
        }

        var now = clock.UtcNow;
        var oldStatus = season.Status;

        foreach (var task in unfinishedTasks)
        {
            task.Status = FarmTaskStatus.Cancelled;
            task.CancelledAt = now;
        }

        season.Status = SeasonStatus.Cancelled;
        season.CancelledAt = now;
        season.CancelledById = currentUser.UserId;
        season.CancelReason = request.Reason.Trim();

        audit.Log(
            AuditActions.SeasonCancel,
            season.Id,
            oldValues: new { status = oldStatus },
            newValues: new { status = SeasonStatus.Cancelled, cancelledAt = season.CancelledAt, cancelReason = season.CancelReason },
            reason: request.Reason.Trim());

        await db.SaveChangesAsync(ct);

        if (tx is not null)
        {
            await tx.CommitAsync(ct);
        }

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

    public async Task<SeasonResponse> ReopenAsync(Guid id, ReopenSeasonRequest request, CancellationToken ct)
    {
        // Quyền: chỉ Chủ hộ (FR-SEA-14)
        currentUser.EnsureOwner();

        if (!currentUser.HouseholdId.HasValue)
        {
            throw AppException.NotFound("hộ");
        }

        await using var tx = db.Database.CurrentTransaction is null
            ? await db.Database.BeginTransactionAsync(ct)
            : null;

        // BR-GEN-12: Khóa dòng seasons trước khi đọc dữ liệu kiểm tra
        await db.Database.ExecuteSqlAsync(
            $"SELECT 1 FROM seasons WHERE id = {id} AND household_id = {currentUser.HouseholdId.Value} FOR UPDATE",
            ct);

        var season = await db.Seasons
            .Include(s => s.Plot)
            .Include(s => s.Crop)
            .FirstOrDefaultAsync(s => s.Id == id, ct)
            ?? throw AppException.NotFound("mùa vụ");

        // BR-SEA-10: Chỉ từ Closed
        if (season.Status != SeasonStatus.Closed)
        {
            throw new AppException(
                ErrorCodes.SeasonInvalidState,
                $"Chỉ có thể mở lại mùa vụ đã chốt (hiện tại: '{FormatStatus(season.Status)}').");
        }

        // BR-SEA-08: Kiểm tra lớp 2 (thửa bận)
        await EnsurePlotNotBusyAsync(season.PlotId, excludeSeasonId: season.Id, ct);

        season.Status = SeasonStatus.InProgress;
        season.ClosedAt = null;
        season.ClosedById = null;

        audit.Log(
            AuditActions.SeasonReopen,
            season.Id,
            oldValues: new { status = SeasonStatus.Closed },
            newValues: new { status = SeasonStatus.InProgress },
            reason: request.Reason.Trim());

        await db.SaveChangesAsync(ct);

        if (tx is not null)
        {
            await tx.CommitAsync(ct);
        }

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

    private static string FormatTaskType(FarmTaskType type, string? typeOther) => type switch
    {
        FarmTaskType.LandPreparation => "Làm đất",
        FarmTaskType.Sowing => "Gieo trồng",
        FarmTaskType.Watering => "Tưới nước",
        FarmTaskType.Fertilizing => "Bón phân",
        FarmTaskType.Weeding => "Làm cỏ",
        FarmTaskType.PestInspection => "Kiểm tra sâu bệnh",
        FarmTaskType.Spraying => "Phun thuốc",
        FarmTaskType.Other => string.IsNullOrWhiteSpace(typeOther) ? "Khác" : typeOther,
        _ => type.ToString(),
    };

    private static string FormatStatus(SeasonStatus status) => status switch
    {
        SeasonStatus.Planned => "Lên kế hoạch",
        SeasonStatus.InProgress => "Đang canh tác",
        SeasonStatus.Closed => "Đã chốt",
        SeasonStatus.Cancelled => "Đã hủy",
        _ => status.ToString(),
    };

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

