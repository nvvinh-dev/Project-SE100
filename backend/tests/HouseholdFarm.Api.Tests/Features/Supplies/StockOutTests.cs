using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using HouseholdFarm.Api.Common.Auditing;
using HouseholdFarm.Api.Common.Errors;
using HouseholdFarm.Api.Common.Responses;
using HouseholdFarm.Api.Domain.Entities;
using HouseholdFarm.Api.Domain.Enums;
using HouseholdFarm.Api.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace HouseholdFarm.Api.Tests.Features.Supplies;

/// <summary>Xuất kho thủ công (FR-SUP-04), giá vốn khi xuất (FR-SUP-05), kiểm tra tồn (FR-SUP-07).</summary>
public class StockOutTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private const string TransactionsPath = "/api/stock-transactions";
    private const string SuppliesPath = "/api/supplies";

    [Fact(DisplayName = "TC-04: tồn 100 kg, ngưỡng 30 kg, xuất 80 kg thành công, tồn 20 kg, cảnh báo LOW_STOCK")]
    public async Task TC04_StockOut_BelowMinimum_WarnsLowStock()
    {
        var ct = TestContext.Current.CancellationToken;
        var (household, supply, season, client) = await ArrangeAsync(ct, stock: 100m, unitCost: 1000m, minStockLevel: 30m);

        var response = await client.PostAsJsonAsync(TransactionsPath, StockOut(supply.Id, season.Id, 80m), ct);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await response.ReadJsonAsync(ct);
        Assert.Equal(-80m, body.GetProperty("data").GetProperty("quantity").GetDecimal());
        Assert.Equal(season.Id, body.GetProperty("data").GetProperty("seasonId").GetGuid());
        Assert.Equal(WarningCodes.LowStock, body.GetProperty("warnings")[0].GetProperty("code").GetString());
        Assert.Equal(20m, await StockOfAsync(client, supply.Id, ct));

        var id = body.GetProperty("data").GetProperty("id").GetGuid();
        await using var db = factory.Postgres.CreateDbContext();
        var log = await db.AuditLogs.IgnoreQueryFilters().SingleAsync(l => l.EntityId == id, ct);
        Assert.Equal(AuditActions.StockTransactionStockOut, log.Action);
        Assert.Equal(household.Household.Id, log.HouseholdId);
    }

    [Fact(DisplayName = "TC-05: tồn 5 kg, hộ bật âm kho, xuất 8 kg thành công, tồn -3 kg, cảnh báo NEGATIVE_STOCK")]
    public async Task TC05_StockOut_NegativeAllowed_WarnsNegativeStock()
    {
        var ct = TestContext.Current.CancellationToken;
        var (household, supply, season, client) = await ArrangeAsync(ct, stock: 5m, unitCost: 1000m);
        await SupplyTestData.SetAllowNegativeStockAsync(factory.Postgres, household, allow: true, ct);

        var response = await client.PostAsJsonAsync(TransactionsPath, StockOut(supply.Id, season.Id, 8m), ct);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await response.ReadJsonAsync(ct);
        Assert.Equal(WarningCodes.NegativeStock, body.GetProperty("warnings")[0].GetProperty("code").GetString());
        Assert.Equal(-3m, await StockOfAsync(client, supply.Id, ct));
    }

    [Fact(DisplayName = "TC-06: tồn 10 kg, hai yêu cầu cùng lúc xuất 8 kg: đúng một thành công, một INSUFFICIENT_STOCK, tồn 2 kg")]
    public async Task TC06_StockOut_Concurrently_OnlyOneSucceeds()
    {
        var ct = TestContext.Current.CancellationToken;
        var (_, supply, season, client) = await ArrangeAsync(ct, stock: 10m, unitCost: 1000m);

        var responses = await Task.WhenAll(
            client.PostAsJsonAsync(TransactionsPath, StockOut(supply.Id, season.Id, 8m), ct),
            client.PostAsJsonAsync(TransactionsPath, StockOut(supply.Id, season.Id, 8m), ct));

        Assert.Single(responses, r => r.StatusCode == HttpStatusCode.Created);
        var rejected = Assert.Single(responses, r => r.StatusCode != HttpStatusCode.Created);
        await rejected.AssertProblemAsync(HttpStatusCode.UnprocessableEntity, ErrorCodes.InsufficientStock, ct);
        Assert.Equal(2m, await StockOfAsync(client, supply.Id, ct));
    }

    [Fact(DisplayName = "TC-07: bước 1 đến 3 của ví dụ giá vốn: đơn giá xuất 22.000đ, chi phí 330.000đ, giá bình quân không đổi")]
    public async Task TC07_StockOut_UsesAverageUnitCost()
    {
        var ct = TestContext.Current.CancellationToken;
        var (_, supply, season, client) = await ArrangeAsync(ct, stock: 10m, unitCost: 20000m);
        await client.PostAsJsonAsync(TransactionsPath, StockIn(supply.Id, 10m, 24000m), ct);

        var response = await client.PostAsJsonAsync(TransactionsPath, StockOut(supply.Id, season.Id, 15m), ct);

        var data = (await response.ReadJsonAsync(ct)).GetProperty("data");
        Assert.Equal(22000m, data.GetProperty("unitCost").GetDecimal());
        Assert.Equal(330000m, data.GetProperty("totalCost").GetDecimal());
        var detail = (await (await client.GetAsync($"{SuppliesPath}/{supply.Id}", ct)).ReadJsonAsync(ct)).GetProperty("data");
        Assert.Equal(22000m, detail.GetProperty("averageUnitCost").GetDecimal());
        Assert.Equal(5m, detail.GetProperty("stockQuantity").GetDecimal());
    }

    [Theory(DisplayName = "BR-SUP-06: xuất cho mùa vụ không InProgress trả SEASON_INVALID_STATE")]
    [InlineData(SeasonStatus.Planned)]
    [InlineData(SeasonStatus.Closed)]
    [InlineData(SeasonStatus.Cancelled)]
    public async Task StockOut_SeasonNotInProgress_ReturnsSeasonInvalidState(SeasonStatus status)
    {
        var ct = TestContext.Current.CancellationToken;
        var (household, supply, _, client) = await ArrangeAsync(ct, stock: 10m, unitCost: 1000m);
        var plot = await TestData.CreatePlotAsync(factory.Postgres, household, "TD002", ct);
        var season = await TestData.CreateSeasonAsync(factory.Postgres, household, plot, status, ct);

        var response = await client.PostAsJsonAsync(TransactionsPath, StockOut(supply.Id, season.Id, 1m), ct);

        await response.AssertProblemAsync(HttpStatusCode.UnprocessableEntity, ErrorCodes.SeasonInvalidState, ct);
    }

    [Fact(DisplayName = "BR-USR-13: TV+Kho xuất cho mùa vụ ngoài phân công thành công")]
    public async Task StockOut_ManageStockOutOfScope_Succeeds()
    {
        var ct = TestContext.Current.CancellationToken;
        var (household, supply, season, _) = await ArrangeAsync(ct, stock: 10m, unitCost: 1000m);

        var response = await factory.CreateClientFor(household.MemberWithAllPermissions)
            .PostAsJsonAsync(TransactionsPath, StockOut(supply.Id, season.Id, 1m), ct);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    [Fact(DisplayName = "Ma trận 2.5: Thành viên không có Kho, Nhân công xuất kho trả FORBIDDEN")]
    public async Task StockOut_WithoutManageStock_ReturnsForbidden()
    {
        var ct = TestContext.Current.CancellationToken;
        var (household, supply, season, _) = await ArrangeAsync(ct, stock: 10m, unitCost: 1000m);

        foreach (var user in new[] { household.Member, household.Worker })
        {
            await (await factory.CreateClientFor(user).PostAsJsonAsync(TransactionsPath, StockOut(supply.Id, season.Id, 1m), ct))
                .AssertProblemAsync(HttpStatusCode.Forbidden, ErrorCodes.Forbidden, ct);
        }
    }

    [Fact(DisplayName = "BR-GEN-12: xuất quá tồn khi hộ không bật âm kho trả INSUFFICIENT_STOCK kèm details; không tạo giao dịch")]
    public async Task StockOut_Insufficient_ReturnsErrorWithDetails()
    {
        var ct = TestContext.Current.CancellationToken;
        var (_, supply, season, client) = await ArrangeAsync(ct, stock: 5m, unitCost: 1000m);

        var response = await client.PostAsJsonAsync(TransactionsPath, StockOut(supply.Id, season.Id, 8m), ct);

        var problem = await response.AssertProblemAsync(HttpStatusCode.UnprocessableEntity, ErrorCodes.InsufficientStock, ct);
        Assert.Equal(5m, problem.GetProperty("details").GetProperty("available").GetDecimal());
        Assert.Equal(8m, problem.GetProperty("details").GetProperty("requested").GetDecimal());
        Assert.Equal(5m, await StockOfAsync(client, supply.Id, ct));
    }

    [Fact(DisplayName = "BR-SUP-06: ngày xuất được trước ngày bắt đầu của mùa vụ")]
    public async Task StockOut_BeforeSeasonStart_Succeeds()
    {
        var ct = TestContext.Current.CancellationToken;
        var (_, supply, season, client) = await ArrangeAsync(ct, stock: 10m, unitCost: 1000m);

        var response = await client.PostAsJsonAsync(
            TransactionsPath,
            StockOut(supply.Id, season.Id, 1m, date: season.StartDate.AddDays(-30)),
            ct);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    [Fact(DisplayName = "BR-GEN-13: xuất vật tư Inactive trả CATALOG_INACTIVE")]
    public async Task StockOut_InactiveSupply_ReturnsCatalogInactive()
    {
        var ct = TestContext.Current.CancellationToken;
        var household = await factory.CreateHouseholdAsync(ct);
        var supply = await SupplyTestData.CreateSupplyAsync(factory.Postgres, household, "VT001", ct, status: RecordStatus.Inactive);
        await SupplyTestData.CreateTransactionAsync(factory.Postgres, household, supply, StockTransactionType.In, 10m, ct);
        var plot = await TestData.CreatePlotAsync(factory.Postgres, household, "TD001", ct);
        var season = await TestData.CreateSeasonAsync(factory.Postgres, household, plot, SeasonStatus.InProgress, ct);

        var response = await factory.CreateClientFor(household.Owner)
            .PostAsJsonAsync(TransactionsPath, StockOut(supply.Id, season.Id, 1m), ct);

        await response.AssertProblemAsync(HttpStatusCode.UnprocessableEntity, ErrorCodes.CatalogInactive, ct);
    }

    [Fact(DisplayName = "BR-GEN-10: gửi trùng requestId một giao dịch, kể cả gửi lại sau khi mùa vụ đã chốt")]
    public async Task StockOut_SameRequestId_ReturnsExisting()
    {
        var ct = TestContext.Current.CancellationToken;
        var (household, supply, season, client) = await ArrangeAsync(ct, stock: 10m, unitCost: 1000m);
        var request = StockOut(supply.Id, season.Id, 8m);

        var responses = await Task.WhenAll(Enumerable.Range(0, 2).Select(_ => client.PostAsJsonAsync(TransactionsPath, request, ct)));
        Assert.All(responses, r => Assert.Equal(HttpStatusCode.Created, r.StatusCode));

        await using (var db = factory.Postgres.CreateDbContext(TestCurrentUser.For(household.Owner)))
        {
            (await db.Seasons.SingleAsync(s => s.Id == season.Id, ct)).Status = SeasonStatus.Closed;
            await db.SaveChangesAsync(ct);
        }

        var resend = await client.PostAsJsonAsync(TransactionsPath, request, ct);

        Assert.Equal(HttpStatusCode.Created, resend.StatusCode);
        Assert.Equal(2m, await StockOfAsync(client, supply.Id, ct));
    }

    [Fact(DisplayName = "BR-SUP-06: xuất kho thiếu mùa vụ, số lượng không dương trả VALIDATION_ERROR")]
    public async Task StockOut_MissingSeason_ReturnsValidationError()
    {
        var ct = TestContext.Current.CancellationToken;
        var (_, supply, _, client) = await ArrangeAsync(ct, stock: 10m, unitCost: 1000m);

        var response = await client.PostAsJsonAsync(
            TransactionsPath,
            new { type = "Out", supplyId = supply.Id, quantity = 0m, transactionDate = "2026-10-01", requestId = Guid.CreateVersion7() },
            ct);

        var problem = await response.AssertProblemAsync(HttpStatusCode.BadRequest, ErrorCodes.ValidationError, ct);
        Assert.True(problem.GetProperty("errors").TryGetProperty("seasonId", out _));
        Assert.True(problem.GetProperty("errors").TryGetProperty("quantity", out _));
    }

    private async Task<(TestHousehold Household, Supply Supply, Season Season, HttpClient Client)> ArrangeAsync(
        CancellationToken ct,
        decimal stock,
        decimal unitCost,
        decimal minStockLevel = 0m)
    {
        var household = await factory.CreateHouseholdAsync(ct);
        var supply = await SupplyTestData.CreateSupplyAsync(factory.Postgres, household, "VT001", ct, minStockLevel: minStockLevel);
        var plot = await TestData.CreatePlotAsync(factory.Postgres, household, "TD001", ct);
        var season = await TestData.CreateSeasonAsync(factory.Postgres, household, plot, SeasonStatus.InProgress, ct);
        var client = factory.CreateClientFor(household.Owner);
        var response = await client.PostAsJsonAsync(TransactionsPath, StockIn(supply.Id, stock, unitCost), ct);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (household, supply, season, client);
    }

    private static async Task<decimal> StockOfAsync(HttpClient client, Guid supplyId, CancellationToken ct)
    {
        var body = await (await client.GetAsync($"{SuppliesPath}/{supplyId}", ct)).ReadJsonAsync(ct);
        return body.GetProperty("data").GetProperty("stockQuantity").GetDecimal();
    }

    private static object StockIn(Guid supplyId, decimal quantity, decimal unitCost) => new
    {
        type = "In",
        supplyId,
        quantity,
        unitCost,
        transactionDate = "2026-10-01",
        requestId = Guid.CreateVersion7(),
    };

    private static object StockOut(Guid supplyId, Guid seasonId, decimal quantity, DateOnly? date = null) => new
    {
        type = "Out",
        supplyId,
        quantity,
        seasonId,
        transactionDate = date ?? new DateOnly(2026, 10, 2),
        requestId = Guid.CreateVersion7(),
    };
}
