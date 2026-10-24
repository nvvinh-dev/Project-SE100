using HouseholdFarm.Api.Common.Auth;
using HouseholdFarm.Api.Common.Persistence;
using HouseholdFarm.Api.Data;
using HouseholdFarm.Api.Domain.Entities;
using HouseholdFarm.Api.Domain.Enums;
using HouseholdFarm.Api.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace HouseholdFarm.Api.Tests.Common.Persistence;

public class CommonColumnsInterceptorTests
{
    private static readonly DateTimeOffset _now = new(2026, 10, 23, 2, 0, 0, TimeSpan.Zero);

    private readonly Guid _householdId = Guid.CreateVersion7();

    [Fact(DisplayName = "BR-GEN-01: thêm bản ghi thì hộ lấy từ người đang đăng nhập, ghi đè giá trị đã gán sẵn")]
    public async Task Add_OverridesHouseholdAndFillsCreatedColumns()
    {
        var user = TestCurrentUser.Owner(_householdId);
        await using var db = CreateDbContext(user);
        var plot = NewPlot();
        plot.HouseholdId = Guid.CreateVersion7();

        db.Plots.Add(plot);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        Assert.Equal(_householdId, plot.HouseholdId);
        Assert.Equal(user.UserId, plot.CreatedById);
        Assert.Equal(_now.UtcDateTime, plot.CreatedAt);
        Assert.Null(plot.UpdatedAt);
        Assert.Null(plot.UpdatedById);
    }

    [Fact(DisplayName = "Thêm bản ghi không có người tạo (ai_usages) vẫn được điền hộ và created_at")]
    public async Task Add_FillsCreatedAtForEntityWithoutCreatedBy()
    {
        var user = TestCurrentUser.Owner(_householdId);
        await using var db = CreateDbContext(user);
        var usage = new AIUsage { UserId = user.UserId, Feature = AIFeature.Assistant, UsageDate = new DateOnly(2026, 10, 23) };

        db.AIUsages.Add(usage);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        Assert.Equal(_householdId, usage.HouseholdId);
        Assert.Equal(_now.UtcDateTime, usage.CreatedAt);
    }

    [Fact(DisplayName = "Sửa bản ghi thì điền updated_at và người sửa")]
    public async Task Update_FillsUpdatedColumns()
    {
        var user = TestCurrentUser.Owner(_householdId);
        await using var db = CreateDbContext(user);
        var plot = NewPlot();
        plot.HouseholdId = _householdId;
        plot.CreatedById = user.UserId;
        db.Plots.Attach(plot);

        plot.Note = "Đã cày ải";
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        Assert.Equal(_now.UtcDateTime, plot.UpdatedAt);
        Assert.Equal(user.UserId, plot.UpdatedById);
    }

    [Fact(DisplayName = "BR-GEN-01: sửa bản ghi thì hộ, người tạo, thời điểm tạo giữ nguyên dù service gán giá trị khác")]
    public async Task Update_KeepsCreationColumns()
    {
        var user = TestCurrentUser.Owner(_householdId);
        await using var db = CreateDbContext(user);
        var createdAt = _now.UtcDateTime.AddDays(-1);
        var plot = NewPlot();
        plot.HouseholdId = _householdId;
        plot.CreatedById = user.UserId;
        plot.CreatedAt = createdAt;
        db.Plots.Attach(plot);

        plot.Note = "Đã cày ải";
        plot.HouseholdId = Guid.CreateVersion7();
        plot.CreatedById = Guid.CreateVersion7();
        plot.CreatedAt = _now.UtcDateTime;
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var entry = db.Entry(plot);
        Assert.Equal(_householdId, plot.HouseholdId);
        Assert.Equal(user.UserId, plot.CreatedById);
        Assert.Equal(createdAt, plot.CreatedAt);
        Assert.False(entry.Property(p => p.HouseholdId).IsModified);
        Assert.False(entry.Property(p => p.CreatedById).IsModified);
        Assert.False(entry.Property(p => p.CreatedAt).IsModified);
        Assert.True(entry.Property(p => p.Note).IsModified);
        Assert.Equal(_now.UtcDateTime, plot.UpdatedAt);
    }

    [Fact(DisplayName = "Sửa mỗi hộ của bản ghi thì không có gì được ghi, kể cả updated_at")]
    public async Task Update_OnlyHousehold_LeavesEntityUnchanged()
    {
        var user = TestCurrentUser.Owner(_householdId);
        await using var db = CreateDbContext(user);
        var plot = NewPlot();
        plot.HouseholdId = _householdId;
        plot.CreatedById = user.UserId;
        db.Plots.Attach(plot);

        plot.HouseholdId = Guid.CreateVersion7();
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        Assert.Equal(EntityState.Unchanged, db.Entry(plot).State);
        Assert.Equal(_householdId, plot.HouseholdId);
        Assert.Null(plot.UpdatedAt);
    }

    [Fact(DisplayName = "Chỉ đổi cài đặt hộ (owned type) vẫn cập nhật updated_at của hộ")]
    public async Task Update_OwnedSettingsOnly_FillsHouseholdUpdatedColumns()
    {
        var user = TestCurrentUser.Owner(_householdId);
        await using var db = CreateDbContext(user);
        var household = new Household { Id = _householdId, Code = "HH000001", Name = "Hộ A", ProductionAddress = "Ấp 1" };
        db.Households.Attach(household);

        household.Settings.AllowNegativeStock = true;
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        Assert.Equal(_now.UtcDateTime, household.UpdatedAt);
        Assert.Equal(user.UserId, household.UpdatedById);
    }

    [Fact(DisplayName = "Chưa đăng nhập và chưa gán hộ thì báo lỗi, không lưu bản ghi có hộ rỗng")]
    public async Task Add_WithoutUserAndWithoutPresetHousehold_Throws()
    {
        await using var db = CreateDbContext(TestCurrentUser.Anonymous);
        db.Plots.Add(NewPlot());

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => db.SaveChangesAsync(TestContext.Current.CancellationToken));
    }

    [Fact(DisplayName = "BR-GEN-01: Quản trị hệ thống thêm bản ghi của hộ thì báo lỗi, kể cả khi đã gán sẵn hộ")]
    public async Task Add_BySystemAdmin_Throws()
    {
        await using var db = CreateDbContext(TestCurrentUser.SystemAdmin());
        var plot = NewPlot();
        plot.HouseholdId = _householdId;
        db.Plots.Add(plot);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => db.SaveChangesAsync(TestContext.Current.CancellationToken));
    }

    [Fact(DisplayName = "Chưa đăng nhập (dữ liệu mẫu) thì giữ hộ và người tạo đã gán sẵn")]
    public async Task Add_WithoutUser_KeepsPresetValues()
    {
        var creatorId = Guid.CreateVersion7();
        await using var db = CreateDbContext(TestCurrentUser.Anonymous);
        var plot = NewPlot();
        plot.HouseholdId = _householdId;
        plot.CreatedById = creatorId;

        db.Plots.Add(plot);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        Assert.Equal(_householdId, plot.HouseholdId);
        Assert.Equal(creatorId, plot.CreatedById);
        Assert.Equal(_now.UtcDateTime, plot.CreatedAt);
    }

    private static AppDbContext CreateDbContext(ICurrentUser currentUser)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql("Host=localhost;Database=unused")
            .UseSnakeCaseNamingConvention()
            .AddInterceptors(new CommonColumnsInterceptor(currentUser, TestClock.At(_now)), new SuppressSaveInterceptor())
            .Options;
        return new AppDbContext(options, currentUser);
    }

    private static Plot NewPlot() => new()
    {
        Code = "TD001",
        AreaM2 = 1000m,
        SoilType = SoilType.Alluvial,
        WaterSource = WaterSource.RiverCanal,
        Status = RecordStatus.Active,
    };

    // Chạy sau interceptor điền cột chung và chặn lệnh ghi xuống database, để test không cần database.
    private sealed class SuppressSaveInterceptor : SaveChangesInterceptor
    {
        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData,
            InterceptionResult<int> result,
            CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(InterceptionResult<int>.SuppressWithResult(0));
    }
}
