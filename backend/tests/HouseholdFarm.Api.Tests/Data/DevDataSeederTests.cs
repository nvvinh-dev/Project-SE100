using HouseholdFarm.Api.Data;
using HouseholdFarm.Api.Data.Seed;
using HouseholdFarm.Api.Domain.Entities;
using HouseholdFarm.Api.Domain.Enums;
using HouseholdFarm.Api.Tests.Infrastructure;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace HouseholdFarm.Api.Tests.Data;

/// <summary>Dữ liệu mẫu chạy bằng <c>--seed</c>, trên database riêng chưa có hộ nào.</summary>
public class DevDataSeederTests(PostgresFixture postgres)
{
    [Fact(DisplayName = "Dữ liệu mẫu: tạo Quản trị hệ thống, hộ A và B đủ bốn tài khoản, thửa đất; tài khoản đăng nhập được bằng mật khẩu mẫu")]
    public async Task Seed_EmptyDatabase_CreatesAdminHouseholdsAndPlots()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var factory = await EmptyDatabaseFactory.CreateAsync(postgres, ct);

        Assert.True(await SeedAsync(factory, ct));

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var admin = Assert.Single(await db.Users.Where(u => u.Role == UserRole.SystemAdmin).ToListAsync(ct));
        Assert.Null(admin.HouseholdId);

        var households = await db.Households.OrderBy(h => h.Code).ToListAsync(ct);
        Assert.Equal(2, households.Count);
        foreach (var household in households)
        {
            var users = await db.Users.Where(u => u.HouseholdId == household.Id).ToListAsync(ct);
            Assert.Single(users, u => u.Role == UserRole.Owner);
            Assert.Single(users, u => u.Role == UserRole.Worker);
            Assert.Single(users, u => u.Role == UserRole.Member && u.CanManageStock && u.CanManageFinance && u.CanCloseSeason);
            Assert.Single(users, u => u.Role == UserRole.Member && !u.CanManageStock && !u.CanManageFinance && !u.CanCloseSeason);
            Assert.Equal(4, users.Count);
        }

        var allUsers = await db.Users.ToListAsync(ct);
        Assert.All(allUsers, u => Assert.Matches("^09000000[0-9]{2}$", u.PhoneNumber!));
        Assert.All(allUsers, u => Assert.True(u.LockoutEnabled));
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        Assert.True(await userManager.CheckPasswordAsync(admin, DevDataSeeder.Password));

        // Database mới chưa có người đăng nhập nên đọc thửa bỏ qua Global Query Filter để thấy cả hai hộ.
        var plots = await db.Plots.IgnoreQueryFilters().ToListAsync(ct);
        var plotsA = plots.Where(p => p.HouseholdId == households[0].Id).ToList();
        Assert.Contains(plotsA, p => p.Status == RecordStatus.Active);
        Assert.Contains(plotsA, p => p.Status == RecordStatus.Inactive);
        Assert.Contains(plots, p => p.HouseholdId == households[1].Id);
        Assert.All(plots, p => Assert.Contains(allUsers, u => u.Id == p.CreatedById && u.HouseholdId == p.HouseholdId));
    }

    [Fact(DisplayName = "Dữ liệu mẫu: database đã có hộ thì bỏ qua, chạy lại không nhân đôi dữ liệu")]
    public async Task Seed_DatabaseWithHousehold_DoesNothing()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var factory = await EmptyDatabaseFactory.CreateAsync(postgres, ct);
        Assert.True(await SeedAsync(factory, ct));

        Assert.False(await SeedAsync(factory, ct));

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.Equal(2, await db.Households.CountAsync(ct));
        Assert.Equal(9, await db.Users.CountAsync(ct));
        Assert.Equal(4, await db.Plots.IgnoreQueryFilters().CountAsync(ct));
    }

    private static async Task<bool> SeedAsync(EmptyDatabaseFactory factory, CancellationToken ct)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<DevDataSeeder>().SeedAsync(ct);
    }

    // Database dùng chung của lượt test đã có nhiều hộ, nên mỗi test dữ liệu mẫu dùng một database mới trong cùng container.
    private sealed class EmptyDatabaseFactory(PostgresFixture postgres, string connectionString) : ApiFactory(postgres)
    {
        public static async Task<EmptyDatabaseFactory> CreateAsync(PostgresFixture postgres, CancellationToken ct)
        {
            var connectionString = new NpgsqlConnectionStringBuilder(postgres.ConnectionString)
            {
                Database = $"seed_{Guid.CreateVersion7():N}",
            }.ConnectionString;

            var factory = new EmptyDatabaseFactory(postgres, connectionString);
            await using var scope = factory.Services.CreateAsyncScope();
            await scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.MigrateAsync(ct);
            return factory;
        }

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            base.ConfigureWebHost(builder);
            builder.ConfigureAppConfiguration(config => config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:Default"] = connectionString,
            }));
        }
    }
}
