using HouseholdFarm.Api.Domain.Entities;
using HouseholdFarm.Api.Domain.Enums;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace HouseholdFarm.Api.Tests.Infrastructure;

/// <summary>
/// Tạo dữ liệu test thẳng trong database, không qua API. Mã hộ và số điện thoại không trùng nhau trong cả lượt chạy,
/// vì mọi test dùng chung một database.
/// </summary>
public static class TestData
{
    /// <summary>Mật khẩu của mọi tài khoản test.</summary>
    public const string Password = "MatKhauTest@2026";

    // Băm mật khẩu cố ý chậm; băm một lần dùng chung để mỗi test tạo hộ không chậm thêm vài trăm mili giây.
    private static readonly Lazy<string> _passwordHash =
        new(() => new PasswordHasher<ApplicationUser>().HashPassword(new ApplicationUser(), Password));

    private static int _householdSequence;
    private static int _phoneSequence;

    /// <summary>
    /// Tạo một hộ mới gồm Chủ hộ, hai Thành viên hộ (một người đủ ba quyền bổ sung, một người không có), một Nhân công.
    /// Hộ thứ hai trong cùng test (hộ B) tạo bằng một lần gọi nữa.
    /// </summary>
    public static async Task<TestHousehold> CreateHouseholdAsync(PostgresFixture postgres, CancellationToken ct)
    {
        var sequence = Interlocked.Increment(ref _householdSequence);
        var household = new Household
        {
            Code = $"HH{sequence:D6}",
            Name = $"Hộ test {sequence}",
            ProductionAddress = "Ấp 1, xã Tân Phú",
        };

        var owner = NewUser(household.Id, UserRole.Owner, "Chủ hộ");
        var memberWithAllPermissions = NewUser(household.Id, UserRole.Member, "Thành viên đủ quyền");
        memberWithAllPermissions.CanManageStock = true;
        memberWithAllPermissions.CanManageFinance = true;
        memberWithAllPermissions.CanCloseSeason = true;
        var member = NewUser(household.Id, UserRole.Member, "Thành viên");
        var worker = NewUser(household.Id, UserRole.Worker, "Nhân công");

        await using var db = postgres.CreateDbContext();
        db.Households.Add(household);
        db.Users.AddRange(owner, memberWithAllPermissions, member, worker);
        await db.SaveChangesAsync(ct);

        return new TestHousehold(household, owner, memberWithAllPermissions, member, worker);
    }

    /// <summary>Tạo một tài khoản Quản trị hệ thống, không thuộc hộ nào.</summary>
    public static async Task<ApplicationUser> CreateSystemAdminAsync(PostgresFixture postgres, CancellationToken ct)
    {
        var admin = NewUser(householdId: null, UserRole.SystemAdmin, "Quản trị");

        await using var db = postgres.CreateDbContext();
        db.Users.Add(admin);
        await db.SaveChangesAsync(ct);

        return admin;
    }

    /// <summary>Tạo một thửa đất <c>Active</c> của hộ, mã <paramref name="code"/>.</summary>
    public static async Task<Plot> CreatePlotAsync(
        PostgresFixture postgres,
        TestHousehold household,
        string code,
        CancellationToken ct,
        decimal areaM2 = 1000m,
        string? locationDescription = null,
        RecordStatus status = RecordStatus.Active)
    {
        var plot = new Plot
        {
            Code = code,
            AreaM2 = areaM2,
            LocationDescription = locationDescription,
            SoilType = SoilType.Alluvial,
            WaterSource = WaterSource.RiverCanal,
            Status = status,
        };

        await using var db = postgres.CreateDbContext(TestCurrentUser.For(household.Owner));
        db.Plots.Add(plot);
        await db.SaveChangesAsync(ct);
        return plot;
    }

    /// <summary>
    /// Tạo một mùa vụ trên <paramref name="plot"/> ở trạng thái <paramref name="status"/>, kèm một cây trồng mới.
    /// Có <paramref name="taskAssignee"/> thì tạo thêm một công việc giao cho người đó.
    /// </summary>
    public static async Task<Season> CreateSeasonAsync(
        PostgresFixture postgres,
        TestHousehold household,
        Plot plot,
        SeasonStatus status,
        CancellationToken ct,
        ApplicationUser? taskAssignee = null,
        Crop? crop = null)
    {
        var actualCrop = crop ?? new Crop
        {
            Name = $"Lúa test {Guid.CreateVersion7():N}",
            Group = CropGroup.Staple,
            YieldUnit = Unit.Kilogram,
            Status = RecordStatus.Active,
        };
        var season = new Season
        {
            PlotId = plot.Id,
            CropId = actualCrop.Id,
            StartDate = new DateOnly(2026, 10, 1),
            ExpectedHarvestDate = new DateOnly(2027, 1, 15),
            TargetYield = 5000m,
            Status = status,
        };

        await using var db = postgres.CreateDbContext(TestCurrentUser.For(household.Owner));
        if (crop is null)
        {
            db.Crops.Add(actualCrop);
        }
        db.Seasons.Add(season);
        if (taskAssignee is not null)
        {
            db.FarmTasks.Add(new FarmTask
            {
                SeasonId = season.Id,
                AssigneeId = taskAssignee.Id,
                Type = FarmTaskType.Watering,
                ScheduledDate = new DateOnly(2026, 10, 5),
                Status = FarmTaskStatus.Pending,
            });
        }

        await db.SaveChangesAsync(ct);
        return season;
    }

    /// <summary>Tạo cây trồng mới của hộ.</summary>
    public static async Task<Crop> CreateCropAsync(
        PostgresFixture postgres,
        TestHousehold household,
        string name,
        CancellationToken ct,
        RecordStatus status = RecordStatus.Active,
        CropGroup group = CropGroup.Staple,
        Unit yieldUnit = Unit.Kilogram)
    {
        var crop = new Crop
        {
            Name = name,
            Group = group,
            YieldUnit = yieldUnit,
            Status = status,
        };

        await using var db = postgres.CreateDbContext(TestCurrentUser.For(household.Owner));
        db.Crops.Add(crop);
        await db.SaveChangesAsync(ct);
        return crop;
    }

    /// <summary>Phân công một thành viên vào mùa vụ.</summary>
    public static async Task AssignMemberAsync(
        PostgresFixture postgres,
        TestHousehold household,
        Season season,
        ApplicationUser member,
        CancellationToken ct)
    {
        await using var db = postgres.CreateDbContext(TestCurrentUser.For(household.Owner));
        db.SeasonAssignments.Add(new SeasonAssignment
        {
            SeasonId = season.Id,
            UserId = member.Id,
        });
        await db.SaveChangesAsync(ct);
    }

    /// <summary>Bật hoặc tắt cài đặt cho phép mùa vụ chồng lấn của hộ (BR-SEA-04).</summary>
    public static async Task SetAllowOverlappingSeasonsAsync(
        PostgresFixture postgres,
        TestHousehold household,
        bool allow,
        CancellationToken ct)
    {
        await using var db = postgres.CreateDbContext(TestCurrentUser.For(household.Owner));
        var h = await db.Households.FirstAsync(h => h.Id == household.Household.Id, ct);
        h.Settings.AllowOverlappingSeasons = allow;
        await db.SaveChangesAsync(ct);
    }

    /// <summary>Số điện thoại đã chuẩn hóa (10 chữ số bắt đầu bằng 0, BR-USR-01) chưa tài khoản nào dùng.</summary>
    /// <summary>Đặt tài khoản vào trạng thái khóa tạm do đăng nhập sai nhiều lần (BR-USR-15), còn 15 phút nữa mới hết.</summary>
    public static async Task SetTemporaryLockoutAsync(PostgresFixture postgres, Guid userId, CancellationToken ct)
    {
        var lockoutEnd = TimeProvider.System.GetUtcNow().AddMinutes(15);
        await using var db = postgres.CreateDbContext();
        await db.Users.Where(u => u.Id == userId).ExecuteUpdateAsync(
            s => s.SetProperty(u => u.LockoutEnd, lockoutEnd).SetProperty(u => u.AccessFailedCount, 5),
            ct);
    }

    public static string NewPhone() => $"09{Interlocked.Increment(ref _phoneSequence):D8}";

    private static ApplicationUser NewUser(Guid? householdId, UserRole role, string fullName)
    {
        var phone = NewPhone();
        return new ApplicationUser
        {
            HouseholdId = householdId,
            FullName = fullName,
            Role = role,
            UserName = phone,
            NormalizedUserName = phone,
            PhoneNumber = phone,
            PasswordHash = _passwordHash.Value,
            SecurityStamp = Guid.CreateVersion7().ToString("N"),
            // Như tài khoản tạo bằng UserManager: khóa tạm khi đăng nhập sai nhiều lần (BR-USR-15).
            LockoutEnabled = true,
        };
    }
}
