using HouseholdFarm.Api.Domain.Entities;
using HouseholdFarm.Api.Domain.Enums;
using HouseholdFarm.Api.Tests.Infrastructure;

namespace HouseholdFarm.Api.Tests.Features.Expenses;

/// <summary>Tạo khoản chi thẳng trong database, không qua API, và dựng body yêu cầu của API chi phí.</summary>
public static class ExpenseTestData
{
    public const string ExpensesPath = "/api/expenses";

    public static async Task<Expense> CreateExpenseAsync(
        PostgresFixture postgres,
        TestHousehold household,
        CancellationToken ct,
        Guid? seasonId = null,
        ExpenseCategory category = ExpenseCategory.Labor,
        decimal amount = 150_000m,
        DateOnly? expenseDate = null,
        bool voided = false)
    {
        var expense = new Expense
        {
            SeasonId = seasonId,
            Category = category,
            Amount = amount,
            ExpenseDate = expenseDate ?? new DateOnly(2026, 10, 5),
            RequestId = Guid.CreateVersion7(),
            VoidedAt = voided ? TimeProvider.System.GetUtcNow().UtcDateTime : null,
            VoidReason = voided ? "Ghi nhầm" : null,
        };

        await using var db = postgres.CreateDbContext(TestCurrentUser.For(household.Owner));
        db.Expenses.Add(expense);
        await db.SaveChangesAsync(ct);
        return expense;
    }

    public static object Create(
        Guid? seasonId,
        decimal amount = 150_000m,
        string category = "Labor",
        DateOnly? expenseDate = null,
        Guid? requestId = null,
        string? categoryOther = null) => new
    {
        requestId = requestId ?? Guid.CreateVersion7(),
        seasonId,
        category,
        categoryOther,
        amount,
        expenseDate = expenseDate ?? new DateOnly(2026, 10, 5),
        note = "Thuê làm cỏ",
    };

    public static object Correct(
        uint version,
        Guid? seasonId,
        decimal amount = 150_000m,
        string category = "Labor",
        string reason = "Ghi nhầm số tiền") => new
    {
        seasonId,
        category,
        amount,
        expenseDate = new DateOnly(2026, 10, 5),
        reason,
        version,
    };
}
