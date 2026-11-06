using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using HouseholdFarm.Api.Common.Auditing;
using HouseholdFarm.Api.Common.Errors;
using HouseholdFarm.Api.Domain.Enums;
using HouseholdFarm.Api.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace HouseholdFarm.Api.Tests.Features.Supplies;

/// <summary>Nhập kho (FR-SUP-03) và giá vốn bình quân khi nhập (FR-SUP-05).</summary>
public class StockInTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private const string TransactionsPath = "/api/stock-transactions";
    private const string SuppliesPath = "/api/supplies";

    [Fact(DisplayName = "FR-SUP-03: nhập kho thành công, tồn và giá trị kho tăng, không gắn mùa vụ, có nhật ký StockIn")]
    public async Task StockIn_Succeeds_IncreasesStockAndWritesAudit()
    {
        var ct = TestContext.Current.CancellationToken;
        var household = await factory.CreateHouseholdAsync(ct);
        var supply = await SupplyTestData.CreateSupplyAsync(factory.Postgres, household, "VT001", ct);
        var client = factory.CreateClientFor(household.MemberWithAllPermissions);

        var response = await client.PostAsJsonAsync(TransactionsPath, StockIn(supply.Id, 10m, 20000m, note: " Đợt 1 "), ct);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var data = (await response.ReadJsonAsync(ct)).GetProperty("data");
        var id = data.GetProperty("id").GetGuid();
        Assert.Equal("In", data.GetProperty("type").GetString());
        Assert.Equal(10m, data.GetProperty("quantity").GetDecimal());
        Assert.Equal(20000m, data.GetProperty("unitCost").GetDecimal());
        Assert.Equal(200000m, data.GetProperty("totalCost").GetDecimal());
        Assert.Equal(JsonValueKind.Null, data.GetProperty("seasonId").ValueKind);
        Assert.Equal("Đợt 1", data.GetProperty("note").GetString());
        Assert.True(data.GetProperty("version").GetUInt32() > 0);
        Assert.Equal($"{TransactionsPath}/{id}", response.Headers.Location!.AbsolutePath);

        var detail = await SupplyOfAsync(client, supply.Id, ct);
        Assert.Equal(10m, detail.GetProperty("stockQuantity").GetDecimal());
        Assert.Equal(20000m, detail.GetProperty("averageUnitCost").GetDecimal());

        var read = await client.GetAsync($"{TransactionsPath}/{id}", ct);
        Assert.Equal(HttpStatusCode.OK, read.StatusCode);

        await using var db = factory.Postgres.CreateDbContext();
        var log = await db.AuditLogs.IgnoreQueryFilters().SingleAsync(l => l.EntityId == id, ct);
        Assert.Equal(AuditActions.StockTransactionStockIn, log.Action);
        Assert.Equal(household.Household.Id, log.HouseholdId);
    }

    [Fact(DisplayName = "BR-SUP-03, BR-SUP-04: bước 1, 2 của ví dụ giá vốn cho giá bình quân 22.000, mỗi giao dịch lưu giá trước đó")]
    public async Task StockIn_TwoReceipts_AverageIs22000()
    {
        var ct = TestContext.Current.CancellationToken;
        var household = await factory.CreateHouseholdAsync(ct);
        var supply = await SupplyTestData.CreateSupplyAsync(factory.Postgres, household, "VT001", ct);
        var client = factory.CreateClientFor(household.Owner);

        await client.PostAsJsonAsync(TransactionsPath, StockIn(supply.Id, 10m, 20000m), ct);
        await client.PostAsJsonAsync(TransactionsPath, StockIn(supply.Id, 10m, 24000m), ct);

        Assert.Equal(22000m, (await SupplyOfAsync(client, supply.Id, ct)).GetProperty("averageUnitCost").GetDecimal());
        await using var db = factory.Postgres.CreateDbContext();
        var before = await db.StockTransactions.IgnoreQueryFilters()
            .Where(t => t.SupplyId == supply.Id)
            .OrderBy(t => t.CreatedAt)
            .Select(t => t.AverageUnitCostBefore)
            .ToListAsync(ct);
        Assert.Equal([0m, 20000m], before);
    }

    [Fact(DisplayName = "BR-SUP-04: bước 4 của ví dụ giá vốn cho giá bình quân 26.000; đơn giá đã lưu không bị tính lại (BR-SUP-02)")]
    public async Task StockIn_AfterStockOut_AverageIs26000()
    {
        var ct = TestContext.Current.CancellationToken;
        var household = await factory.CreateHouseholdAsync(ct);
        var supply = await SupplyTestData.CreateSupplyAsync(factory.Postgres, household, "VT001", ct);
        var plot = await TestData.CreatePlotAsync(factory.Postgres, household, "TD001", ct);
        var season = await TestData.CreateSeasonAsync(factory.Postgres, household, plot, SeasonStatus.InProgress, ct);
        var client = factory.CreateClientFor(household.Owner);

        await client.PostAsJsonAsync(TransactionsPath, StockIn(supply.Id, 10m, 20000m), ct);
        await client.PostAsJsonAsync(TransactionsPath, StockIn(supply.Id, 10m, 24000m), ct);
        var stockOut = await SupplyTestData.CreateTransactionAsync(
            factory.Postgres, household, supply, StockTransactionType.Out, -15m, ct, seasonId: season.Id, unitCost: 22000m);

        await client.PostAsJsonAsync(TransactionsPath, StockIn(supply.Id, 5m, 30000m), ct);

        var detail = await SupplyOfAsync(client, supply.Id, ct);
        Assert.Equal(26000m, detail.GetProperty("averageUnitCost").GetDecimal());
        Assert.Equal(10m, detail.GetProperty("stockQuantity").GetDecimal());
        await using var db = factory.Postgres.CreateDbContext();
        var saved = await db.StockTransactions.IgnoreQueryFilters().SingleAsync(t => t.Id == stockOut.Id, ct);
        Assert.Equal(22000m, saved.UnitCost);
    }

    [Fact(DisplayName = "BR-SUP-04: nhập khi tồn âm thì giá bình quân bằng đơn giá nhập")]
    public async Task StockIn_WhenStockNegative_AverageEqualsUnitCost()
    {
        var ct = TestContext.Current.CancellationToken;
        var household = await factory.CreateHouseholdAsync(ct);
        var supply = await SupplyTestData.CreateSupplyAsync(factory.Postgres, household, "VT001", ct, averageUnitCost: 15000m);
        await SupplyTestData.CreateTransactionAsync(factory.Postgres, household, supply, StockTransactionType.Adjustment, -3m, ct);
        var client = factory.CreateClientFor(household.Owner);

        await client.PostAsJsonAsync(TransactionsPath, StockIn(supply.Id, 5m, 30000m), ct);

        var detail = await SupplyOfAsync(client, supply.Id, ct);
        Assert.Equal(30000m, detail.GetProperty("averageUnitCost").GetDecimal());
        Assert.Equal(2m, detail.GetProperty("stockQuantity").GetDecimal());
    }

    [Fact(DisplayName = "BR-GEN-12: hai lần nhập cùng lúc đều tính đúng tồn, giá bình quân không phụ thuộc thứ tự chen ngang")]
    public async Task StockIn_Concurrently_KeepsAverageConsistent()
    {
        var ct = TestContext.Current.CancellationToken;
        var household = await factory.CreateHouseholdAsync(ct);
        var supply = await SupplyTestData.CreateSupplyAsync(factory.Postgres, household, "VT001", ct);
        var client = factory.CreateClientFor(household.Owner);

        var responses = await Task.WhenAll(
            client.PostAsJsonAsync(TransactionsPath, StockIn(supply.Id, 10m, 20000m), ct),
            client.PostAsJsonAsync(TransactionsPath, StockIn(supply.Id, 10m, 40000m), ct));

        Assert.All(responses, r => Assert.Equal(HttpStatusCode.Created, r.StatusCode));
        var detail = await SupplyOfAsync(client, supply.Id, ct);
        Assert.Equal(20m, detail.GetProperty("stockQuantity").GetDecimal());
        Assert.Equal(30000m, detail.GetProperty("averageUnitCost").GetDecimal());
    }

    [Fact(DisplayName = "BR-GEN-10: gửi trùng requestId chỉ tạo một giao dịch, cả khi gửi lần lượt lẫn cùng lúc")]
    public async Task StockIn_SameRequestId_CreatesOneTransaction()
    {
        var ct = TestContext.Current.CancellationToken;
        var household = await factory.CreateHouseholdAsync(ct);
        var supply = await SupplyTestData.CreateSupplyAsync(factory.Postgres, household, "VT001", ct);
        var client = factory.CreateClientFor(household.Owner);
        var request = StockIn(supply.Id, 10m, 20000m);

        var responses = await Task.WhenAll(Enumerable.Range(0, 3).Select(_ => client.PostAsJsonAsync(TransactionsPath, request, ct)));
        var resend = await client.PostAsJsonAsync(TransactionsPath, request, ct);

        Assert.All(responses, r => Assert.Equal(HttpStatusCode.Created, r.StatusCode));
        Assert.Equal(HttpStatusCode.Created, resend.StatusCode);
        var ids = new List<Guid>();
        foreach (var response in responses.Append(resend))
        {
            ids.Add((await response.ReadJsonAsync(ct)).GetProperty("data").GetProperty("id").GetGuid());
        }

        Assert.Single(ids.Distinct());
        Assert.Equal(10m, (await SupplyOfAsync(client, supply.Id, ct)).GetProperty("stockQuantity").GetDecimal());
    }

    [Fact(DisplayName = "Ma trận 2.5: Thành viên không có Kho, Nhân công nhập kho, xem giao dịch trả FORBIDDEN")]
    public async Task StockIn_WithoutManageStock_ReturnsForbidden()
    {
        var ct = TestContext.Current.CancellationToken;
        var household = await factory.CreateHouseholdAsync(ct);
        var supply = await SupplyTestData.CreateSupplyAsync(factory.Postgres, household, "VT001", ct);
        var transaction = await SupplyTestData.CreateTransactionAsync(
            factory.Postgres, household, supply, StockTransactionType.In, 10m, ct);

        foreach (var user in new[] { household.Member, household.Worker })
        {
            var client = factory.CreateClientFor(user);

            await (await client.PostAsJsonAsync(TransactionsPath, StockIn(supply.Id, 10m, 20000m), ct))
                .AssertProblemAsync(HttpStatusCode.Forbidden, ErrorCodes.Forbidden, ct);
            await (await client.GetAsync($"{TransactionsPath}/{transaction.Id}", ct))
                .AssertProblemAsync(HttpStatusCode.Forbidden, ErrorCodes.Forbidden, ct);
        }
    }

    [Fact(DisplayName = "BR-GEN-13: nhập kho cho vật tư Inactive trả CATALOG_INACTIVE")]
    public async Task StockIn_InactiveSupply_ReturnsCatalogInactive()
    {
        var ct = TestContext.Current.CancellationToken;
        var household = await factory.CreateHouseholdAsync(ct);
        var supply = await SupplyTestData.CreateSupplyAsync(
            factory.Postgres, household, "VT001", ct, status: RecordStatus.Inactive);

        var response = await factory.CreateClientFor(household.Owner)
            .PostAsJsonAsync(TransactionsPath, StockIn(supply.Id, 10m, 20000m), ct);

        await response.AssertProblemAsync(HttpStatusCode.UnprocessableEntity, ErrorCodes.CatalogInactive, ct);
    }

    [Fact(DisplayName = "BR-GEN-01: nhập kho cho vật tư, xem giao dịch của hộ khác trả NOT_FOUND")]
    public async Task OtherHousehold_ReturnsNotFound()
    {
        var ct = TestContext.Current.CancellationToken;
        var household = await factory.CreateHouseholdAsync(ct);
        var other = await factory.CreateHouseholdAsync(ct);
        var otherSupply = await SupplyTestData.CreateSupplyAsync(factory.Postgres, other, "VT001", ct);
        var otherTransaction = await SupplyTestData.CreateTransactionAsync(
            factory.Postgres, other, otherSupply, StockTransactionType.In, 10m, ct);
        var client = factory.CreateClientFor(household.Owner);

        await (await client.PostAsJsonAsync(TransactionsPath, StockIn(otherSupply.Id, 10m, 20000m), ct))
            .AssertProblemAsync(HttpStatusCode.NotFound, ErrorCodes.NotFound, ct);
        await (await client.GetAsync($"{TransactionsPath}/{otherTransaction.Id}", ct))
            .AssertProblemAsync(HttpStatusCode.NotFound, ErrorCodes.NotFound, ct);
    }

    [Fact(DisplayName = "BR-SUP-05: đơn giá nhập 0 hợp lệ")]
    public async Task StockIn_ZeroUnitCost_Succeeds()
    {
        var ct = TestContext.Current.CancellationToken;
        var household = await factory.CreateHouseholdAsync(ct);
        var supply = await SupplyTestData.CreateSupplyAsync(factory.Postgres, household, "VT001", ct);

        var response = await factory.CreateClientFor(household.Owner)
            .PostAsJsonAsync(TransactionsPath, StockIn(supply.Id, 10m, 0m), ct);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    [Theory(DisplayName = "Dữ liệu nhập kho sai trả VALIDATION_ERROR ở đúng trường")]
    [InlineData(0, 20000, "quantity")]
    [InlineData(-1, 20000, "quantity")]
    [InlineData(1.2345, 20000, "quantity")]
    [InlineData(10, -1, "unitCost")]
    [InlineData(10, 1.23456, "unitCost")]
    [InlineData(100000000000, 100000000, "unitCost")]
    public async Task StockIn_InvalidNumbers_ReturnsValidationError(double quantity, double unitCost, string field)
    {
        var ct = TestContext.Current.CancellationToken;
        var household = await factory.CreateHouseholdAsync(ct);
        var supply = await SupplyTestData.CreateSupplyAsync(factory.Postgres, household, "VT001", ct);

        var response = await factory.CreateClientFor(household.Owner)
            .PostAsJsonAsync(TransactionsPath, StockIn(supply.Id, (decimal)quantity, (decimal)unitCost), ct);

        var problem = await response.AssertProblemAsync(HttpStatusCode.BadRequest, ErrorCodes.ValidationError, ct);
        Assert.True(problem.GetProperty("errors").TryGetProperty(field, out _));
    }

    [Fact(DisplayName = "BR-GEN-05: ngày nhập sau hôm nay trả VALIDATION_ERROR; thiếu đơn giá, gắn mùa vụ cũng bị từ chối")]
    public async Task StockIn_InvalidFields_ReturnsValidationError()
    {
        var ct = TestContext.Current.CancellationToken;
        var household = await factory.CreateHouseholdAsync(ct);
        var supply = await SupplyTestData.CreateSupplyAsync(factory.Postgres, household, "VT001", ct);
        var client = factory.CreateClientFor(household.Owner);

        var response = await client.PostAsJsonAsync(
            TransactionsPath,
            new
            {
                type = "In",
                supplyId = supply.Id,
                quantity = 10m,
                transactionDate = "2999-01-01",
                seasonId = Guid.CreateVersion7(),
                requestId = Guid.CreateVersion7(),
            },
            ct);

        var problem = await response.AssertProblemAsync(HttpStatusCode.BadRequest, ErrorCodes.ValidationError, ct);
        var errors = problem.GetProperty("errors");
        Assert.True(errors.TryGetProperty("transactionDate", out _));
        Assert.True(errors.TryGetProperty("unitCost", out _));
        Assert.True(errors.TryGetProperty("seasonId", out _));
    }

    private static async Task<JsonElement> SupplyOfAsync(HttpClient client, Guid supplyId, CancellationToken ct)
    {
        var body = await (await client.GetAsync($"{SuppliesPath}/{supplyId}", ct)).ReadJsonAsync(ct);
        return body.GetProperty("data");
    }

    private static object StockIn(Guid supplyId, decimal quantity, decimal unitCost, string? note = null) => new
    {
        type = "In",
        supplyId,
        quantity,
        unitCost,
        transactionDate = "2026-10-01",
        note,
        requestId = Guid.CreateVersion7(),
    };
}
