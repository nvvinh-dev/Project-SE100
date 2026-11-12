using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using HouseholdFarm.Api.Common.Auditing;
using HouseholdFarm.Api.Common.Errors;
using HouseholdFarm.Api.Domain.Entities;
using HouseholdFarm.Api.Domain.Enums;
using HouseholdFarm.Api.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using static HouseholdFarm.Api.Tests.Features.Expenses.ExpenseTestData;

namespace HouseholdFarm.Api.Tests.Features.Expenses;

/// <summary>Điều chỉnh, hủy khoản chi (FR-EXP-02, BR-EXP-02, BR-EXP-03, BR-GEN-06, BR-GEN-08, BR-GEN-09, BR-GEN-11).</summary>
public class ExpenseCorrectVoidTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    [Fact(DisplayName = "BR-GEN-08: điều chỉnh số tiền, nhật ký Expense.Correct chỉ có trường amount kèm lý do")]
    public async Task Correct_Amount_LogsOnlyChangedField()
    {
        var ct = TestContext.Current.CancellationToken;
        var (household, season) = await ArrangeAsync(ct);
        var expense = await CreateExpenseAsync(factory.Postgres, household, ct, seasonId: season.Id);
        var client = factory.CreateClientFor(household.Owner);
        var version = await VersionOfAsync(client, expense.Id, ct);

        var response = await client.PostAsJsonAsync(
            $"{ExpensesPath}/{expense.Id}/correct",
            Correct(version, season.Id, amount: 180_000m),
            ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(180_000m, (await response.ReadJsonAsync(ct)).GetProperty("data").GetProperty("amount").GetDecimal());
        await using var db = factory.Postgres.CreateDbContext();
        var log = await db.AuditLogs.IgnoreQueryFilters()
            .SingleAsync(l => l.EntityId == expense.Id && l.Action == AuditActions.ExpenseCorrect, ct);
        Assert.Equal("Ghi nhầm số tiền", log.Reason);
        var oldValues = JsonDocument.Parse(log.OldValues!).RootElement;
        var newValues = JsonDocument.Parse(log.NewValues!).RootElement;
        Assert.Equal(["amount"], oldValues.EnumerateObject().Select(p => p.Name));
        Assert.Equal(150_000m, oldValues.GetProperty("amount").GetDecimal());
        Assert.Equal(180_000m, newValues.GetProperty("amount").GetDecimal());
    }

    [Fact(DisplayName = "BR-EXP-02: chuyển chi phí sang mùa vụ đã chốt trả SEASON_INVALID_STATE, khoản chi giữ nguyên")]
    public async Task Correct_ToClosedSeason_ReturnsSeasonInvalidState()
    {
        var ct = TestContext.Current.CancellationToken;
        var (household, season) = await ArrangeAsync(ct);
        var closedPlot = await TestData.CreatePlotAsync(factory.Postgres, household, "TD002", ct);
        var closed = await TestData.CreateSeasonAsync(factory.Postgres, household, closedPlot, SeasonStatus.Closed, ct);
        var expense = await CreateExpenseAsync(factory.Postgres, household, ct, seasonId: season.Id);
        var client = factory.CreateClientFor(household.Owner);
        var version = await VersionOfAsync(client, expense.Id, ct);

        var response = await client.PostAsJsonAsync($"{ExpensesPath}/{expense.Id}/correct", Correct(version, closed.Id), ct);

        await response.AssertProblemAsync(HttpStatusCode.UnprocessableEntity, ErrorCodes.SeasonInvalidState, ct);
        await using var db = factory.Postgres.CreateDbContext();
        Assert.Equal(season.Id, (await db.Expenses.IgnoreQueryFilters().SingleAsync(e => e.Id == expense.Id, ct)).SeasonId);
    }

    [Fact(DisplayName = "BR-EXP-02: khoản chi của mùa vụ đã chốt không chuyển thành chi phí chung được")]
    public async Task Correct_FromClosedSeason_ReturnsSeasonInvalidState()
    {
        var ct = TestContext.Current.CancellationToken;
        var household = await factory.CreateHouseholdAsync(ct);
        var plot = await TestData.CreatePlotAsync(factory.Postgres, household, "TD001", ct);
        var closed = await TestData.CreateSeasonAsync(factory.Postgres, household, plot, SeasonStatus.Closed, ct);
        var expense = await CreateExpenseAsync(factory.Postgres, household, ct, seasonId: closed.Id);
        var client = factory.CreateClientFor(household.Owner);
        var version = await VersionOfAsync(client, expense.Id, ct);

        var response = await client.PostAsJsonAsync($"{ExpensesPath}/{expense.Id}/correct", Correct(version, seasonId: null), ct);

        await response.AssertProblemAsync(HttpStatusCode.UnprocessableEntity, ErrorCodes.SeasonInvalidState, ct);
    }

    [Fact(DisplayName = "BR-EXP-02: chuyển chi phí chung sang mùa vụ InProgress thành công")]
    public async Task Correct_GeneralToSeason_Succeeds()
    {
        var ct = TestContext.Current.CancellationToken;
        var (household, season) = await ArrangeAsync(ct);
        var expense = await CreateExpenseAsync(factory.Postgres, household, ct);
        var client = factory.CreateClientFor(household.MemberWithAllPermissions);
        var version = await VersionOfAsync(client, expense.Id, ct);

        var response = await client.PostAsJsonAsync($"{ExpensesPath}/{expense.Id}/correct", Correct(version, season.Id), ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(season.Id, (await response.ReadJsonAsync(ct)).GetProperty("data").GetProperty("seasonId").GetGuid());
    }

    [Fact(DisplayName = "BR-GEN-11: điều chỉnh với version cũ trả CONCURRENCY_CONFLICT")]
    public async Task Correct_StaleVersion_ReturnsConcurrencyConflict()
    {
        var ct = TestContext.Current.CancellationToken;
        var (household, season) = await ArrangeAsync(ct);
        var expense = await CreateExpenseAsync(factory.Postgres, household, ct, seasonId: season.Id);
        var client = factory.CreateClientFor(household.Owner);
        var version = await VersionOfAsync(client, expense.Id, ct);
        var first = await client.PostAsJsonAsync(
            $"{ExpensesPath}/{expense.Id}/correct",
            Correct(version, season.Id, amount: 160_000m),
            ct);
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);

        var response = await client.PostAsJsonAsync(
            $"{ExpensesPath}/{expense.Id}/correct",
            Correct(version, season.Id, amount: 170_000m),
            ct);

        await response.AssertProblemAsync(HttpStatusCode.Conflict, ErrorCodes.ConcurrencyConflict, ct);
    }

    [Fact(DisplayName = "BR-GEN-06: điều chỉnh, hủy thiếu lý do trả VALIDATION_ERROR")]
    public async Task CorrectAndVoid_WithoutReason_ReturnsValidationError()
    {
        var ct = TestContext.Current.CancellationToken;
        var (household, season) = await ArrangeAsync(ct);
        var expense = await CreateExpenseAsync(factory.Postgres, household, ct, seasonId: season.Id);
        var client = factory.CreateClientFor(household.Owner);
        var version = await VersionOfAsync(client, expense.Id, ct);

        var correct = await client.PostAsJsonAsync(
            $"{ExpensesPath}/{expense.Id}/correct",
            Correct(version, season.Id, amount: 1m, reason: "   "),
            ct);
        var @void = await client.PostAsJsonAsync($"{ExpensesPath}/{expense.Id}/void", new { reason = "" }, ct);

        await correct.AssertProblemAsync(HttpStatusCode.BadRequest, ErrorCodes.ValidationError, ct);
        await @void.AssertProblemAsync(HttpStatusCode.BadRequest, ErrorCodes.ValidationError, ct);
    }

    [Fact(DisplayName = "BR-GEN-09: hủy khoản chi có nhật ký Expense.Void; hủy lần nữa trả ALREADY_VOIDED")]
    public async Task Void_Twice_ReturnsAlreadyVoided()
    {
        var ct = TestContext.Current.CancellationToken;
        var (household, season) = await ArrangeAsync(ct);
        var expense = await CreateExpenseAsync(factory.Postgres, household, ct, seasonId: season.Id);
        var client = factory.CreateClientFor(household.Owner);

        var first = await client.PostAsJsonAsync($"{ExpensesPath}/{expense.Id}/void", new { reason = "Ghi nhầm" }, ct);
        var second = await client.PostAsJsonAsync($"{ExpensesPath}/{expense.Id}/void", new { reason = "Ghi nhầm" }, ct);

        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        var data = (await first.ReadJsonAsync(ct)).GetProperty("data");
        Assert.NotEqual(JsonValueKind.Null, data.GetProperty("voidedAt").ValueKind);
        Assert.Equal("Ghi nhầm", data.GetProperty("voidReason").GetString());
        await second.AssertProblemAsync(HttpStatusCode.UnprocessableEntity, ErrorCodes.AlreadyVoided, ct);

        await using var db = factory.Postgres.CreateDbContext();
        var log = await db.AuditLogs.IgnoreQueryFilters()
            .SingleAsync(l => l.EntityId == expense.Id && l.Action == AuditActions.ExpenseVoid, ct);
        Assert.Equal("Ghi nhầm", log.Reason);
    }

    [Fact(DisplayName = "BR-GEN-09: khoản chi đã hủy không điều chỉnh được")]
    public async Task Correct_Voided_ReturnsAlreadyVoided()
    {
        var ct = TestContext.Current.CancellationToken;
        var household = await factory.CreateHouseholdAsync(ct);
        var expense = await CreateExpenseAsync(factory.Postgres, household, ct, voided: true);
        var client = factory.CreateClientFor(household.Owner);
        var version = await VersionOfAsync(client, expense.Id, ct);

        var response = await client.PostAsJsonAsync($"{ExpensesPath}/{expense.Id}/correct", Correct(version, null), ct);

        await response.AssertProblemAsync(HttpStatusCode.UnprocessableEntity, ErrorCodes.AlreadyVoided, ct);
    }

    [Fact(DisplayName = "BR-EXP-02: hủy khoản chi của mùa vụ đã chốt trả SEASON_INVALID_STATE; chi phí chung hủy được")]
    public async Task Void_DependsOnSeasonStateOnlyForSeasonExpense()
    {
        var ct = TestContext.Current.CancellationToken;
        var household = await factory.CreateHouseholdAsync(ct);
        var plot = await TestData.CreatePlotAsync(factory.Postgres, household, "TD001", ct);
        var closed = await TestData.CreateSeasonAsync(factory.Postgres, household, plot, SeasonStatus.Closed, ct);
        var seasonExpense = await CreateExpenseAsync(factory.Postgres, household, ct, seasonId: closed.Id);
        var generalExpense = await CreateExpenseAsync(factory.Postgres, household, ct);
        var client = factory.CreateClientFor(household.Owner);

        var blocked = await client.PostAsJsonAsync($"{ExpensesPath}/{seasonExpense.Id}/void", new { reason = "Ghi nhầm" }, ct);
        var general = await client.PostAsJsonAsync($"{ExpensesPath}/{generalExpense.Id}/void", new { reason = "Ghi nhầm" }, ct);

        await blocked.AssertProblemAsync(HttpStatusCode.UnprocessableEntity, ErrorCodes.SeasonInvalidState, ct);
        Assert.Equal(HttpStatusCode.OK, general.StatusCode);
    }

    [Fact(DisplayName = "Ma trận 2.5: Thành viên không có TC điều chỉnh, hủy khoản chi trả FORBIDDEN")]
    public async Task CorrectAndVoid_WithoutManageFinance_ReturnsForbidden()
    {
        var ct = TestContext.Current.CancellationToken;
        var household = await factory.CreateHouseholdAsync(ct);
        var expense = await CreateExpenseAsync(factory.Postgres, household, ct);
        var client = factory.CreateClientFor(household.Member);

        var correct = await client.PostAsJsonAsync($"{ExpensesPath}/{expense.Id}/correct", Correct(0, null), ct);
        var @void = await client.PostAsJsonAsync($"{ExpensesPath}/{expense.Id}/void", new { reason = "Ghi nhầm" }, ct);

        await correct.AssertProblemAsync(HttpStatusCode.Forbidden, ErrorCodes.Forbidden, ct);
        await @void.AssertProblemAsync(HttpStatusCode.Forbidden, ErrorCodes.Forbidden, ct);
    }

    private async Task<(TestHousehold Household, Season Season)> ArrangeAsync(CancellationToken ct)
    {
        var household = await factory.CreateHouseholdAsync(ct);
        var plot = await TestData.CreatePlotAsync(factory.Postgres, household, "TD001", ct);
        var season = await TestData.CreateSeasonAsync(factory.Postgres, household, plot, SeasonStatus.InProgress, ct);
        return (household, season);
    }

    private static async Task<uint> VersionOfAsync(HttpClient client, Guid id, CancellationToken ct)
    {
        var body = await (await client.GetAsync($"{ExpensesPath}/{id}", ct)).ReadJsonAsync(ct);
        return body.GetProperty("data").GetProperty("version").GetUInt32();
    }
}
