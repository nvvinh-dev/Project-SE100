using System.Net;
using System.Text.Json;
using HouseholdFarm.Api.Common.Errors;
using HouseholdFarm.Api.Domain.Enums;
using HouseholdFarm.Api.Tests.Infrastructure;
using static HouseholdFarm.Api.Tests.Features.Expenses.ExpenseTestData;

namespace HouseholdFarm.Api.Tests.Features.Expenses;

/// <summary>Danh sách chi phí, lọc (FR-EXP-03).</summary>
public class ExpenseListTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    [Fact(DisplayName = "FR-EXP-03: lọc theo nhóm, mùa vụ, chi phí chung, khoảng ngày; mặc định bỏ khoản chi đã hủy")]
    public async Task GetList_Filters()
    {
        var ct = TestContext.Current.CancellationToken;
        var household = await factory.CreateHouseholdAsync(ct);
        var plot = await TestData.CreatePlotAsync(factory.Postgres, household, "TD001", ct);
        var season = await TestData.CreateSeasonAsync(factory.Postgres, household, plot, SeasonStatus.InProgress, ct);
        var labor = await CreateExpenseAsync(
            factory.Postgres, household, ct, seasonId: season.Id, category: ExpenseCategory.Labor, expenseDate: new DateOnly(2026, 10, 3));
        var transport = await CreateExpenseAsync(
            factory.Postgres, household, ct, seasonId: season.Id, category: ExpenseCategory.Transport, expenseDate: new DateOnly(2026, 10, 20));
        var utilities = await CreateExpenseAsync(
            factory.Postgres, household, ct, category: ExpenseCategory.Utilities, expenseDate: new DateOnly(2026, 10, 10));
        var voided = await CreateExpenseAsync(factory.Postgres, household, ct, seasonId: season.Id, voided: true);
        var client = factory.CreateClientFor(household.Owner);

        Assert.Equal([transport.Id, utilities.Id, labor.Id], await IdsAsync(client, "", ct));
        Assert.Equal([transport.Id], await IdsAsync(client, "category=Transport", ct));
        Assert.Equal([transport.Id, labor.Id], await IdsAsync(client, $"seasonId={season.Id}", ct));
        Assert.Equal([utilities.Id], await IdsAsync(client, "general=true", ct));
        Assert.Equal([utilities.Id, labor.Id], await IdsAsync(client, "from=2026-10-01&to=2026-10-10", ct));
        Assert.Contains(voided.Id, await IdsAsync(client, "includeVoided=true", ct));
    }

    [Fact(DisplayName = "Dòng danh sách có nhãn mùa vụ; chi phí chung không có mùa vụ")]
    public async Task GetList_ItemHasSeasonLabel()
    {
        var ct = TestContext.Current.CancellationToken;
        var household = await factory.CreateHouseholdAsync(ct);
        var plot = await TestData.CreatePlotAsync(factory.Postgres, household, "TD001", ct);
        var season = await TestData.CreateSeasonAsync(factory.Postgres, household, plot, SeasonStatus.InProgress, ct);
        await CreateExpenseAsync(factory.Postgres, household, ct, seasonId: season.Id, expenseDate: new DateOnly(2026, 10, 9));
        await CreateExpenseAsync(factory.Postgres, household, ct, expenseDate: new DateOnly(2026, 10, 1));

        var body = await (await factory.CreateClientFor(household.MemberWithAllPermissions).GetAsync(ExpensesPath, ct)).ReadJsonAsync(ct);

        var items = body.GetProperty("data").EnumerateArray().ToList();
        Assert.Equal("TD001", items[0].GetProperty("plotCode").GetString());
        Assert.Equal("2026-10-01", items[0].GetProperty("seasonStartDate").GetString());
        Assert.Equal(JsonValueKind.Null, items[1].GetProperty("seasonId").ValueKind);
        Assert.Equal(2, body.GetProperty("meta").GetProperty("totalCount").GetInt32());
    }

    [Fact(DisplayName = "TC-20 (a): Nhân công xem danh sách chi phí trả FORBIDDEN")]
    public async Task TC20_Worker_ListExpenses_Forbidden()
    {
        var ct = TestContext.Current.CancellationToken;
        var household = await factory.CreateHouseholdAsync(ct);

        var response = await factory.CreateClientFor(household.Worker).GetAsync(ExpensesPath, ct);

        await response.AssertProblemAsync(HttpStatusCode.Forbidden, ErrorCodes.Forbidden, ct);
    }

    [Fact(DisplayName = "Ma trận 2.5: Thành viên không có TC xem danh sách chi phí trả FORBIDDEN")]
    public async Task GetList_MemberWithoutManageFinance_Forbidden()
    {
        var ct = TestContext.Current.CancellationToken;
        var household = await factory.CreateHouseholdAsync(ct);

        var response = await factory.CreateClientFor(household.Member).GetAsync(ExpensesPath, ct);

        await response.AssertProblemAsync(HttpStatusCode.Forbidden, ErrorCodes.Forbidden, ct);
    }

    [Theory(DisplayName = "Tham số lọc sai trả VALIDATION_ERROR")]
    [InlineData("from=2026-10-10&to=2026-10-01")]
    [InlineData("general=true&seasonId=0199a1b2-0000-7000-8000-000000000001")]
    public async Task GetList_InvalidQuery_ReturnsValidationError(string query)
    {
        var ct = TestContext.Current.CancellationToken;
        var household = await factory.CreateHouseholdAsync(ct);

        var response = await factory.CreateClientFor(household.Owner).GetAsync($"{ExpensesPath}?{query}", ct);

        await response.AssertProblemAsync(HttpStatusCode.BadRequest, ErrorCodes.ValidationError, ct);
    }

    [Fact(DisplayName = "BR-GEN-01: danh sách không có chi phí của hộ khác")]
    public async Task GetList_OtherHousehold_NotIncluded()
    {
        var ct = TestContext.Current.CancellationToken;
        var other = await factory.CreateHouseholdAsync(ct);
        await CreateExpenseAsync(factory.Postgres, other, ct);
        var household = await factory.CreateHouseholdAsync(ct);

        Assert.Empty(await IdsAsync(factory.CreateClientFor(household.Owner), "", ct));
    }

    private static async Task<List<Guid>> IdsAsync(HttpClient client, string query, CancellationToken ct)
    {
        var response = await client.GetAsync($"{ExpensesPath}?{query}", ct);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.ReadJsonAsync(ct);
        return body.GetProperty("data").EnumerateArray().Select(i => i.GetProperty("id").GetGuid()).ToList();
    }
}
