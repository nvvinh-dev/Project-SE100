using HouseholdFarm.Api.Domain.Entities;
using HouseholdFarm.Api.Domain.Enums;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace HouseholdFarm.Api.Data.Seed;

/// <summary>
/// Dữ liệu mẫu để chạy thử trên máy phát triển, chạy bằng <c>dotnet run -- --seed</c>: tài khoản Quản trị hệ thống (BR-USR-06),
/// hai hộ A và B, mỗi hộ có Chủ hộ, hai Thành viên hộ (một người đủ ba quyền bổ sung, một người không có), một Nhân công,
/// và vài thửa đất.
/// </summary>
/// <remarks>
/// Chỉ chạy khi database chưa có hộ nào, để chạy lại không nhân đôi dữ liệu. Tên, số điện thoại đều là giả vì dữ liệu mẫu
/// cũng là dữ liệu gửi cho Gemini (D-18). Chạy không có người đăng nhập nên hộ và người tạo của thửa gán sẵn ở đây.
/// </remarks>
public sealed class DevDataSeeder(
    AppDbContext db,
    UserManager<ApplicationUser> userManager,
    ILogger<DevDataSeeder> logger)
{
    /// <summary>Mật khẩu của mọi tài khoản mẫu, chỉ dùng cho dữ liệu mẫu.</summary>
    public const string Password = "MatKhauMau@2026";

    /// <summary>Tạo dữ liệu mẫu; trả <c>false</c> khi database đã có hộ nên không tạo gì.</summary>
    public async Task<bool> SeedAsync(CancellationToken ct)
    {
        if (await db.Households.AnyAsync(ct))
        {
            logger.LogInformation("Database đã có hộ, bỏ qua dữ liệu mẫu");
            return false;
        }

        // Một giao dịch cho toàn bộ dữ liệu mẫu: lỗi giữa chừng thì không để lại hộ thiếu tài khoản, lần chạy sau vẫn tạo được.
        await using var transaction = await db.Database.BeginTransactionAsync(ct);

        await CreateUserAsync(householdId: null, UserRole.SystemAdmin, "Quản trị hệ thống", "0900000001");

        var householdA = new Household
        {
            Code = "HH000001",
            Name = "Hộ mẫu A",
            ProductionAddress = "Ấp Mẫu 1, xã Mẫu Thuận",
        };
        var householdB = new Household
        {
            Code = "HH000002",
            Name = "Hộ mẫu B",
            ProductionAddress = "Ấp Mẫu 2, xã Mẫu Thuận",
        };
        db.Households.AddRange(householdA, householdB);
        await db.SaveChangesAsync(ct);

        var ownerA = await CreateHouseholdUsersAsync(householdA, "A", phonePrefix: "090000001");
        var ownerB = await CreateHouseholdUsersAsync(householdB, "B", phonePrefix: "090000002");

        // Hộ A có thửa ở cả hai trạng thái để thử lọc và ngừng sử dụng; dữ liệu các module khác thêm sau.
        db.Plots.AddRange(
            NewPlot(householdA, ownerA, "TD001", 2000m, "Ruộng trước nhà", SoilType.Alluvial, WaterSource.RiverCanal),
            NewPlot(householdA, ownerA, "TD002", 1500m, "Ruộng sau nhà", SoilType.Loam, WaterSource.Well),
            NewPlot(householdA, ownerA, "TD003", 800m, "Vườn cạnh ao", SoilType.Clay, WaterSource.PondLake, RecordStatus.Inactive),
            NewPlot(householdB, ownerB, "TD001", 1200m, "Ruộng đầu xóm", SoilType.Sandy, WaterSource.Rainwater));
        await db.SaveChangesAsync(ct);

        await transaction.CommitAsync(ct);
        logger.LogInformation("Đã tạo dữ liệu mẫu: 1 tài khoản quản trị, 2 hộ, 8 tài khoản trong hộ, 4 thửa đất");
        return true;

        // Số điện thoại của hộ: <phonePrefix>0 là Chủ hộ, 1 và 2 là Thành viên hộ, 3 là Nhân công.
        async Task<ApplicationUser> CreateHouseholdUsersAsync(Household household, string label, string phonePrefix)
        {
            var owner = await CreateUserAsync(household.Id, UserRole.Owner, $"Chủ hộ {label}", phonePrefix + "0");
            await CreateUserAsync(
                household.Id, UserRole.Member, $"Thành viên đủ quyền {label}", phonePrefix + "1",
                canManageStock: true, canManageFinance: true, canCloseSeason: true);
            await CreateUserAsync(household.Id, UserRole.Member, $"Thành viên {label}", phonePrefix + "2");
            await CreateUserAsync(household.Id, UserRole.Worker, $"Nhân công {label}", phonePrefix + "3");
            return owner;
        }
    }

    // Tạo qua UserManager để mật khẩu được băm và tài khoản bật khóa tạm như tài khoản đăng ký thật (BR-USR-15).
    private async Task<ApplicationUser> CreateUserAsync(
        Guid? householdId,
        UserRole role,
        string fullName,
        string phone,
        bool canManageStock = false,
        bool canManageFinance = false,
        bool canCloseSeason = false)
    {
        var user = new ApplicationUser
        {
            HouseholdId = householdId,
            FullName = fullName,
            Role = role,
            UserName = phone,
            PhoneNumber = phone,
            CanManageStock = canManageStock,
            CanManageFinance = canManageFinance,
            CanCloseSeason = canCloseSeason,
        };

        var result = await userManager.CreateAsync(user, Password);
        if (!result.Succeeded)
        {
            throw new InvalidOperationException(
                "Không tạo được tài khoản mẫu: " + string.Join(", ", result.Errors.Select(e => e.Code)));
        }

        return user;
    }

    private static Plot NewPlot(
        Household household,
        ApplicationUser owner,
        string code,
        decimal areaM2,
        string locationDescription,
        SoilType soilType,
        WaterSource waterSource,
        RecordStatus status = RecordStatus.Active) => new()
    {
        HouseholdId = household.Id,
        CreatedById = owner.Id,
        Code = code,
        AreaM2 = areaM2,
        LocationDescription = locationDescription,
        SoilType = soilType,
        WaterSource = waterSource,
        Status = status,
    };
}
