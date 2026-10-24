using HouseholdFarm.Api.Domain.Entities;
using HouseholdFarm.Api.Domain.Enums;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace HouseholdFarm.Api.Tests.Infrastructure;

public class TestDataTests(PostgresFixture postgres)
{
    [Fact(DisplayName = "Hộ test có Chủ hộ, Thành viên đủ ba quyền bổ sung, Thành viên không quyền, Nhân công")]
    public async Task CreateHousehold_SavesFourAccountsWithExpectedRoles()
    {
        var ct = TestContext.Current.CancellationToken;

        var created = await TestData.CreateHouseholdAsync(postgres, ct);

        await using var db = postgres.CreateDbContext();
        var users = await db.Users.Where(u => u.HouseholdId == created.Household.Id).ToListAsync(ct);
        Assert.Equal(4, users.Count);
        Assert.Equal(UserRole.Owner, Single(users, created.Owner).Role);
        Assert.Equal(UserRole.Worker, Single(users, created.Worker).Role);

        var memberWithAllPermissions = Single(users, created.MemberWithAllPermissions);
        Assert.Equal(UserRole.Member, memberWithAllPermissions.Role);
        Assert.True(memberWithAllPermissions is { CanManageStock: true, CanManageFinance: true, CanCloseSeason: true });

        var member = Single(users, created.Member);
        Assert.Equal(UserRole.Member, member.Role);
        Assert.True(member is { CanManageStock: false, CanManageFinance: false, CanCloseSeason: false });
    }

    [Fact(DisplayName = "Hai hộ test khác mã hộ, mọi tài khoản khác số điện thoại")]
    public async Task CreateHousehold_Twice_UsesDistinctCodesAndPhones()
    {
        var ct = TestContext.Current.CancellationToken;

        var a = await TestData.CreateHouseholdAsync(postgres, ct);
        var b = await TestData.CreateHouseholdAsync(postgres, ct);

        Assert.NotEqual(a.Household.Code, b.Household.Code);
        var phones = new[] { a, b }
            .SelectMany(h => new[] { h.Owner, h.MemberWithAllPermissions, h.Member, h.Worker })
            .Select(u => u.PhoneNumber)
            .ToList();
        Assert.Equal(phones.Count, phones.Distinct().Count());
        Assert.All(phones, p => Assert.Matches("^0[0-9]{9}$", p));
    }

    [Fact(DisplayName = "Tài khoản test kiểm tra đúng với mật khẩu test")]
    public async Task CreateHousehold_PasswordHashMatchesTestPassword()
    {
        var created = await TestData.CreateHouseholdAsync(postgres, TestContext.Current.CancellationToken);

        var result = new PasswordHasher<ApplicationUser>()
            .VerifyHashedPassword(created.Owner, created.Owner.PasswordHash!, TestData.Password);

        Assert.Equal(PasswordVerificationResult.Success, result);
    }

    [Fact(DisplayName = "Quản trị hệ thống test không thuộc hộ nào")]
    public async Task CreateSystemAdmin_HasNoHousehold()
    {
        var ct = TestContext.Current.CancellationToken;

        var admin = await TestData.CreateSystemAdminAsync(postgres, ct);

        await using var db = postgres.CreateDbContext();
        var saved = await db.Users.SingleAsync(u => u.Id == admin.Id, ct);
        Assert.Equal(UserRole.SystemAdmin, saved.Role);
        Assert.Null(saved.HouseholdId);
    }

    private static ApplicationUser Single(List<ApplicationUser> users, ApplicationUser expected) =>
        Assert.Single(users, u => u.Id == expected.Id);
}
