using System.Linq.Expressions;
using HouseholdFarm.Api.Common.Auth;
using HouseholdFarm.Api.Common.Errors;
using HouseholdFarm.Api.Common.Persistence;
using HouseholdFarm.Api.Common.Responses;
using HouseholdFarm.Api.Data;
using HouseholdFarm.Api.Domain.Entities;
using HouseholdFarm.Api.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace HouseholdFarm.Api.Features.Plots;

public sealed class PlotService(AppDbContext db, ICurrentUser currentUser) : IPlotService
{
    // BR-CAT-01: tiền tố mã thửa tự sinh.
    private const string CodePrefix = "TD";

    // Độ dài cột plots.code.
    private const int CodeMaxLength = 20;

    // Dịch biểu thức một lần, dùng khi đã có entity trong bộ nhớ (sau khi tạo, sửa).
    private static readonly Func<Plot, PlotResponse> _toOwnerResponse = ToResponse(isWorker: false).Compile();

    public async Task<PagedResult<PlotListItem>> GetListAsync(PlotQuery query, CancellationToken ct)
    {
        currentUser.EnsureHouseholdUser();

        // Không thêm điều kiện theo hộ: Global Query Filter đã lọc. VisibleTo chỉ thu hẹp thêm trong hộ.
        var plots = db.Plots.AsNoTracking().VisibleTo(currentUser);
        var isWorker = IsWorker();

        // BR-USR-14: Nhân công không được thấy tình trạng, loại đất, nguồn nước, diện tích. Lọc hay sắp xếp theo các trường
        // này cũng dò ra được giá trị, nên với Nhân công bỏ qua các tham số đó, chỉ tìm theo mã, vị trí và sắp theo mã.
        if (isWorker)
        {
            query = query with { Status = null, SoilType = null, WaterSource = null, Sort = null };
        }

        if (query.Status is { } status)
        {
            plots = plots.Where(p => p.Status == status);
        }

        if (query.SoilType is { } soilType)
        {
            plots = plots.Where(p => p.SoilType == soilType);
        }

        if (query.WaterSource is { } waterSource)
        {
            plots = plots.Where(p => p.WaterSource == waterSource);
        }

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var pattern = SearchPatterns.Contains(query.Search);
            plots = plots.Where(p =>
                EF.Functions.ILike(p.Code, pattern, SearchPatterns.EscapeCharacter)
                || EF.Functions.ILike(p.LocationDescription!, pattern, SearchPatterns.EscapeCharacter));
        }

        // Sắp thêm theo Id để thứ tự giữa các trang ổn định khi giá trị sắp xếp trùng nhau.
        return await Sort(plots, query.Sort)
            .ThenBy(p => p.Id)
            .Select(p => new PlotListItem(
                p.Id,
                p.Code,
                isWorker ? null : p.AreaM2,
                p.LocationDescription,
                isWorker ? null : p.SoilType,
                isWorker ? null : p.WaterSource,
                isWorker ? null : p.Status))
            .ToPagedResultAsync(query, ct);
    }

    public async Task<PlotResponse> GetAsync(Guid id, CancellationToken ct)
    {
        currentUser.EnsureHouseholdUser();

        // BR-GEN-02: thửa của hộ khác, hoặc ngoài phạm vi của Nhân công, đều báo không tìm thấy.
        return await db.Plots
            .AsNoTracking()
            .VisibleTo(currentUser)
            .Where(p => p.Id == id)
            .Select(ToResponse(IsWorker()))
            .SingleOrDefaultAsync(ct)
            ?? throw NotFound();
    }

    public async Task<PlotResponse> CreateAsync(CreatePlotRequest request, CancellationToken ct)
    {
        currentUser.EnsureOwner();

        // Validator đã bảo đảm các enum có giá trị.
        var soilType = request.SoilType!.Value;
        var waterSource = request.WaterSource!.Value;

        // Không gán HouseholdId, CreatedAt, CreatedById: interceptor lấy từ người đang đăng nhập (BR-GEN-01, TC-10).
        var plot = new Plot
        {
            AreaM2 = request.AreaM2,
            LocationDescription = TrimToNull(request.LocationDescription),
            SoilType = soilType,
            SoilTypeOther = soilType == SoilType.Other ? TrimToNull(request.SoilTypeOther) : null,
            WaterSource = waterSource,
            WaterSourceOther = waterSource == WaterSource.Other ? TrimToNull(request.WaterSourceOther) : null,
            Status = RecordStatus.Active,
            Note = TrimToNull(request.Note),
        };
        db.Plots.Add(plot);

        if (TrimToNull(request.Code) is { } code)
        {
            await EnsureCodeUnusedAsync(code, exceptId: null, ct);
            plot.Code = code;
            await SaveAsync(code, ct);
        }
        else
        {
            await SaveWithGeneratedCodeAsync(plot, ct);
        }

        return _toOwnerResponse(plot);
    }

    public async Task<PlotResponse> UpdateAsync(Guid id, UpdatePlotRequest request, CancellationToken ct)
    {
        currentUser.EnsureOwner();

        var plot = await db.Plots.VisibleTo(currentUser).SingleOrDefaultAsync(p => p.Id == id, ct) ?? throw NotFound();

        // Validator đã bảo đảm các enum có giá trị.
        var soilType = request.SoilType!.Value;
        var waterSource = request.WaterSource!.Value;
        var status = request.Status!.Value;

        // BR-GEN-11: người khác đã sửa sau lần đọc của người này thì lưu báo CONCURRENCY_CONFLICT, không ghi đè.
        db.Entry(plot).Property(p => p.Version).OriginalValue = request.Version;

        var code = request.Code.Trim();
        if (code != plot.Code)
        {
            await EnsureCodeUnusedAsync(code, exceptId: plot.Id, ct);
        }

        if (plot.Status == RecordStatus.Active && status == RecordStatus.Inactive)
        {
            await EnsureNoRunningSeasonAsync(plot, ct);
        }

        plot.Code = code;
        plot.AreaM2 = request.AreaM2;
        plot.LocationDescription = TrimToNull(request.LocationDescription);
        plot.SoilType = soilType;
        plot.SoilTypeOther = soilType == SoilType.Other ? TrimToNull(request.SoilTypeOther) : null;
        plot.WaterSource = waterSource;
        plot.WaterSourceOther = waterSource == WaterSource.Other ? TrimToNull(request.WaterSourceOther) : null;
        plot.Status = status;
        plot.Note = TrimToNull(request.Note);
        await SaveAsync(code, ct);

        return _toOwnerResponse(plot);
    }

    // BR-CAT-01: kiểm tra trước để báo lỗi rõ ràng; ràng buộc duy nhất (household_id, code) chặn nốt khi tạo cùng lúc.
    private async Task EnsureCodeUnusedAsync(string code, Guid? exceptId, CancellationToken ct)
    {
        if (await db.Plots.AnyAsync(p => p.Code == code && p.Id != exceptId, ct))
        {
            throw CodeAlreadyUsed(code);
        }
    }

    // BR-CAT-04: mùa vụ đã chốt, đã hủy không chặn; mùa vụ cũ vẫn giữ tham chiếu tới thửa ngừng sử dụng (BR-GEN-13).
    private async Task EnsureNoRunningSeasonAsync(Plot plot, CancellationToken ct)
    {
        var running = await db.Seasons.CountAsync(
            s => s.PlotId == plot.Id && (s.Status == SeasonStatus.Planned || s.Status == SeasonStatus.InProgress),
            ct);
        if (running > 0)
        {
            throw new AppException(
                ErrorCodes.CatalogInUse,
                $"Thửa {plot.Code} đang có {running} mùa vụ lên kế hoạch hoặc đang canh tác. "
                + "Chốt hoặc hủy các mùa vụ đó trước khi ngừng sử dụng thửa.");
        }
    }

    // Mã tự sinh có thể trùng khi hai người cùng tạo một lúc: sinh lại mã rồi lưu lại, người dùng không thấy lỗi.
    private async Task SaveWithGeneratedCodeAsync(Plot plot, CancellationToken ct)
    {
        for (var attempt = 1; ; attempt++)
        {
            plot.Code = await CatalogCodes.NextAsync(db.Plots.Select(p => p.Code), CodePrefix, CodeMaxLength, ct);
            try
            {
                await db.SaveChangesAsync(ct);
                return;
            }
            catch (DbUpdateException ex) when (IsCodeViolation(ex))
            {
                // Thử lại với mã mới; thửa vẫn ở trạng thái chờ thêm trong DbContext.
                if (attempt == CatalogCodes.MaxGenerateAttempts)
                {
                    throw CodeAlreadyUsed(plot.Code);
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
        ex.IsUniqueViolation<Plot>(db, nameof(Plot.HouseholdId), nameof(Plot.Code));

    private bool IsWorker() => currentUser.Role == UserRole.Worker;

    // Tham số sort đã được PlotQueryValidator kiểm tra, chỉ còn các trường trong PlotQuery.SortFields.
    private static IOrderedQueryable<Plot> Sort(IQueryable<Plot> plots, string? sort)
    {
        var descending = sort?.StartsWith('-') == true;
        return sort?.TrimStart('-') switch
        {
            "areaM2" => descending ? plots.OrderByDescending(p => p.AreaM2) : plots.OrderBy(p => p.AreaM2),
            "createdAt" => descending ? plots.OrderByDescending(p => p.CreatedAt) : plots.OrderBy(p => p.CreatedAt),
            _ => descending ? plots.OrderByDescending(p => p.Code) : plots.OrderBy(p => p.Code),
        };
    }

    // Chuyển entity sang DTO bằng biểu thức để EF dịch thẳng ra SQL, chỉ đọc đúng các cột cần (không nạp cả entity).
    // BR-USR-14: Nhân công chỉ thấy mã và vị trí thửa.
    private static Expression<Func<Plot, PlotResponse>> ToResponse(bool isWorker) => p => new PlotResponse(
        p.Id,
        p.Code,
        isWorker ? null : p.AreaM2,
        p.LocationDescription,
        isWorker ? null : p.SoilType,
        isWorker ? null : p.SoilTypeOther,
        isWorker ? null : p.WaterSource,
        isWorker ? null : p.WaterSourceOther,
        isWorker ? null : p.Status,
        isWorker ? null : p.Note,
        isWorker ? null : p.Version);

    private static string? TrimToNull(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static AppException NotFound() => new(ErrorCodes.NotFound, "Không tìm thấy thửa đất.");

    private static AppException CodeAlreadyUsed(string code) =>
        new(ErrorCodes.CodeAlreadyUsed, $"Mã thửa {code} đã có trong hộ. Hãy dùng mã khác.");
}
