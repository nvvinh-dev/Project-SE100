using HouseholdFarm.Api.Data;
using HouseholdFarm.Api.Tests.Infrastructure;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace HouseholdFarm.Api.Tests.Data;

// Kiểm tra model EF dựng từ đúng cấu hình trong Program.cs; không mở kết nối database.
public class AppDbContextModelTests(WebApplicationFactory<Program> factory)
    : IClassFixture<WebApplicationFactory<Program>>
{
    private static readonly string[] _householdFilteredTables =
    [
        "ai_season_summaries", "ai_usages", "audit_logs", "crops", "customers", "expenses", "farm_task_supply_usages",
        "farm_tasks", "harvest_records", "payments", "plots", "produce_stock_adjustments", "report_exports",
        "sale_harvest_links", "sales", "season_assignments", "season_notes", "season_supply_plans", "seasons",
        "stock_transactions", "supplies",
    ];

    private static readonly string[] _unfilteredTables =
    [
        "asp_net_user_claims", "asp_net_user_logins", "asp_net_user_tokens", "asp_net_users", "households", "refresh_tokens",
    ];

    // Chuỗi kết nối giả chỉ để dựng DbContext; các test ở đây không mở kết nối.
    private readonly WebApplicationFactory<Program> _factory = factory.WithWebHostBuilder(b => b
        .UseEnvironment("Production")
        .ConfigureAppConfiguration(config => config.AddInMemoryCollection(
            new Dictionary<string, string?> { ["ConnectionStrings:Default"] = "Host=localhost;Database=model_only" })));

    [Fact(DisplayName = "Model có đúng 24 bảng của data model cùng 3 bảng phụ của Identity")]
    public void Model_HasExpectedTables()
    {
        var model = GetDesignTimeModel();

        var tables = model.GetEntityTypes().Select(e => e.GetTableName()).Distinct().Order();

        Assert.Equal(_householdFilteredTables.Concat(_unfilteredTables).Order(), tables);
    }

    [Fact(DisplayName = "Mọi bảng nghiệp vụ có Global Query Filter theo hộ; asp_net_users, households, refresh_tokens thì không")]
    public void Model_AppliesHouseholdFilterToBusinessTables()
    {
        var model = GetDesignTimeModel();

        var filteredTables = model.GetEntityTypes()
            .Where(e => e.GetDeclaredQueryFilters().Any(f => f.Key == AppDbContext.HouseholdFilter))
            .Select(e => e.GetTableName())
            .Order();

        Assert.Equal(_householdFilteredTables.Order(), filteredTables);
    }

    [Fact(DisplayName = "Bảng có updated_at dùng cột hệ thống xmin làm concurrency token")]
    public void Model_MapsVersionToXminForUpdatableTables()
    {
        var model = GetDesignTimeModel();
        var entityTypes = model.GetEntityTypes().Where(e => !e.IsOwned()).ToList();

        var withUpdatedAt = entityTypes.Where(e => e.FindProperty("UpdatedAt") is not null).Select(e => e.GetTableName());
        var withVersion = entityTypes.Where(e => e.FindProperty("Version") is not null).ToList();

        Assert.Equal(withUpdatedAt.Order(), withVersion.Select(e => e.GetTableName()).Order());
        Assert.Equal(17, withVersion.Count);
        Assert.All(withVersion, e =>
        {
            var version = e.FindProperty("Version")!;
            Assert.Equal("xmin", version.GetColumnName());
            Assert.True(version.IsConcurrencyToken);
            Assert.Equal(ValueGenerated.OnAddOrUpdate, version.ValueGenerated);
        });
    }

    [Fact(DisplayName = "Khóa ngoại của bảng dự án không xóa dây chuyền")]
    public void Model_UsesRestrictForProjectForeignKeys()
    {
        var model = GetDesignTimeModel();

        var foreignKeys = model.GetEntityTypes()
            .Where(e => e.ClrType.Assembly == typeof(AppDbContext).Assembly)
            .SelectMany(e => e.GetDeclaredForeignKeys())
            .Where(fk => !fk.IsOwnership)
            .ToList();

        Assert.NotEmpty(foreignKeys);
        Assert.All(foreignKeys, fk => Assert.Equal(DeleteBehavior.Restrict, fk.DeleteBehavior));
    }

    [Fact(DisplayName = "Model khớp migration mới nhất: đổi entity hay cấu hình mà quên tạo migration thì test đỏ")]
    public void Model_HasNoPendingChanges()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        Assert.False(db.Database.HasPendingModelChanges());
    }

    [Fact(DisplayName = "BR-GEN-01: truy vấn bảng nghiệp vụ tự lọc theo hộ của người đang đăng nhập")]
    public void Query_FiltersByCurrentHousehold()
    {
        var householdId = Guid.CreateVersion7();
        using var scope = _factory.Services.CreateScope();
        var options = scope.ServiceProvider.GetRequiredService<DbContextOptions<AppDbContext>>();
        using var db = new AppDbContext(options, TestCurrentUser.Owner(householdId));

        var plotsSql = db.Plots.ToQueryString();
        var auditLogsSql = db.AuditLogs.ToQueryString();

        Assert.Contains("household_id = @", plotsSql);
        Assert.Contains(householdId.ToString(), plotsSql);
        Assert.Contains("household_id = @", auditLogsSql);
        Assert.Contains(householdId.ToString(), auditLogsSql);
    }

    [Fact(DisplayName = "BR-GEN-02: người không thuộc hộ nào không thấy các dòng nhật ký không có hộ")]
    public void Query_AuditLogsWithoutHousehold_HiddenFromUserWithoutHousehold()
    {
        using var scope = _factory.Services.CreateScope();
        var options = scope.ServiceProvider.GetRequiredService<DbContextOptions<AppDbContext>>();
        using var db = new AppDbContext(options, TestCurrentUser.Anonymous);

        var sql = db.AuditLogs.ToQueryString();

        Assert.DoesNotContain("household_id IS NULL", sql);
    }

    // Model đầy đủ (gồm CHECK, filter) dùng khi tạo migration; model chạy thật có thể lược bớt thông tin này.
    private IModel GetDesignTimeModel()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return db.GetService<IDesignTimeModel>().Model;
    }
}
