using System.Text.Json;
using HouseholdFarm.Api.Common.Auditing;
using HouseholdFarm.Api.Data;
using HouseholdFarm.Api.Domain.Entities;
using HouseholdFarm.Api.Domain.Enums;
using HouseholdFarm.Api.Tests.Infrastructure;
using Microsoft.AspNetCore.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace HouseholdFarm.Api.Tests.Common.Auditing;

public class ChangedValuesTests(ApiFactory factory, PostgresFixture postgres) : IClassFixture<ApiFactory>
{
    private readonly Guid _householdId = Guid.CreateVersion7();

    [Fact(DisplayName = "BR-GEN-08: chỉ lấy các trường bị đổi, tên camelCase, giá trị trước và sau")]
    public async Task Of_ReturnsOnlyChangedFields()
    {
        await using var db = postgres.CreateDbContext(TestCurrentUser.Owner(_householdId));
        var expense = AttachedExpense(db);

        expense.Amount = 200_000m;
        expense.Category = ExpenseCategory.Transport;
        expense.Note = "Thuê xe chở lúa";
        // Gán lại cùng giá trị không phải là thay đổi.
        expense.ExpenseDate = new DateOnly(2026, 10, 20);

        var changes = ChangedValues.Of(db.Entry(expense));

        Assert.Equal(["amount", "category", "note"], changes.OldValues.Keys.Order());
        Assert.Equal(changes.OldValues.Keys.Order(), changes.NewValues.Keys.Order());
        Assert.Equal(150_000m, changes.OldValues["amount"]);
        Assert.Equal(200_000m, changes.NewValues["amount"]);
        Assert.Equal(ExpenseCategory.Labor, changes.OldValues["category"]);
        Assert.Equal(ExpenseCategory.Transport, changes.NewValues["category"]);
        Assert.Null(changes.OldValues["note"]);
        Assert.Equal("Thuê xe chở lúa", changes.NewValues["note"]);
    }

    [Fact(DisplayName = "BR-GEN-08: bỏ qua cột do hệ thống điền (hộ, người tạo, người sửa, version)")]
    public async Task Of_IgnoresSystemColumns()
    {
        await using var db = postgres.CreateDbContext(TestCurrentUser.Owner(_householdId));
        var expense = AttachedExpense(db);

        expense.UpdatedAt = new DateTime(2026, 10, 24, 3, 0, 0, DateTimeKind.Utc);
        expense.UpdatedById = Guid.CreateVersion7();
        expense.Version = 99;
        expense.CreatedById = Guid.CreateVersion7();

        var changes = ChangedValues.Of(db.Entry(expense));

        Assert.True(changes.IsEmpty);
        Assert.Empty(changes.NewValues);
    }

    [Fact(DisplayName = "FR-AUD-02: nhật ký Correct lưu lý do và chỉ các trường bị đổi, enum dạng chuỗi")]
    public async Task Of_WithAuditLogger_SavesOnlyChangedFieldsAsJson()
    {
        var ct = TestContext.Current.CancellationToken;
        var household = await factory.CreateHouseholdAsync(ct);
        var user = TestCurrentUser.For(household.Owner);
        var expense = NewExpense();
        await using (var createDb = postgres.CreateDbContext(user))
        {
            createDb.Expenses.Add(expense);
            await createDb.SaveChangesAsync(ct);
        }

        await using var db = postgres.CreateDbContext(user);
        var tracked = await db.Expenses.SingleAsync(e => e.Id == expense.Id, ct);
        tracked.Category = ExpenseCategory.Transport;
        var changes = ChangedValues.Of(db.Entry(tracked));
        var audit = new AuditLogger(
            db,
            user,
            TestClock.At(TimeProvider.System.GetUtcNow()),
            factory.Services.GetRequiredService<IOptions<JsonOptions>>());
        audit.Log(AuditActions.ExpenseCorrect, tracked.Id, changes.OldValues, changes.NewValues, "Ghi nhầm nhóm chi phí");
        await db.SaveChangesAsync(ct);

        await using var readDb = postgres.CreateDbContext(user);
        var log = await readDb.AuditLogs.SingleAsync(a => a.EntityId == expense.Id, ct);
        Assert.Equal("Ghi nhầm nhóm chi phí", log.Reason);
        using var oldValues = JsonDocument.Parse(log.OldValues!);
        using var newValues = JsonDocument.Parse(log.NewValues!);
        Assert.Equal(["category"], oldValues.RootElement.EnumerateObject().Select(p => p.Name));
        Assert.Equal("Labor", oldValues.RootElement.GetProperty("category").GetString());
        Assert.Equal(["category"], newValues.RootElement.EnumerateObject().Select(p => p.Name));
        Assert.Equal("Transport", newValues.RootElement.GetProperty("category").GetString());
    }

    // Bản ghi như vừa đọc từ database: giá trị lúc gắn vào DbContext là giá trị gốc để so.
    private Expense AttachedExpense(AppDbContext db)
    {
        var expense = NewExpense();
        expense.HouseholdId = _householdId;
        expense.CreatedById = Guid.CreateVersion7();
        expense.Version = 1;
        db.Expenses.Attach(expense);
        return expense;
    }

    private static Expense NewExpense() => new()
    {
        Category = ExpenseCategory.Labor,
        Amount = 150_000m,
        ExpenseDate = new DateOnly(2026, 10, 20),
        RequestId = Guid.CreateVersion7(),
    };
}
