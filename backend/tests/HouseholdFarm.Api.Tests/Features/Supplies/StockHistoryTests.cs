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

/// <summary>Lịch sử giao dịch kho và hủy giao dịch kho (FR-SUP-08).</summary>
public class StockHistoryTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private const string TransactionsPath = "/api/stock-transactions";
    private const string SuppliesPath = "/api/supplies";

    [Fact(DisplayName = "TC-08: sau bước 4 của ví dụ giá vốn, hủy giao dịch xuất #3: tồn 25 kg, giá bình quân 23.600đ")]
    public async Task TC08_VoidStockOut_ReceivesBackAtOwnCost()
    {
        var ct = TestContext.Current.CancellationToken;
        var example = await ArrangeExampleAsync(ct);

        var response = await example.Client.PostAsJsonAsync(VoidPath(example.StockOutId), new { reason = " Ghi nhầm " }, ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var data = (await response.ReadJsonAsync(ct)).GetProperty("data");
        Assert.NotEqual(JsonValueKind.Null, data.GetProperty("voidedAt").ValueKind);
        var detail = await SupplyOfAsync(example.Client, example.SupplyId, ct);
        Assert.Equal(25m, detail.GetProperty("stockQuantity").GetDecimal());
        Assert.Equal(23600m, detail.GetProperty("averageUnitCost").GetDecimal());

        // Chi phí vật tư của mùa vụ là tổng giao dịch Out chưa Void nên giảm đúng 330.000đ.
        await using var db = factory.Postgres.CreateDbContext();
        var saved = await db.StockTransactions.IgnoreQueryFilters().SingleAsync(t => t.Id == example.StockOutId, ct);
        Assert.Equal(example.Household.Owner.Id, saved.VoidedById);
        Assert.Equal("Ghi nhầm", saved.VoidReason);
        var seasonCost = await db.StockTransactions.IgnoreQueryFilters()
            .Where(t => t.SeasonId == example.SeasonId && t.Type == StockTransactionType.Out && t.VoidedAt == null)
            .SumAsync(t => t.TotalCost, ct);
        Assert.Equal(0m, seasonCost);
        var log = await db.AuditLogs.IgnoreQueryFilters().SingleAsync(
            l => l.EntityId == example.StockOutId && l.Action == AuditActions.StockTransactionVoid,
            ct);
        Assert.Equal("Ghi nhầm", log.Reason);
    }

    [Fact(DisplayName = "TC-09: sau bước 5 của ví dụ giá vốn, hủy giao dịch nhập #4 trả STOCK_TX_NOT_LAST")]
    public async Task TC09_VoidStockIn_AfterOtherVoid_ReturnsNotLast()
    {
        var ct = TestContext.Current.CancellationToken;
        var example = await ArrangeExampleAsync(ct);
        await example.Client.PostAsJsonAsync(VoidPath(example.StockOutId), new { reason = "Ghi nhầm" }, ct);

        var response = await example.Client.PostAsJsonAsync(VoidPath(example.LastStockInId), new { reason = "Ghi nhầm" }, ct);

        await response.AssertProblemAsync(HttpStatusCode.UnprocessableEntity, ErrorCodes.StockTxNotLast, ct);
    }

    [Fact(DisplayName = "BR-SUP-10: hủy giao dịch nhập cuối cùng thì giá bình quân về giá trước giao dịch")]
    public async Task VoidStockIn_WhenLast_RestoresAverage()
    {
        var ct = TestContext.Current.CancellationToken;
        var example = await ArrangeExampleAsync(ct);

        var response = await example.Client.PostAsJsonAsync(VoidPath(example.LastStockInId), new { reason = "Ghi nhầm" }, ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var detail = await SupplyOfAsync(example.Client, example.SupplyId, ct);
        Assert.Equal(5m, detail.GetProperty("stockQuantity").GetDecimal());
        Assert.Equal(22000m, detail.GetProperty("averageUnitCost").GetDecimal());
    }

    [Fact(DisplayName = "BR-SUP-10: hủy giao dịch nhập khi sau đó đã có giao dịch khác trả STOCK_TX_NOT_LAST")]
    public async Task VoidStockIn_WithLaterTransaction_ReturnsNotLast()
    {
        var ct = TestContext.Current.CancellationToken;
        var example = await ArrangeExampleAsync(ct);
        var firstIn = await example.FirstStockInIdAsync(factory, ct);

        var response = await example.Client.PostAsJsonAsync(VoidPath(firstIn), new { reason = "Ghi nhầm" }, ct);

        var problem = await response.AssertProblemAsync(HttpStatusCode.UnprocessableEntity, ErrorCodes.StockTxNotLast, ct);
        Assert.Contains("điều chỉnh tồn", problem.GetProperty("detail").GetString());
    }

    [Fact(DisplayName = "BR-SUP-07: hủy giao dịch làm tăng tồn khi tồn không đủ trả INSUFFICIENT_STOCK")]
    public async Task VoidIncrease_WhenStockInsufficient_ReturnsInsufficientStock()
    {
        var ct = TestContext.Current.CancellationToken;
        var household = await factory.CreateHouseholdAsync(ct);
        var supply = await SupplyTestData.CreateSupplyAsync(factory.Postgres, household, "VT001", ct);
        var client = factory.CreateClientFor(household.Owner);
        // Giao dịch âm có trước nên giao dịch nhập vẫn là thao tác cuối, chỉ còn vướng tồn.
        await SupplyTestData.SetAllowNegativeStockAsync(factory.Postgres, household, allow: true, ct);
        await PostAsync(client, new { type = "Adjustment", supplyId = supply.Id, quantity = -5m, reason = "Kiểm kê", transactionDate = "2026-10-01", requestId = Guid.CreateVersion7() }, ct);
        await SupplyTestData.SetAllowNegativeStockAsync(factory.Postgres, household, allow: false, ct);
        var stockIn = await PostAsync(client, new { type = "In", supplyId = supply.Id, quantity = 3m, unitCost = 1000m, transactionDate = "2026-10-01", requestId = Guid.CreateVersion7() }, ct);

        var response = await client.PostAsJsonAsync(VoidPath(stockIn), new { reason = "Ghi nhầm" }, ct);

        await response.AssertProblemAsync(HttpStatusCode.UnprocessableEntity, ErrorCodes.InsufficientStock, ct);
    }

    [Fact(DisplayName = "BR-SUP-11: hủy điều chỉnh giảm coi như nhập lại với đơn giá của giao dịch; vật tư Inactive vẫn hủy được")]
    public async Task VoidNegativeAdjustment_InactiveSupply_ReceivesBack()
    {
        var ct = TestContext.Current.CancellationToken;
        var household = await factory.CreateHouseholdAsync(ct);
        var supply = await SupplyTestData.CreateSupplyAsync(factory.Postgres, household, "VT001", ct, averageUnitCost: 1000m);
        await SupplyTestData.CreateTransactionAsync(factory.Postgres, household, supply, StockTransactionType.In, 10m, ct, unitCost: 1000m);
        var client = factory.CreateClientFor(household.Owner);
        var adjustment = await PostAsync(client, new { type = "Adjustment", supplyId = supply.Id, quantity = -4m, reason = "Hao hụt", transactionDate = "2026-10-01", requestId = Guid.CreateVersion7() }, ct);
        await using (var db = factory.Postgres.CreateDbContext(TestCurrentUser.For(household.Owner)))
        {
            (await db.Supplies.SingleAsync(s => s.Id == supply.Id, ct)).Status = RecordStatus.Inactive;
            await db.SaveChangesAsync(ct);
        }

        var response = await client.PostAsJsonAsync(VoidPath(adjustment), new { reason = "Đếm nhầm" }, ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var detail = await SupplyOfAsync(client, supply.Id, ct);
        Assert.Equal(10m, detail.GetProperty("stockQuantity").GetDecimal());
        Assert.Equal(1000m, detail.GetProperty("averageUnitCost").GetDecimal());
    }

    [Fact(DisplayName = "BR-SUP-11: hủy giao dịch xuất của mùa vụ đã chốt trả SEASON_INVALID_STATE")]
    public async Task VoidStockOut_SeasonClosed_ReturnsSeasonInvalidState()
    {
        var ct = TestContext.Current.CancellationToken;
        var example = await ArrangeExampleAsync(ct);
        await using (var db = factory.Postgres.CreateDbContext(TestCurrentUser.For(example.Household.Owner)))
        {
            (await db.Seasons.SingleAsync(s => s.Id == example.SeasonId, ct)).Status = SeasonStatus.Closed;
            await db.SaveChangesAsync(ct);
        }

        var response = await example.Client.PostAsJsonAsync(VoidPath(example.StockOutId), new { reason = "Ghi nhầm" }, ct);

        await response.AssertProblemAsync(HttpStatusCode.UnprocessableEntity, ErrorCodes.SeasonInvalidState, ct);
    }

    [Fact(DisplayName = "BR-GEN-09: hủy lần hai trả ALREADY_VOIDED; hai yêu cầu hủy cùng lúc chỉ một thành công")]
    public async Task Void_Twice_ReturnsAlreadyVoided()
    {
        var ct = TestContext.Current.CancellationToken;
        var example = await ArrangeExampleAsync(ct);

        var responses = await Task.WhenAll(
            example.Client.PostAsJsonAsync(VoidPath(example.StockOutId), new { reason = "Ghi nhầm" }, ct),
            example.Client.PostAsJsonAsync(VoidPath(example.StockOutId), new { reason = "Ghi nhầm" }, ct));

        Assert.Single(responses, r => r.StatusCode == HttpStatusCode.OK);
        await Assert.Single(responses, r => r.StatusCode != HttpStatusCode.OK)
            .AssertProblemAsync(HttpStatusCode.UnprocessableEntity, ErrorCodes.AlreadyVoided, ct);
        Assert.Equal(25m, (await SupplyOfAsync(example.Client, example.SupplyId, ct)).GetProperty("stockQuantity").GetDecimal());
    }

    [Fact(DisplayName = "BR-TSK-11: hủy giao dịch xuất sinh từ công việc trả STOCK_TX_MANAGED_BY_TASK")]
    public async Task Void_FromFarmTask_ReturnsManagedByTask()
    {
        var ct = TestContext.Current.CancellationToken;
        var example = await ArrangeExampleAsync(ct);
        await LinkToFarmTaskAsync(example, ct);

        var response = await example.Client.PostAsJsonAsync(VoidPath(example.StockOutId), new { reason = "Ghi nhầm" }, ct);

        await response.AssertProblemAsync(HttpStatusCode.UnprocessableEntity, ErrorCodes.StockTxManagedByTask, ct);

        var rows = (await (await example.Client.GetAsync($"{TransactionsPath}?type=Out", ct)).ReadJsonAsync(ct)).GetProperty("data");
        Assert.True(rows[0].GetProperty("isFromFarmTask").GetBoolean());
    }

    [Fact(DisplayName = "BR-GEN-06: hủy thiếu lý do trả VALIDATION_ERROR; ma trận 2.5: không có Kho trả FORBIDDEN; hộ khác trả NOT_FOUND")]
    public async Task Void_Rejections()
    {
        var ct = TestContext.Current.CancellationToken;
        var example = await ArrangeExampleAsync(ct);
        var other = await factory.CreateHouseholdAsync(ct);

        await (await example.Client.PostAsJsonAsync(VoidPath(example.StockOutId), new { reason = "  " }, ct))
            .AssertProblemAsync(HttpStatusCode.BadRequest, ErrorCodes.ValidationError, ct);
        await (await factory.CreateClientFor(example.Household.Member).PostAsJsonAsync(VoidPath(example.StockOutId), new { reason = "x" }, ct))
            .AssertProblemAsync(HttpStatusCode.Forbidden, ErrorCodes.Forbidden, ct);
        await (await factory.CreateClientFor(other.Owner).PostAsJsonAsync(VoidPath(example.StockOutId), new { reason = "x" }, ct))
            .AssertProblemAsync(HttpStatusCode.NotFound, ErrorCodes.NotFound, ct);
    }

    [Fact(DisplayName = "FR-SUP-08: lọc lịch sử theo vật tư, loại, thời gian, mùa vụ; mặc định không gồm giao dịch đã hủy; mới nhất trước")]
    public async Task List_Filters()
    {
        var ct = TestContext.Current.CancellationToken;
        var household = await factory.CreateHouseholdAsync(ct);
        var first = await SupplyTestData.CreateSupplyAsync(factory.Postgres, household, "VT001", ct);
        var second = await SupplyTestData.CreateSupplyAsync(factory.Postgres, household, "VT002", ct);
        var plot = await TestData.CreatePlotAsync(factory.Postgres, household, "TD001", ct);
        var season = await TestData.CreateSeasonAsync(factory.Postgres, household, plot, SeasonStatus.InProgress, ct);
        var stockIn = await SupplyTestData.CreateTransactionAsync(factory.Postgres, household, first, StockTransactionType.In, 10m, ct, unitCost: 100m);
        var stockOut = await SupplyTestData.CreateTransactionAsync(factory.Postgres, household, first, StockTransactionType.Out, -1m, ct, seasonId: season.Id, unitCost: 100m);
        var adjustment = await SupplyTestData.CreateTransactionAsync(factory.Postgres, household, second, StockTransactionType.Adjustment, 2m, ct);
        var voided = await SupplyTestData.CreateTransactionAsync(factory.Postgres, household, second, StockTransactionType.In, 2m, ct, voided: true);
        await using (var db = factory.Postgres.CreateDbContext(TestCurrentUser.For(household.Owner)))
        {
            (await db.StockTransactions.SingleAsync(t => t.Id == stockOut.Id, ct)).TransactionDate = new DateOnly(2026, 10, 20);
            await db.SaveChangesAsync(ct);
        }

        var client = factory.CreateClientFor(household.MemberWithAllPermissions);

        Assert.Equal([stockOut.Id, stockIn.Id], await IdsAsync(client, $"?supplyId={first.Id}", ct));
        Assert.Equal([adjustment.Id], await IdsAsync(client, "?type=Adjustment", ct));
        Assert.Equal([stockOut.Id], await IdsAsync(client, $"?seasonId={season.Id}", ct));
        Assert.Equal([stockOut.Id], await IdsAsync(client, "?from=2026-10-10&to=2026-10-31", ct));
        Assert.Equal([adjustment.Id, stockIn.Id], await IdsAsync(client, "?to=2026-10-09", ct));
        Assert.Contains(voided.Id, await IdsAsync(client, "?includeVoided=true", ct));

        var row = (await (await client.GetAsync($"{TransactionsPath}?seasonId={season.Id}", ct)).ReadJsonAsync(ct)).GetProperty("data")[0];
        Assert.Equal("VT001", row.GetProperty("supplyCode").GetString());
        Assert.Equal("Kilogram", row.GetProperty("unit").GetString());
        Assert.Equal("TD001", row.GetProperty("plotCode").GetString());
        Assert.False(row.GetProperty("isFromFarmTask").GetBoolean());
    }

    [Fact(DisplayName = "FR-SUP-08: khoảng ngày ngược trả VALIDATION_ERROR; không có Kho xem lịch sử trả FORBIDDEN")]
    public async Task List_Rejections()
    {
        var ct = TestContext.Current.CancellationToken;
        var household = await factory.CreateHouseholdAsync(ct);

        await (await factory.CreateClientFor(household.Owner).GetAsync($"{TransactionsPath}?from=2026-10-10&to=2026-10-01", ct))
            .AssertProblemAsync(HttpStatusCode.BadRequest, ErrorCodes.ValidationError, ct);
        foreach (var user in new[] { household.Member, household.Worker })
        {
            await (await factory.CreateClientFor(user).GetAsync(TransactionsPath, ct))
                .AssertProblemAsync(HttpStatusCode.Forbidden, ErrorCodes.Forbidden, ct);
        }
    }

    private static string VoidPath(Guid id) => $"{TransactionsPath}/{id}/void";

    private async Task<Example> ArrangeExampleAsync(CancellationToken ct)
    {
        var household = await factory.CreateHouseholdAsync(ct);
        var supply = await SupplyTestData.CreateSupplyAsync(factory.Postgres, household, "VT001", ct);
        var plot = await TestData.CreatePlotAsync(factory.Postgres, household, "TD001", ct);
        var season = await TestData.CreateSeasonAsync(factory.Postgres, household, plot, SeasonStatus.InProgress, ct);
        var client = factory.CreateClientFor(household.Owner);

        await PostAsync(client, In(supply.Id, 10m, 20000m), ct);
        await PostAsync(client, In(supply.Id, 10m, 24000m), ct);
        var stockOut = await PostAsync(
            client,
            new { type = "Out", supplyId = supply.Id, quantity = 15m, seasonId = season.Id, transactionDate = "2026-10-02", requestId = Guid.CreateVersion7() },
            ct);
        var lastIn = await PostAsync(client, In(supply.Id, 5m, 30000m), ct);

        return new Example(household, client, supply.Id, season.Id, stockOut, lastIn);
    }

    private async Task LinkToFarmTaskAsync(Example example, CancellationToken ct)
    {
        await using var db = factory.Postgres.CreateDbContext(TestCurrentUser.For(example.Household.Owner));
        var task = new FarmTask
        {
            SeasonId = example.SeasonId,
            Type = FarmTaskType.Fertilizing,
            ScheduledDate = new DateOnly(2026, 10, 5),
            Status = FarmTaskStatus.Done,
        };
        db.FarmTasks.Add(task);
        db.FarmTaskSupplyUsages.Add(new FarmTaskSupplyUsage
        {
            FarmTaskId = task.Id,
            SupplyId = example.SupplyId,
            ActualQuantity = 15m,
            StockTransactionId = example.StockOutId,
        });
        await db.SaveChangesAsync(ct);
    }

    private static async Task<Guid> PostAsync(HttpClient client, object request, CancellationToken ct)
    {
        var response = await client.PostAsJsonAsync(TransactionsPath, request, ct);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.ReadJsonAsync(ct)).GetProperty("data").GetProperty("id").GetGuid();
    }

    private static async Task<JsonElement> SupplyOfAsync(HttpClient client, Guid supplyId, CancellationToken ct)
    {
        var body = await (await client.GetAsync($"{SuppliesPath}/{supplyId}", ct)).ReadJsonAsync(ct);
        return body.GetProperty("data");
    }

    private static async Task<IReadOnlyList<Guid>> IdsAsync(HttpClient client, string queryString, CancellationToken ct)
    {
        var response = await client.GetAsync($"{TransactionsPath}{queryString}", ct);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.ReadJsonAsync(ct)).GetProperty("data").EnumerateArray().Select(r => r.GetProperty("id").GetGuid()).ToList();
    }

    private static object In(Guid supplyId, decimal quantity, decimal unitCost) => new
    {
        type = "In",
        supplyId,
        quantity,
        unitCost,
        transactionDate = "2026-10-01",
        requestId = Guid.CreateVersion7(),
    };

    private sealed record Example(
        TestHousehold Household,
        HttpClient Client,
        Guid SupplyId,
        Guid SeasonId,
        Guid StockOutId,
        Guid LastStockInId)
    {
        public async Task<Guid> FirstStockInIdAsync(ApiFactory factory, CancellationToken ct)
        {
            await using var db = factory.Postgres.CreateDbContext();
            return await db.StockTransactions.IgnoreQueryFilters()
                .Where(t => t.SupplyId == SupplyId)
                .OrderBy(t => t.CreatedAt)
                .Select(t => t.Id)
                .FirstAsync(ct);
        }
    }
}
