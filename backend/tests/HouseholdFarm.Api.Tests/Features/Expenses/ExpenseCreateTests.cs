using System.Net;
using System.Net.Http.Json;
using HouseholdFarm.Api.Common.Auditing;
using HouseholdFarm.Api.Common.Errors;
using HouseholdFarm.Api.Domain.Entities;
using HouseholdFarm.Api.Domain.Enums;
using HouseholdFarm.Api.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using static HouseholdFarm.Api.Tests.Features.Expenses.ExpenseTestData;

namespace HouseholdFarm.Api.Tests.Features.Expenses;

/// <summary>Ghi chi phí (FR-EXP-01, BR-EXP-01, BR-EXP-02).</summary>
public class ExpenseCreateTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    [Fact(DisplayName = "FR-EXP-01: ghi chi phí cho mùa vụ InProgress thành công, có nhật ký Expense.Create")]
    public async Task Create_SeasonExpense_Succeeds()
    {
        var ct = TestContext.Current.CancellationToken;
        var (household, season) = await ArrangeAsync(SeasonStatus.InProgress, ct);
        var client = factory.CreateClientFor(household.Owner);

        // BR-EXP-02: ngày phát sinh được trước ngày bắt đầu mùa vụ (2026-10-01).
        var response = await client.PostAsJsonAsync(ExpensesPath, Create(season.Id, expenseDate: new DateOnly(2026, 9, 20)), ct);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var data = (await response.ReadJsonAsync(ct)).GetProperty("data");
        Assert.Equal(season.Id, data.GetProperty("seasonId").GetGuid());
        Assert.Equal("TD001", data.GetProperty("plotCode").GetString());
        Assert.Equal("Labor", data.GetProperty("category").GetString());
        Assert.Equal(150_000m, data.GetProperty("amount").GetDecimal());
        Assert.Equal("2026-09-20", data.GetProperty("expenseDate").GetString());

        var id = data.GetProperty("id").GetGuid();
        Assert.Equal($"{ExpensesPath}/{id}", response.Headers.Location?.AbsolutePath);
        await using var db = factory.Postgres.CreateDbContext();
        var log = await db.AuditLogs.IgnoreQueryFilters().SingleAsync(l => l.EntityId == id, ct);
        Assert.Equal(AuditActions.ExpenseCreate, log.Action);
        Assert.Equal(household.Household.Id, log.HouseholdId);
    }

    [Theory(DisplayName = "BR-EXP-02: chi phí chung của hộ không phụ thuộc trạng thái mùa vụ")]
    [InlineData("Utilities")]
    [InlineData("Transport")]
    public async Task Create_GeneralExpense_Succeeds(string category)
    {
        var ct = TestContext.Current.CancellationToken;
        var household = await factory.CreateHouseholdAsync(ct);

        var response = await factory.CreateClientFor(household.Owner)
            .PostAsJsonAsync(ExpensesPath, Create(seasonId: null, category: category), ct);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var data = (await response.ReadJsonAsync(ct)).GetProperty("data");
        Assert.Equal(System.Text.Json.JsonValueKind.Null, data.GetProperty("seasonId").ValueKind);
        Assert.Equal(category, data.GetProperty("category").GetString());
    }

    [Fact(DisplayName = "Nhóm Khác giữ ghi chú nhóm; nhóm khác bỏ ghi chú nhóm")]
    public async Task Create_CategoryOther_KeepsNoteOnlyForOther()
    {
        var ct = TestContext.Current.CancellationToken;
        var household = await factory.CreateHouseholdAsync(ct);
        var client = factory.CreateClientFor(household.Owner);

        var other = await client.PostAsJsonAsync(ExpensesPath, Create(null, category: "Other", categoryOther: " Phí thủy lợi "), ct);
        var labor = await client.PostAsJsonAsync(ExpensesPath, Create(null, category: "Labor", categoryOther: "Không dùng"), ct);

        Assert.Equal("Phí thủy lợi", (await other.ReadJsonAsync(ct)).GetProperty("data").GetProperty("categoryOther").GetString());
        Assert.Equal(
            System.Text.Json.JsonValueKind.Null,
            (await labor.ReadJsonAsync(ct)).GetProperty("data").GetProperty("categoryOther").ValueKind);
    }

    [Theory(DisplayName = "BR-EXP-01: số tiền không lớn hơn 0, thiếu nhóm, nhóm vật tư, ngày sau hôm nay trả VALIDATION_ERROR")]
    [InlineData("amount")]
    [InlineData("missingCategory")]
    [InlineData("supplyCategory")]
    [InlineData("futureDate")]
    public async Task Create_InvalidData_ReturnsValidationError(string invalid)
    {
        var ct = TestContext.Current.CancellationToken;
        var household = await factory.CreateHouseholdAsync(ct);
        var tomorrow = DateOnly.FromDateTime(TimeProvider.System.GetUtcNow().UtcDateTime).AddDays(2);
        object body = invalid switch
        {
            "amount" => Create(null, amount: 0m),
            "missingCategory" => new { requestId = Guid.CreateVersion7(), amount = 1000m, expenseDate = "2026-10-05" },
            "supplyCategory" => Create(null, category: "Fertilizer"),
            _ => Create(null, expenseDate: tomorrow),
        };

        var response = await factory.CreateClientFor(household.Owner).PostAsJsonAsync(ExpensesPath, body, ct);

        await response.AssertProblemAsync(HttpStatusCode.BadRequest, ErrorCodes.ValidationError, ct);
        await using var db = factory.Postgres.CreateDbContext();
        Assert.False(await db.Expenses.IgnoreQueryFilters().AnyAsync(e => e.HouseholdId == household.Household.Id, ct));
    }

    [Theory(DisplayName = "BR-EXP-02: ghi chi phí cho mùa vụ không InProgress trả SEASON_INVALID_STATE")]
    [InlineData(SeasonStatus.Planned)]
    [InlineData(SeasonStatus.Closed)]
    [InlineData(SeasonStatus.Cancelled)]
    public async Task Create_SeasonNotInProgress_ReturnsSeasonInvalidState(SeasonStatus status)
    {
        var ct = TestContext.Current.CancellationToken;
        var (household, season) = await ArrangeAsync(status, ct);

        var response = await factory.CreateClientFor(household.Owner).PostAsJsonAsync(ExpensesPath, Create(season.Id), ct);

        await response.AssertProblemAsync(HttpStatusCode.UnprocessableEntity, ErrorCodes.SeasonInvalidState, ct);
    }

    [Fact(DisplayName = "BR-USR-13: TV+TC ghi chi phí cho mùa vụ ngoài phân công thành công")]
    public async Task Create_ManageFinanceOutOfScope_Succeeds()
    {
        var ct = TestContext.Current.CancellationToken;
        var (household, season) = await ArrangeAsync(SeasonStatus.InProgress, ct);

        var response = await factory.CreateClientFor(household.MemberWithAllPermissions)
            .PostAsJsonAsync(ExpensesPath, Create(season.Id), ct);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    [Fact(DisplayName = "Ma trận 2.5: Thành viên không có TC, Nhân công ghi chi phí trả FORBIDDEN")]
    public async Task Create_WithoutManageFinance_ReturnsForbidden()
    {
        var ct = TestContext.Current.CancellationToken;
        var (household, season) = await ArrangeAsync(SeasonStatus.InProgress, ct);
        await TestData.AssignMemberAsync(factory.Postgres, household, season, household.Member, ct);

        foreach (var user in new[] { household.Member, household.Worker })
        {
            var response = await factory.CreateClientFor(user).PostAsJsonAsync(ExpensesPath, Create(season.Id), ct);
            await response.AssertProblemAsync(HttpStatusCode.Forbidden, ErrorCodes.Forbidden, ct);
        }
    }

    [Fact(DisplayName = "BR-GEN-02: mùa vụ của hộ khác trả NOT_FOUND")]
    public async Task Create_SeasonOfOtherHousehold_ReturnsNotFound()
    {
        var ct = TestContext.Current.CancellationToken;
        var (_, otherSeason) = await ArrangeAsync(SeasonStatus.InProgress, ct);
        var household = await factory.CreateHouseholdAsync(ct);

        var response = await factory.CreateClientFor(household.Owner).PostAsJsonAsync(ExpensesPath, Create(otherSeason.Id), ct);

        await response.AssertProblemAsync(HttpStatusCode.NotFound, ErrorCodes.NotFound, ct);
    }

    [Fact(DisplayName = "BR-GEN-10: gửi trùng requestId chỉ tạo một khoản chi, lần sau trả lại khoản chi đó")]
    public async Task Create_SameRequestIdTwice_CreatesOnce()
    {
        var ct = TestContext.Current.CancellationToken;
        var (household, season) = await ArrangeAsync(SeasonStatus.InProgress, ct);
        var client = factory.CreateClientFor(household.Owner);
        var body = Create(season.Id, requestId: Guid.CreateVersion7());

        var responses = await Task.WhenAll(
            client.PostAsJsonAsync(ExpensesPath, body, ct),
            client.PostAsJsonAsync(ExpensesPath, body, ct));
        var again = await client.PostAsJsonAsync(ExpensesPath, body, ct);

        var ids = new List<Guid>();
        foreach (var response in responses.Append(again))
        {
            Assert.Equal(HttpStatusCode.Created, response.StatusCode);
            ids.Add((await response.ReadJsonAsync(ct)).GetProperty("data").GetProperty("id").GetGuid());
        }

        Assert.Single(ids.Distinct());
        await using var db = factory.Postgres.CreateDbContext();
        Assert.Equal(1, await db.Expenses.IgnoreQueryFilters().CountAsync(e => e.HouseholdId == household.Household.Id, ct));
        Assert.Equal(1, await db.AuditLogs.IgnoreQueryFilters().CountAsync(l => l.EntityId == ids[0], ct));
    }

    [Fact(DisplayName = "Xem chi tiết khoản chi; khoản chi của hộ khác trả NOT_FOUND")]
    public async Task Get_ReturnsExpense_OtherHouseholdNotFound()
    {
        var ct = TestContext.Current.CancellationToken;
        var (household, season) = await ArrangeAsync(SeasonStatus.InProgress, ct);
        var expense = await CreateExpenseAsync(factory.Postgres, household, ct, seasonId: season.Id);
        var other = await factory.CreateHouseholdAsync(ct);

        var mine = await factory.CreateClientFor(household.MemberWithAllPermissions).GetAsync($"{ExpensesPath}/{expense.Id}", ct);
        var theirs = await factory.CreateClientFor(other.Owner).GetAsync($"{ExpensesPath}/{expense.Id}", ct);

        Assert.Equal(HttpStatusCode.OK, mine.StatusCode);
        var data = (await mine.ReadJsonAsync(ct)).GetProperty("data");
        Assert.Equal("InProgress", data.GetProperty("seasonStatus").GetString());
        await theirs.AssertProblemAsync(HttpStatusCode.NotFound, ErrorCodes.NotFound, ct);
    }

    private async Task<(TestHousehold Household, Season Season)> ArrangeAsync(SeasonStatus status, CancellationToken ct)
    {
        var household = await factory.CreateHouseholdAsync(ct);
        var plot = await TestData.CreatePlotAsync(factory.Postgres, household, "TD001", ct);
        var season = await TestData.CreateSeasonAsync(factory.Postgres, household, plot, status, ct);
        return (household, season);
    }
}
