using System.Net;
using System.Net.Http.Json;
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

    [Fact(DisplayName = "Dữ liệu mẫu: tài khoản mẫu đăng nhập được qua API, số điện thoại nhập có khoảng trắng, dấu chấm (BR-USR-01)")]
    public async Task Seed_EmptyDatabase_AccountsCanLogInThroughApi()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var factory = await EmptyDatabaseFactory.CreateAsync(postgres, ct);
        Assert.True(await SeedAsync(factory, ct));
        var client = factory.CreateClient();

        var owner = await client.PostAsJsonAsync(LoginPath, new { phone = "0900 000 010", password = DevDataSeeder.Password }, ct);
        var worker = await client.PostAsJsonAsync(LoginPath, new { phone = "+84900.000.013", password = DevDataSeeder.Password }, ct);
        var admin = await client.PostAsJsonAsync(LoginPath, new { phone = "0900000001", password = DevDataSeeder.Password }, ct);

        Assert.Equal(HttpStatusCode.OK, owner.StatusCode);
        Assert.Equal(HttpStatusCode.OK, worker.StatusCode);
        Assert.Equal(HttpStatusCode.OK, admin.StatusCode);
    }

    [Fact(DisplayName = "Dữ liệu mẫu: database không ở máy mình mà thiếu Seed:AdminPassword thì dừng, không tạo gì")]
    public async Task Seed_RemoteDatabaseWithoutAdminPassword_Throws()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var factory = await EmptyDatabaseFactory.CreateAsync(postgres, ct, remoteHost: true);

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => SeedAsync(factory, ct));

        Assert.Contains(DevDataSeeder.AdminPasswordKey, error.Message);
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.Equal(0, await db.Households.CountAsync(ct));
        Assert.Equal(0, await db.Users.CountAsync(ct));
    }

    [Fact(DisplayName = "Dữ liệu mẫu: database không ở máy mình thì Quản trị dùng Seed:AdminPassword, tài khoản trong hộ dùng mật khẩu mẫu")]
    public async Task Seed_RemoteDatabaseWithAdminPassword_UsesItForAdminOnly()
    {
        var ct = TestContext.Current.CancellationToken;
        const string adminPassword = "QuanTriRieng2026";
        await using var factory = await EmptyDatabaseFactory.CreateAsync(postgres, ct, remoteHost: true, adminPassword);

        Assert.True(await SeedAsync(factory, ct));

        var client = factory.CreateClient();
        var adminWithSample = await client.PostAsJsonAsync(LoginPath, new { phone = "0900000001", password = DevDataSeeder.Password }, ct);
        var adminWithOwn = await client.PostAsJsonAsync(LoginPath, new { phone = "0900000001", password = adminPassword }, ct);
        var owner = await client.PostAsJsonAsync(LoginPath, new { phone = "0900000010", password = DevDataSeeder.Password }, ct);

        Assert.Equal(HttpStatusCode.Unauthorized, adminWithSample.StatusCode);
        Assert.Equal(HttpStatusCode.OK, adminWithOwn.StatusCode);
        Assert.Equal(HttpStatusCode.OK, owner.StatusCode);
    }

    private const string LoginPath = "/api/auth/login";

    private static async Task<bool> SeedAsync(EmptyDatabaseFactory factory, CancellationToken ct)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<DevDataSeeder>().SeedAsync(ct);
    }

    // Database dùng chung của lượt test đã có nhiều hộ, nên mỗi test dữ liệu mẫu dùng một database mới trong cùng container.
    private sealed class EmptyDatabaseFactory(PostgresFixture postgres, string connectionString, string? adminPassword)
        : ApiFactory(postgres)
    {
        /// <param name="remoteHost">
        /// Nối tới container qua 127.0.0.2: vẫn là máy này nhưng không phải tên máy chủ seeder coi là máy mình,
        /// nên đóng vai database demo.
        /// </param>
        public static async Task<EmptyDatabaseFactory> CreateAsync(
            PostgresFixture postgres,
            CancellationToken ct,
            bool remoteHost = false,
            string? adminPassword = null)
        {
            var builder = new NpgsqlConnectionStringBuilder(postgres.ConnectionString)
            {
                Database = $"seed_{Guid.CreateVersion7():N}",
            };
            if (remoteHost)
            {
                builder.Host = "127.0.0.2";
            }

            var factory = new EmptyDatabaseFactory(postgres, builder.ConnectionString, adminPassword);
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
                [DevDataSeeder.AdminPasswordKey] = adminPassword,
            }));
        }
    }
}
