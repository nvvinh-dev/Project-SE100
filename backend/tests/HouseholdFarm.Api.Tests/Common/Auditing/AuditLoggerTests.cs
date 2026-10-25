using System.Text.Json;
using HouseholdFarm.Api.Common.Auditing;
using HouseholdFarm.Api.Common.Auth;
using HouseholdFarm.Api.Data;
using HouseholdFarm.Api.Domain.Entities;
using HouseholdFarm.Api.Domain.Enums;
using HouseholdFarm.Api.Tests.Infrastructure;
using Microsoft.AspNetCore.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace HouseholdFarm.Api.Tests.Common.Auditing;

public class AuditLoggerTests(ApiFactory factory, PostgresFixture postgres) : IClassFixture<ApiFactory>
{
    private static readonly DateTimeOffset _now = new(2026, 10, 24, 3, 0, 0, TimeSpan.Zero);

    [Fact(DisplayName = "BR-GEN-07: nhật ký lưu cùng thao tác, đủ hộ, người thực hiện, thời điểm, loại bản ghi")]
    public async Task Log_ThenSave_SavesAuditLogWithOperation()
    {
        var ct = TestContext.Current.CancellationToken;
        var household = await factory.CreateHouseholdAsync(ct);
        var user = TestCurrentUser.For(household.Owner);
        await using var db = postgres.CreateDbContext(user);
        var expense = NewExpense(amount: 150_000m);

        db.Expenses.Add(expense);
        CreateLogger(db, user).Log(
            AuditActions.ExpenseCreate,
            expense.Id,
            oldValues: null,
            newValues: new { expense.Amount, expense.Category, expense.ExpenseDate },
            reason: null);
        await db.SaveChangesAsync(ct);

        await using var readDb = postgres.CreateDbContext(user);
        var log = await readDb.AuditLogs.SingleAsync(a => a.EntityId == expense.Id, ct);
        Assert.Equal(household.Household.Id, log.HouseholdId);
        Assert.Equal(household.Owner.Id, log.ActorId);
        Assert.Equal("Expense.Create", log.Action);
        Assert.Equal("Expense", log.EntityType);
        Assert.Equal(_now.UtcDateTime, log.OccurredAt);
        Assert.Null(log.OldValues);
        Assert.Null(log.Reason);

        // Cùng định dạng với phản hồi API: tên camelCase, enum dạng chuỗi, ngày yyyy-MM-dd.
        using var newValues = JsonDocument.Parse(log.NewValues!);
        Assert.Equal(150_000m, newValues.RootElement.GetProperty("amount").GetDecimal());
        Assert.Equal("Labor", newValues.RootElement.GetProperty("category").GetString());
        Assert.Equal("2026-10-20", newValues.RootElement.GetProperty("expenseDate").GetString());
    }

    [Fact(DisplayName = "BR-GEN-07: thao tác lỗi khi lưu thì không có nhật ký")]
    public async Task Log_WhenSaveFails_SavesNoAuditLog()
    {
        var ct = TestContext.Current.CancellationToken;
        var household = await factory.CreateHouseholdAsync(ct);
        var user = TestCurrentUser.For(household.Owner);
        await using var db = postgres.CreateDbContext(user);
        var expense = NewExpense(amount: 0m);

        // Số tiền 0 vi phạm ràng buộc amount > 0 của database.
        db.Expenses.Add(expense);
        CreateLogger(db, user).Log(AuditActions.ExpenseCreate, expense.Id, null, new { expense.Amount }, null);
        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync(ct));

        await using var readDb = postgres.CreateDbContext(user);
        Assert.False(await readDb.AuditLogs.AnyAsync(a => a.EntityId == expense.Id, ct));
    }

    [Fact(DisplayName = "BR-GEN-07: giao dịch database bị hủy thì thao tác và nhật ký cùng bị hủy")]
    public async Task Log_WhenTransactionRolledBack_SavesNeitherOperationNorAuditLog()
    {
        var ct = TestContext.Current.CancellationToken;
        var household = await factory.CreateHouseholdAsync(ct);
        var user = TestCurrentUser.For(household.Owner);
        await using var db = postgres.CreateDbContext(user);
        var expense = NewExpense(amount: 150_000m);

        await using (var tx = await db.Database.BeginTransactionAsync(ct))
        {
            db.Expenses.Add(expense);
            CreateLogger(db, user).Log(AuditActions.ExpenseCreate, expense.Id, null, new { expense.Amount }, null);
            await db.SaveChangesAsync(ct);
            await tx.RollbackAsync(ct);
        }

        await using var readDb = postgres.CreateDbContext(user);
        Assert.False(await readDb.Expenses.AnyAsync(e => e.Id == expense.Id, ct));
        Assert.False(await readDb.AuditLogs.AnyAsync(a => a.EntityId == expense.Id, ct));
    }

    [Fact(DisplayName = "Hành động không có trong AuditActions bị từ chối, không thêm gì vào DbContext")]
    public async Task Log_UnknownAction_Throws()
    {
        var user = TestCurrentUser.Owner(Guid.CreateVersion7());
        await using var db = postgres.CreateDbContext(user);

        Assert.Throws<ArgumentException>(() =>
            CreateLogger(db, user).Log("Expense.Delete", Guid.CreateVersion7(), null, null, "Ghi nhầm"));

        Assert.Empty(db.ChangeTracker.Entries<AuditLog>());
    }

    [Fact(DisplayName = "IAuditLogger của ứng dụng thêm nhật ký vào chính AppDbContext của yêu cầu")]
    public async Task Log_FromApplicationServices_UsesScopedDbContext()
    {
        await using var scope = factory.Services.CreateAsyncScope();

        scope.ServiceProvider.GetRequiredService<IAuditLogger>()
            .Log(AuditActions.ReportExport, entityId: null, oldValues: null, newValues: new { Type = ReportType.Finance }, reason: null);

        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var log = Assert.Single(db.ChangeTracker.Entries<AuditLog>()).Entity;
        Assert.Equal("Report", log.EntityType);
        // Chưa đăng nhập (việc nền, dữ liệu mẫu) thì không có hộ, không có người thực hiện.
        Assert.Null(log.HouseholdId);
        Assert.Null(log.ActorId);
    }

    private AuditLogger CreateLogger(AppDbContext db, ICurrentUser user) =>
        new(db, user, TestClock.At(_now), factory.Services.GetRequiredService<IOptions<JsonOptions>>());

    private static Expense NewExpense(decimal amount) => new()
    {
        Category = ExpenseCategory.Labor,
        Amount = amount,
        ExpenseDate = new DateOnly(2026, 10, 20),
        RequestId = Guid.CreateVersion7(),
    };
}
