using HouseholdFarm.Api.Common.Auth;
using HouseholdFarm.Api.Common.Errors;
using HouseholdFarm.Api.Common.Persistence;
using HouseholdFarm.Api.Common.Responses;
using HouseholdFarm.Api.Data;
using HouseholdFarm.Api.Domain.Entities;
using HouseholdFarm.Api.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace HouseholdFarm.Api.Features.Crops;

public sealed class CropService(AppDbContext db, ICurrentUser currentUser) : ICropService
{
    public async Task<PagedResult<CropListItem>> GetListAsync(CropQuery query, CancellationToken ct)
    {
        currentUser.EnsureHouseholdUser();

        // Không thêm điều kiện theo hộ: Global Query Filter đã lọc. VisibleTo chỉ thu hẹp thêm trong hộ.
        var crops = Crops().AsNoTracking();
        var isWorker = IsWorker();

        // BR-USR-14: Nhân công chỉ thấy tên cây. Lọc hay sắp xếp theo nhóm, trạng thái cũng dò ra được giá trị,
        // nên với Nhân công bỏ qua các tham số đó, chỉ tìm theo tên và sắp theo tên.
        if (isWorker)
        {
            query = query with { Group = null, Status = null, Sort = null };
        }

        if (query.Group is { } group)
        {
            crops = crops.Where(c => c.Group == group);
        }

        if (query.Status is { } status)
        {
            crops = crops.Where(c => c.Status == status);
        }

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var pattern = SearchPatterns.Contains(query.Search);
            crops = crops.Where(c => EF.Functions.ILike(c.Name, pattern, SearchPatterns.EscapeCharacter));
        }

        // Sắp thêm theo Id để thứ tự giữa các trang ổn định khi giá trị sắp xếp trùng nhau.
        return await Sort(crops, query.Sort)
            .ThenBy(c => c.Id)
            .Select(c => new CropListItem(
                c.Id,
                c.Name,
                isWorker ? null : c.Group,
                isWorker ? null : c.YieldUnit,
                isWorker ? null : c.Status))
            .ToPagedResultAsync(query, ct);
    }

    public async Task<CropResponse> GetAsync(Guid id, CancellationToken ct)
    {
        currentUser.EnsureHouseholdUser();

        var isWorker = IsWorker();
        var harvestRecords = db.HarvestRecords;

        // BR-GEN-02: cây của hộ khác, hoặc ngoài phạm vi của Nhân công, đều báo không tìm thấy.
        // BR-USR-14: Nhân công chỉ thấy tên cây.
        return await Crops()
            .AsNoTracking()
            .Where(c => c.Id == id)
            .Select(c => new CropResponse(
                c.Id,
                c.Name,
                isWorker ? null : c.Group,
                isWorker ? null : c.GroupOther,
                isWorker ? null : c.YieldUnit,
                isWorker ? null : c.ReferenceInfo,
                isWorker ? null : c.Status,
                isWorker ? null : harvestRecords.Any(h => h.Season.CropId == c.Id),
                isWorker ? null : c.Version))
            .SingleOrDefaultAsync(ct)
            ?? throw NotFound();
    }

    public async Task<CropResponse> CreateAsync(CreateCropRequest request, CancellationToken ct)
    {
        currentUser.EnsureOwner();

        // Validator đã bảo đảm tên và các enum có giá trị.
        var name = request.Name!.Trim();
        var group = request.Group!.Value;

        await EnsureNameUnusedAsync(name, exceptId: null, ct);

        // Không gán HouseholdId, CreatedAt, CreatedById: interceptor lấy từ người đang đăng nhập (BR-GEN-01, TC-10).
        var crop = new Crop
        {
            Name = name,
            Group = group,
            GroupOther = group == CropGroup.Other ? TrimToNull(request.GroupOther) : null,
            YieldUnit = request.YieldUnit!.Value,
            ReferenceInfo = TrimToNull(request.ReferenceInfo),
            Status = RecordStatus.Active,
        };
        db.Crops.Add(crop);
        await SaveAsync(name, ct);

        return ToOwnerResponse(crop, yieldUnitLocked: false);
    }

    public async Task<CropResponse> UpdateAsync(Guid id, UpdateCropRequest request, CancellationToken ct)
    {
        currentUser.EnsureOwner();

        var crop = await Crops().SingleOrDefaultAsync(c => c.Id == id, ct) ?? throw NotFound();

        // Validator đã bảo đảm tên và các enum có giá trị.
        var name = request.Name!.Trim();
        var group = request.Group!.Value;
        var yieldUnit = request.YieldUnit!.Value;
        var status = request.Status!.Value;

        // BR-GEN-11: người khác đã sửa sau lần đọc của người này thì lưu báo CONCURRENCY_CONFLICT, không ghi đè.
        db.Entry(crop).Property(c => c.Version).OriginalValue = request.Version;

        if (name != crop.Name)
        {
            await EnsureNameUnusedAsync(name, exceptId: crop.Id, ct);
        }

        var yieldUnitLocked = await HasHarvestRecordAsync(crop.Id, ct);

        // BR-CAT-05: tính cả phiếu đã hủy, vì phiếu đã hủy vẫn được lưu và hiển thị theo đơn vị của cây.
        if (yieldUnit != crop.YieldUnit && yieldUnitLocked)
        {
            throw new AppException(
                ErrorCodes.UnitLocked,
                $"Cây {crop.Name} đã có phiếu thu hoạch nên không đổi được đơn vị sản lượng.");
        }

        if (crop.Status == RecordStatus.Active && status == RecordStatus.Inactive)
        {
            await EnsureNoRunningSeasonAsync(crop, ct);
        }

        crop.Name = name;
        crop.Group = group;
        crop.GroupOther = group == CropGroup.Other ? TrimToNull(request.GroupOther) : null;
        crop.YieldUnit = yieldUnit;
        crop.ReferenceInfo = TrimToNull(request.ReferenceInfo);
        crop.Status = status;
        await SaveAsync(name, ct);

        return ToOwnerResponse(crop, yieldUnitLocked);
    }

    private IQueryable<Crop> Crops() => db.Crops.VisibleTo(db.FarmTasks, currentUser);

    // BR-CAT-03: kiểm tra trước để báo lỗi rõ ràng; ràng buộc duy nhất (household_id, name) chặn nốt khi tạo cùng lúc.
    private async Task EnsureNameUnusedAsync(string name, Guid? exceptId, CancellationToken ct)
    {
        if (await db.Crops.AnyAsync(c => c.Name == name && c.Id != exceptId, ct))
        {
            throw NameAlreadyUsed(name);
        }
    }

    private Task<bool> HasHarvestRecordAsync(Guid cropId, CancellationToken ct) =>
        db.HarvestRecords.AnyAsync(h => h.Season.CropId == cropId, ct);

    // BR-CAT-04: mùa vụ đã chốt, đã hủy không chặn; mùa vụ cũ vẫn giữ tham chiếu tới cây ngừng sử dụng (BR-GEN-13).
    private async Task EnsureNoRunningSeasonAsync(Crop crop, CancellationToken ct)
    {
        var running = await db.Seasons.CountAsync(
            s => s.CropId == crop.Id && (s.Status == SeasonStatus.Planned || s.Status == SeasonStatus.InProgress),
            ct);
        if (running > 0)
        {
            throw new AppException(
                ErrorCodes.CatalogInUse,
                $"Cây {crop.Name} đang có {running} mùa vụ lên kế hoạch hoặc đang canh tác. "
                + "Chốt hoặc hủy các mùa vụ đó trước khi ngừng sử dụng cây.");
        }
    }

    private async Task SaveAsync(string name, CancellationToken ct)
    {
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (ex.IsUniqueViolation<Crop>(db, nameof(Crop.HouseholdId), nameof(Crop.Name)))
        {
            throw NameAlreadyUsed(name);
        }
    }

    private bool IsWorker() => currentUser.Role == UserRole.Worker;

    // Tham số sort đã được CropQueryValidator kiểm tra, chỉ còn các trường trong CropQuery.SortFields.
    private static IOrderedQueryable<Crop> Sort(IQueryable<Crop> crops, string? sort)
    {
        var descending = sort?.StartsWith('-') == true;
        return sort?.TrimStart('-') switch
        {
            "createdAt" => descending ? crops.OrderByDescending(c => c.CreatedAt) : crops.OrderBy(c => c.CreatedAt),
            _ => descending ? crops.OrderByDescending(c => c.Name) : crops.OrderBy(c => c.Name),
        };
    }

    private static CropResponse ToOwnerResponse(Crop c, bool yieldUnitLocked) => new(
        c.Id,
        c.Name,
        c.Group,
        c.GroupOther,
        c.YieldUnit,
        c.ReferenceInfo,
        c.Status,
        yieldUnitLocked,
        c.Version);

    private static string? TrimToNull(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static AppException NotFound() => new(ErrorCodes.NotFound, "Không tìm thấy cây trồng.");

    private static AppException NameAlreadyUsed(string name) =>
        new(ErrorCodes.NameAlreadyUsed, $"Tên cây {name} đã có trong hộ. Hãy dùng tên khác.");
}
