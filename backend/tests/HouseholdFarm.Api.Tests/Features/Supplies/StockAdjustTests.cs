using System.Net;
using System.Net.Http.Json;
using HouseholdFarm.Api.Common.Auditing;
using HouseholdFarm.Api.Common.Errors;
using HouseholdFarm.Api.Common.Responses;
using HouseholdFarm.Api.Domain.Enums;
using HouseholdFarm.Api.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace HouseholdFarm.Api.Tests.Features.Supplies;

/// <summary>Điều chỉnh tồn vật tư (FR-SUP-06).</summary>
public class StockAdjustTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private const string TransactionsPath = "/api/stock-transactions";
    private const string SuppliesPath = "/api/supplies";

    [Fact(DisplayName = "BR-SUP-09: điều chỉnh tăng tồn, đơn giá là giá bình quân, giá bình quân không đổi, có nhật ký kèm lý do")]
    public async Task Adjust_Increase_KeepsAverageAndWritesAudit()
    {
        var ct = TestContext.Current.CancellationToken;
        var household = await factory.CreateHouseholdAsync(ct);
        var supply = await SupplyTestData.CreateSupplyAsync(factory.Postgres, household, "VT001", ct, averageUnitCost: 22000m);
        var client = factory.CreateClientFor(household.MemberWithAllPermissions);

        var response = await client.PostAsJsonAsync(TransactionsPath, Adjust(supply.Id, 5m, " Kiểm kê thừa "), ct);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var data = (await response.ReadJsonAsync(ct)).GetProperty("data");
        Assert.Equal(22000m, data.GetProperty("unitCost").GetDecimal());
        Assert.Equal(110000m, data.GetProperty("totalCost").GetDecimal());
        Assert.Equal("Kiểm kê thừa", data.GetProperty("reason").GetString());
        var detail = (await (await client.GetAsync($"{SuppliesPath}/{supply.Id}", ct)).ReadJsonAsync(ct)).GetProperty("data");
        Assert.Equal(5m, detail.GetProperty("stockQuantity").GetDecimal());
        Assert.Equal(22000m, detail.GetProperty("averageUnitCost").GetDecimal());

        var id = data.GetProperty("id").GetGuid();
        await using var db = factory.Postgres.CreateDbContext();
        var log = await db.AuditLogs.IgnoreQueryFilters().SingleAsync(l => l.EntityId == id, ct);
        Assert.Equal(AuditActions.StockTransactionAdjust, log.Action);
        Assert.Equal("Kiểm kê thừa", log.Reason);
    }

    [Fact(DisplayName = "BR-GEN-13: vật tư Inactive vẫn điều chỉnh được để dọn tồn còn lại")]
    public async Task Adjust_InactiveSupply_Succeeds()
    {
        var ct = TestContext.Current.CancellationToken;
        var household = await factory.CreateHouseholdAsync(ct);
        var supply = await SupplyTestData.CreateSupplyAsync(factory.Postgres, household, "VT001", ct, status: RecordStatus.Inactive);
        await SupplyTestData.CreateTransactionAsync(factory.Postgres, household, supply, StockTransactionType.In, 3m, ct);

        var response = await factory.CreateClientFor(household.Owner)
            .PostAsJsonAsync(TransactionsPath, Adjust(supply.Id, -3m), ct);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    [Fact(DisplayName = "BR-SUP-08: điều chỉnh giảm xuống dưới ngưỡng có cảnh báo LOW_STOCK")]
    public async Task Adjust_DecreaseBelowMinimum_WarnsLowStock()
    {
        var ct = TestContext.Current.CancellationToken;
        var household = await factory.CreateHouseholdAsync(ct);
        var supply = await SupplyTestData.CreateSupplyAsync(factory.Postgres, household, "VT001", ct, minStockLevel: 30m);
        await SupplyTestData.CreateTransactionAsync(factory.Postgres, household, supply, StockTransactionType.In, 40m, ct);

        var response = await factory.CreateClientFor(household.Owner)
            .PostAsJsonAsync(TransactionsPath, Adjust(supply.Id, -15m), ct);

        var body = await response.ReadJsonAsync(ct);
        Assert.Equal(WarningCodes.LowStock, body.GetProperty("warnings")[0].GetProperty("code").GetString());
    }

    [Fact(DisplayName = "BR-SUP-07: điều chỉnh giảm quá tồn trả INSUFFICIENT_STOCK")]
    public async Task Adjust_DecreaseBelowZero_ReturnsInsufficientStock()
    {
        var ct = TestContext.Current.CancellationToken;
        var household = await factory.CreateHouseholdAsync(ct);
        var supply = await SupplyTestData.CreateSupplyAsync(factory.Postgres, household, "VT001", ct);
        await SupplyTestData.CreateTransactionAsync(factory.Postgres, household, supply, StockTransactionType.In, 2m, ct);

        var response = await factory.CreateClientFor(household.Owner)
            .PostAsJsonAsync(TransactionsPath, Adjust(supply.Id, -3m), ct);

        await response.AssertProblemAsync(HttpStatusCode.UnprocessableEntity, ErrorCodes.InsufficientStock, ct);
    }

    [Fact(DisplayName = "BR-GEN-06: thiếu lý do, số lượng 0, gắn mùa vụ khi điều chỉnh trả VALIDATION_ERROR")]
    public async Task Adjust_InvalidData_ReturnsValidationError()
    {
        var ct = TestContext.Current.CancellationToken;
        var household = await factory.CreateHouseholdAsync(ct);
        var supply = await SupplyTestData.CreateSupplyAsync(factory.Postgres, household, "VT001", ct);

        var response = await factory.CreateClientFor(household.Owner).PostAsJsonAsync(
            TransactionsPath,
            new
            {
                type = "Adjustment",
                supplyId = supply.Id,
                quantity = 0m,
                reason = "   ",
                seasonId = Guid.CreateVersion7(),
                transactionDate = "2026-10-01",
                requestId = Guid.CreateVersion7(),
            },
            ct);

        var problem = await response.AssertProblemAsync(HttpStatusCode.BadRequest, ErrorCodes.ValidationError, ct);
        var errors = problem.GetProperty("errors");
        Assert.True(errors.TryGetProperty("quantity", out _));
        Assert.True(errors.TryGetProperty("reason", out _));
        Assert.True(errors.TryGetProperty("seasonId", out _));
    }

    [Fact(DisplayName = "Ma trận 2.5: Thành viên không có Kho điều chỉnh tồn trả FORBIDDEN")]
    public async Task Adjust_WithoutManageStock_ReturnsForbidden()
    {
        var ct = TestContext.Current.CancellationToken;
        var household = await factory.CreateHouseholdAsync(ct);
        var supply = await SupplyTestData.CreateSupplyAsync(factory.Postgres, household, "VT001", ct);

        var response = await factory.CreateClientFor(household.Member).PostAsJsonAsync(TransactionsPath, Adjust(supply.Id, 1m), ct);

        await response.AssertProblemAsync(HttpStatusCode.Forbidden, ErrorCodes.Forbidden, ct);
    }

    private static object Adjust(Guid supplyId, decimal quantity, string reason = "Kiểm kê") => new
    {
        type = "Adjustment",
        supplyId,
        quantity,
        reason,
        transactionDate = "2026-10-01",
        requestId = Guid.CreateVersion7(),
    };
}
