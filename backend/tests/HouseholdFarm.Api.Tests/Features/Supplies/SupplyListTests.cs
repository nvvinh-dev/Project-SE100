using System.Net;
using System.Text.Json;
using HouseholdFarm.Api.Common.Errors;
using HouseholdFarm.Api.Domain.Enums;
using HouseholdFarm.Api.Tests.Infrastructure;

namespace HouseholdFarm.Api.Tests.Features.Supplies;

/// <summary>Danh sách vật tư: tìm kiếm, lọc, phân trang (FR-SUP-01) và vật tư dưới ngưỡng (FR-SUP-09).</summary>
public class SupplyListTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private const string SuppliesPath = "/api/supplies";

    [Fact(DisplayName = "FR-SUP-01: lọc theo nhóm, trạng thái")]
    public async Task List_FiltersByGroupAndStatus()
    {
        var ct = TestContext.Current.CancellationToken;
        var household = await factory.CreateHouseholdAsync(ct);
        await SupplyTestData.CreateSupplyAsync(factory.Postgres, household, "VT001", ct, group: SupplyGroup.Seed);
        await SupplyTestData.CreateSupplyAsync(factory.Postgres, household, "VT002", ct, group: SupplyGroup.Pesticide);
        await SupplyTestData.CreateSupplyAsync(
            factory.Postgres, household, "VT003", ct, group: SupplyGroup.Seed, status: RecordStatus.Inactive);
        var client = factory.CreateClientFor(household.Owner);

        Assert.Equal(["VT001", "VT003"], await CodesAsync(client, "?group=Seed", ct));
        Assert.Equal(["VT003"], await CodesAsync(client, "?status=Inactive", ct));
        Assert.Equal(["VT001"], await CodesAsync(client, "?group=Seed&status=Active", ct));
    }

    [Fact(DisplayName = "FR-SUP-01: tìm theo mã và tên, không phân biệt hoa thường; % trong chuỗi tìm không phải ký tự đại diện")]
    public async Task List_SearchesCodeAndName()
    {
        var ct = TestContext.Current.CancellationToken;
        var household = await factory.CreateHouseholdAsync(ct);
        await SupplyTestData.CreateSupplyAsync(factory.Postgres, household, "VT001", ct, name: "Phân NPK");
        await SupplyTestData.CreateSupplyAsync(factory.Postgres, household, "VT002", ct, name: "Thuốc 50% hoạt chất");
        await SupplyTestData.CreateSupplyAsync(factory.Postgres, household, "GIONG-1", ct, name: "Lúa giống");
        var client = factory.CreateClientFor(household.Owner);

        Assert.Equal(["VT001"], await CodesAsync(client, "?search=phân%20npk", ct));
        Assert.Equal(["GIONG-1"], await CodesAsync(client, "?search=giong", ct));
        Assert.Equal(["VT002"], await CodesAsync(client, "?search=50%25", ct));
        Assert.Empty(await CodesAsync(client, "?search=%25%25%25", ct));
    }

    [Fact(DisplayName = "FR-SUP-01: phân trang trả đúng meta, sắp xếp theo tên giảm dần")]
    public async Task List_PaginatesAndSorts()
    {
        var ct = TestContext.Current.CancellationToken;
        var household = await factory.CreateHouseholdAsync(ct);
        await SupplyTestData.CreateSupplyAsync(factory.Postgres, household, "VT001", ct, name: "A");
        await SupplyTestData.CreateSupplyAsync(factory.Postgres, household, "VT002", ct, name: "C");
        await SupplyTestData.CreateSupplyAsync(factory.Postgres, household, "VT003", ct, name: "B");
        var client = factory.CreateClientFor(household.Owner);

        var body = await (await client.GetAsync($"{SuppliesPath}?sort=-name&page=2&pageSize=2", ct)).ReadJsonAsync(ct);

        Assert.Equal(["VT001"], body.GetProperty("data").EnumerateArray().Select(CodeOf));
        Assert.Equal(2, body.GetProperty("meta").GetProperty("page").GetInt32());
        Assert.Equal(2, body.GetProperty("meta").GetProperty("pageSize").GetInt32());
        Assert.Equal(3, body.GetProperty("meta").GetProperty("totalCount").GetInt32());
    }

    [Fact(DisplayName = "FR-SUP-01: sắp xếp theo trường không cho phép trả VALIDATION_ERROR")]
    public async Task List_InvalidSort_ReturnsValidationError()
    {
        var ct = TestContext.Current.CancellationToken;
        var household = await factory.CreateHouseholdAsync(ct);

        var response = await factory.CreateClientFor(household.Member).GetAsync($"{SuppliesPath}?sort=referencePrice", ct);

        await response.AssertProblemAsync(HttpStatusCode.BadRequest, ErrorCodes.ValidationError, ct);
    }

    [Fact(DisplayName = "BR-GEN-01: danh sách không có vật tư của hộ khác")]
    public async Task List_ExcludesOtherHousehold()
    {
        var ct = TestContext.Current.CancellationToken;
        var householdA = await factory.CreateHouseholdAsync(ct);
        var householdB = await factory.CreateHouseholdAsync(ct);
        await SupplyTestData.CreateSupplyAsync(factory.Postgres, householdA, "VT001", ct);
        await SupplyTestData.CreateSupplyAsync(factory.Postgres, householdB, "VT999", ct);

        Assert.Equal(["VT001"], await CodesAsync(factory.CreateClientFor(householdA.Owner), string.Empty, ct));
    }

    [Fact(DisplayName = "BR-SUP-12: vật tư tồn 20 kg ngưỡng 30 kg có trong danh sách; tồn bằng ngưỡng không có; Inactive không có")]
    public async Task List_BelowMin_OnlyActiveSuppliesUnderThreshold()
    {
        var ct = TestContext.Current.CancellationToken;
        var household = await factory.CreateHouseholdAsync(ct);
        await SupplyWithStockAsync(household, "VT001", stock: 20m, minStockLevel: 30m, ct);
        await SupplyWithStockAsync(household, "VT002", stock: 30m, minStockLevel: 30m, ct);
        await SupplyWithStockAsync(household, "VT003", stock: 40m, minStockLevel: 30m, ct);
        await SupplyWithStockAsync(household, "VT004", stock: 20m, minStockLevel: 30m, ct, RecordStatus.Inactive);
        // Chưa có giao dịch nào: tồn 0 dưới ngưỡng 5.
        await SupplyTestData.CreateSupplyAsync(factory.Postgres, household, "VT005", ct, minStockLevel: 5m);
        // Ngưỡng 0, tồn 0: không dưới ngưỡng.
        await SupplyTestData.CreateSupplyAsync(factory.Postgres, household, "VT006", ct);

        Assert.Equal(["VT001", "VT005"], await CodesAsync(factory.CreateClientFor(household.Owner), "?belowMin=true", ct));
    }

    [Fact(DisplayName = "BR-SUP-12: vật tư tồn âm đứng đầu danh sách dưới ngưỡng; mỗi dòng có tên, tồn, ngưỡng, đơn vị")]
    public async Task List_BelowMin_NegativeStockFirst()
    {
        var ct = TestContext.Current.CancellationToken;
        var household = await factory.CreateHouseholdAsync(ct);
        await SupplyWithStockAsync(household, "VT001", stock: 20m, minStockLevel: 30m, ct);
        await SupplyWithStockAsync(household, "VT002", stock: -3m, minStockLevel: 0m, ct);
        await SupplyWithStockAsync(household, "VT003", stock: 5m, minStockLevel: 30m, ct);
        await SupplyWithStockAsync(household, "VT004", stock: -1m, minStockLevel: 10m, ct);

        var body = await (await factory.CreateClientFor(household.Member).GetAsync($"{SuppliesPath}?belowMin=true", ct))
            .ReadJsonAsync(ct);

        var rows = body.GetProperty("data").EnumerateArray().ToList();
        Assert.Equal(["VT002", "VT004", "VT001", "VT003"], rows.Select(CodeOf));
        Assert.Equal("Vật tư VT002", rows[0].GetProperty("name").GetString());
        Assert.Equal(-3m, rows[0].GetProperty("stockQuantity").GetDecimal());
        Assert.Equal(0m, rows[0].GetProperty("minStockLevel").GetDecimal());
        Assert.Equal("Kilogram", rows[0].GetProperty("unit").GetString());
        Assert.False(rows[0].TryGetProperty("averageUnitCost", out _));
    }

    [Fact(DisplayName = "Ma trận 2.5: Nhân công xem vật tư dưới ngưỡng trả FORBIDDEN")]
    public async Task List_BelowMin_Worker_ReturnsForbidden()
    {
        var ct = TestContext.Current.CancellationToken;
        var household = await factory.CreateHouseholdAsync(ct);

        var response = await factory.CreateClientFor(household.Worker).GetAsync($"{SuppliesPath}?belowMin=true", ct);

        await response.AssertProblemAsync(HttpStatusCode.Forbidden, ErrorCodes.Forbidden, ct);
    }

    private async Task SupplyWithStockAsync(
        TestHousehold household,
        string code,
        decimal stock,
        decimal minStockLevel,
        CancellationToken ct,
        RecordStatus status = RecordStatus.Active)
    {
        var supply = await SupplyTestData.CreateSupplyAsync(
            factory.Postgres, household, code, ct, minStockLevel: minStockLevel, status: status);
        await SupplyTestData.CreateTransactionAsync(
            factory.Postgres,
            household,
            supply,
            stock > 0 ? StockTransactionType.In : StockTransactionType.Adjustment,
            stock,
            ct);
    }

    private static async Task<IReadOnlyList<string>> CodesAsync(HttpClient client, string queryString, CancellationToken ct)
    {
        var response = await client.GetAsync($"{SuppliesPath}{queryString}", ct);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.ReadJsonAsync(ct)).GetProperty("data").EnumerateArray().Select(CodeOf).ToList();
    }

    private static string CodeOf(JsonElement row) => row.GetProperty("code").GetString()!;
}
