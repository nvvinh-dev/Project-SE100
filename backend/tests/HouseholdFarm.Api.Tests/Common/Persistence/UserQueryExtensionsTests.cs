using HouseholdFarm.Api.Common.Persistence;
using HouseholdFarm.Api.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace HouseholdFarm.Api.Tests.Common.Persistence;

public class UserQueryExtensionsTests(PostgresFixture postgres)
{
    [Fact(DisplayName = "BR-GEN-02: InCurrentHousehold chỉ trả người cùng hộ, không có người hộ khác và Quản trị")]
    public async Task InCurrentHousehold_ReturnsOnlyUsersOfCurrentHousehold()
    {
        var ct = TestContext.Current.CancellationToken;
        var householdA = await TestData.CreateHouseholdAsync(postgres, ct);
        await TestData.CreateHouseholdAsync(postgres, ct);
        await TestData.CreateSystemAdminAsync(postgres, ct);
        var user = TestCurrentUser.For(householdA.Member);
        await using var db = postgres.CreateDbContext(user);

        var ids = await db.Users.InCurrentHousehold(user).Select(u => u.Id).ToListAsync(ct);

        Assert.Equal(
            new[] { householdA.Owner, householdA.MemberWithAllPermissions, householdA.Member, householdA.Worker }
                .Select(u => u.Id)
                .Order(),
            ids.Order());
    }

    [Fact(DisplayName = "BR-GEN-02: người dùng hộ khác tìm theo id qua InCurrentHousehold được xử lý như không tồn tại")]
    public async Task InCurrentHousehold_UserOfOtherHousehold_IsNotFound()
    {
        var ct = TestContext.Current.CancellationToken;
        var householdA = await TestData.CreateHouseholdAsync(postgres, ct);
        var householdB = await TestData.CreateHouseholdAsync(postgres, ct);
        var user = TestCurrentUser.For(householdA.Owner);
        await using var db = postgres.CreateDbContext(user);

        Assert.Null(await db.Users.InCurrentHousehold(user).SingleOrDefaultAsync(u => u.Id == householdB.Member.Id, ct));
    }

    [Fact(DisplayName = "Quản trị hệ thống và người chưa đăng nhập không thuộc hộ nào nên không thấy ai, kể cả Quản trị khác")]
    public async Task InCurrentHousehold_WithoutHousehold_ReturnsNobody()
    {
        var ct = TestContext.Current.CancellationToken;
        await TestData.CreateHouseholdAsync(postgres, ct);
        var admin = await TestData.CreateSystemAdminAsync(postgres, ct);
        await TestData.CreateSystemAdminAsync(postgres, ct);

        foreach (var user in new[] { TestCurrentUser.For(admin), TestCurrentUser.Anonymous })
        {
            await using var db = postgres.CreateDbContext(user);
            Assert.False(await db.Users.InCurrentHousehold(user).AnyAsync(ct));
        }
    }
}
