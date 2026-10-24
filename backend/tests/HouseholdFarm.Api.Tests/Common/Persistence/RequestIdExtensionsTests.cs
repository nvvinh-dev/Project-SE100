using HouseholdFarm.Api.Common.Auditing;
using HouseholdFarm.Api.Common.Persistence;
using HouseholdFarm.Api.Domain.Entities;
using HouseholdFarm.Api.Domain.Enums;
using HouseholdFarm.Api.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace HouseholdFarm.Api.Tests.Common.Persistence;

public class RequestIdExtensionsTests(PostgresFixture postgres)
{
    [Fact(DisplayName = "BR-GEN-10: gửi lại cùng requestId thì tìm được đúng bản ghi đã tạo lần trước")]
    public async Task FindByRequestId_WhenSentAgain_ReturnsExistingRecord()
    {
        var ct = TestContext.Current.CancellationToken;
        var household = await TestData.CreateHouseholdAsync(postgres, ct);
        var user = TestCurrentUser.For(household.Owner);
        var first = NewExpense(Guid.CreateVersion7());
        await SaveAsync(user, first, ct);

        await using var db = postgres.CreateDbContext(user);
        var existing = await db.Expenses.FindByRequestIdAsync(first.RequestId, ct);
        var notSentBefore = await db.Expenses.FindByRequestIdAsync(Guid.CreateVersion7(), ct);

        Assert.Equal(first.Id, existing?.Id);
        Assert.Null(notSentBefore);
    }

    [Fact(DisplayName = "BR-GEN-10: requestId chỉ duy nhất trong hộ, hộ khác dùng cùng giá trị vẫn tạo được bản ghi riêng")]
    public async Task SameRequestId_InOtherHousehold_IsNotDuplicate()
    {
        var ct = TestContext.Current.CancellationToken;
        var householdA = await TestData.CreateHouseholdAsync(postgres, ct);
        var householdB = await TestData.CreateHouseholdAsync(postgres, ct);
        var requestId = Guid.CreateVersion7();
        await SaveAsync(TestCurrentUser.For(householdA.Owner), NewExpense(requestId), ct);

        var userB = TestCurrentUser.For(householdB.Owner);
        await using var db = postgres.CreateDbContext(userB);
        Assert.Null(await db.Expenses.FindByRequestIdAsync(requestId, ct));

        var expenseB = NewExpense(requestId);
        db.Expenses.Add(expenseB);
        Assert.Null(await db.SaveChangesOrFindDuplicateAsync(expenseB, ct));
        Assert.Equal(householdB.Household.Id, expenseB.HouseholdId);
    }

    [Fact(DisplayName = "BR-GEN-10: hai yêu cầu trùng cùng qua bước tìm thì ràng buộc duy nhất chặn yêu cầu sau, trả lại bản ghi cũ")]
    public async Task SaveChangesOrFindDuplicate_WhenUniqueViolated_ReturnsExistingAndDiscardsChanges()
    {
        var ct = TestContext.Current.CancellationToken;
        var household = await TestData.CreateHouseholdAsync(postgres, ct);
        var user = TestCurrentUser.For(household.Owner);
        var requestId = Guid.CreateVersion7();

        // Yêu cầu sau đã tìm theo requestId trước khi yêu cầu đầu lưu xong, nên vẫn thêm bản ghi mới.
        await using var db = postgres.CreateDbContext(user);
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        Assert.Null(await db.Expenses.FindByRequestIdAsync(requestId, ct));
        var first = NewExpense(requestId);
        await SaveAsync(user, first, ct);

        var second = NewExpense(requestId);
        db.Expenses.Add(second);
        db.AuditLogs.Add(new AuditLog
        {
            HouseholdId = household.Household.Id,
            Action = AuditActions.ExpenseCreate,
            EntityType = nameof(Expense),
            EntityId = second.Id,
            OccurredAt = DateTime.UnixEpoch,
        });
        var duplicate = await db.SaveChangesOrFindDuplicateAsync(second, ct);

        Assert.Equal(first.Id, duplicate?.Id);
        Assert.Null(db.Database.CurrentTransaction);
        Assert.DoesNotContain(db.ChangeTracker.Entries(), e => e.State == EntityState.Added);

        await using var readDb = postgres.CreateDbContext(user);
        Assert.Equal(first.Id, (await readDb.Expenses.SingleAsync(e => e.RequestId == requestId, ct)).Id);
        Assert.False(await readDb.AuditLogs.AnyAsync(a => a.EntityId == second.Id, ct));
    }

    [Fact(DisplayName = "Vi phạm ràng buộc duy nhất khác (trùng mã thửa) vẫn là lỗi, không bị coi là gửi trùng")]
    public async Task SaveChangesOrFindDuplicate_WhenOtherUniqueViolated_Throws()
    {
        var ct = TestContext.Current.CancellationToken;
        var household = await TestData.CreateHouseholdAsync(postgres, ct);
        var user = TestCurrentUser.For(household.Owner);
        await SaveAsync(user, NewPlot("TD001"), ct);

        await using var db = postgres.CreateDbContext(user);
        var expense = NewExpense(Guid.CreateVersion7());
        db.Expenses.Add(expense);
        db.Plots.Add(NewPlot("TD001"));

        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesOrFindDuplicateAsync(expense, ct));
    }

    private async Task SaveAsync(TestCurrentUser user, object entity, CancellationToken ct)
    {
        await using var db = postgres.CreateDbContext(user);
        db.Add(entity);
        await db.SaveChangesAsync(ct);
    }

    private static Expense NewExpense(Guid requestId) => new()
    {
        Category = ExpenseCategory.Utilities,
        Amount = 320_000m,
        ExpenseDate = new DateOnly(2026, 10, 20),
        RequestId = requestId,
    };

    private static Plot NewPlot(string code) => new()
    {
        Code = code,
        AreaM2 = 1000m,
        SoilType = SoilType.Alluvial,
        WaterSource = WaterSource.RiverCanal,
        Status = RecordStatus.Active,
    };
}
