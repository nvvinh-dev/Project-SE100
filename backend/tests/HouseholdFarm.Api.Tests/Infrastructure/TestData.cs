using HouseholdFarm.Api.Domain.Entities;
using HouseholdFarm.Api.Domain.Enums;
using Microsoft.AspNetCore.Identity;

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

    private static ApplicationUser NewUser(Guid? householdId, UserRole role, string fullName)
    {
        // Số điện thoại đã chuẩn hóa: 10 chữ số bắt đầu bằng 0 (BR-USR-01).
        var phone = $"09{Interlocked.Increment(ref _phoneSequence):D8}";
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
