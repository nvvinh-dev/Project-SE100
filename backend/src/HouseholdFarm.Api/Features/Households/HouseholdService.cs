using HouseholdFarm.Api.Common.Auditing;
using HouseholdFarm.Api.Common.Auth;
using HouseholdFarm.Api.Common.Errors;
using HouseholdFarm.Api.Data;
using HouseholdFarm.Api.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace HouseholdFarm.Api.Features.Households;

// Bảng households không có Global Query Filter: luôn đọc đúng hộ của người đang đăng nhập.
public sealed class HouseholdService(AppDbContext db, ICurrentUser currentUser, IAuditLogger audit) : IHouseholdService
{
    public async Task<HouseholdResponse> GetAsync(CancellationToken ct)
    {
        currentUser.EnsureNotWorker();

        return await CurrentHousehold()
            .AsNoTracking()
            .Select(h => new HouseholdResponse(h.Code, h.Name, h.ProductionAddress, h.ContactInfo, h.Version))
            .SingleAsync(ct);
    }

    public async Task<HouseholdResponse> UpdateAsync(UpdateHouseholdRequest request, CancellationToken ct)
    {
        currentUser.EnsureOwner();

        var household = await CurrentHousehold().SingleAsync(ct);

        // BR-GEN-11: người khác đã sửa sau lần đọc của người này thì lưu báo CONCURRENCY_CONFLICT, không ghi đè.
        db.Entry(household).Property(h => h.Version).OriginalValue = request.Version;

        // BR-CAT-02: mã hộ không sửa được nên không có trong request. Validator đã bảo đảm tên, địa chỉ có giá trị.
        household.Name = request.Name!.Trim();
        household.ProductionAddress = request.ProductionAddress!.Trim();
        household.ContactInfo = string.IsNullOrWhiteSpace(request.ContactInfo) ? null : request.ContactInfo.Trim();
        await db.SaveChangesAsync(ct);

        return new HouseholdResponse(
            household.Code,
            household.Name,
            household.ProductionAddress,
            household.ContactInfo,
            household.Version);
    }

    public async Task<HouseholdSettingsResponse> GetSettingsAsync(CancellationToken ct)
    {
        currentUser.EnsureOwner();

        return await CurrentHousehold()
            .AsNoTracking()
            .Select(h => new HouseholdSettingsResponse(
                h.Settings.AllowNegativeStock,
                h.Settings.AllowOverlappingSeasons,
                h.Version))
            .SingleAsync(ct);
    }

    public async Task<HouseholdSettingsResponse> ChangeSettingsAsync(ChangeHouseholdSettingsRequest request, CancellationToken ct)
    {
        currentUser.EnsureOwner();

        var household = await CurrentHousehold().SingleAsync(ct);
        db.Entry(household).Property(h => h.Version).OriginalValue = request.Version;

        // Validator đã bảo đảm hai công tắc có giá trị.
        household.Settings.AllowNegativeStock = request.AllowNegativeStock!.Value;
        household.Settings.AllowOverlappingSeasons = request.AllowOverlappingSeasons!.Value;

        // BR-GEN-07, BR-GEN-08: chỉ ghi công tắc bị đổi, cùng lần lưu với thao tác; không đổi gì thì không ghi nhật ký.
        var changes = ChangedValues.Of(db.Entry(household.Settings));
        if (!changes.IsEmpty)
        {
            audit.Log(AuditActions.HouseholdChangeSettings, household.Id, changes.OldValues, changes.NewValues, reason: null);
        }

        await db.SaveChangesAsync(ct);

        return new HouseholdSettingsResponse(
            household.Settings.AllowNegativeStock,
            household.Settings.AllowOverlappingSeasons,
            household.Version);
    }

    // EnsureNotWorker, EnsureOwner đã loại Quản trị hệ thống nên người gọi luôn thuộc một hộ.
    private IQueryable<Household> CurrentHousehold()
    {
        var householdId = currentUser.HouseholdId
            ?? throw new AppException(ErrorCodes.Forbidden, "Tài khoản không thuộc hộ nào.");
        return db.Households.Where(h => h.Id == householdId);
    }
}
