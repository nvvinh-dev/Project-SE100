using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using HouseholdFarm.Api.Common.Errors;
using HouseholdFarm.Api.Domain.Enums;
using HouseholdFarm.Api.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace HouseholdFarm.Api.Tests.Features.Supplies;

/// <summary>Tạo, xem, sửa vật tư (FR-SUP-01) và tồn tính từ giao dịch kho (FR-SUP-02).</summary>
public class SupplyTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private const string SuppliesPath = "/api/supplies";

    [Fact(DisplayName = "BR-CAT-01: tạo vật tư để trống mã thì tự sinh VT001, VT002; vật tư mới Active, tồn 0, giá vốn 0")]
    public async Task Create_WithoutCode_GeneratesSequentialCodes()
    {
        var ct = TestContext.Current.CancellationToken;
        var household = await factory.CreateHouseholdAsync(ct);
        var client = factory.CreateClientFor(household.Owner);

        var first = await client.PostAsJsonAsync(SuppliesPath, NewSupply(code: null), ct);
        var second = await client.PostAsJsonAsync(SuppliesPath, NewSupply(code: "  "), ct);

        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        var data = (await first.ReadJsonAsync(ct)).GetProperty("data");
        Assert.Equal("VT001", data.GetProperty("code").GetString());
        Assert.Equal("Phân NPK", data.GetProperty("name").GetString());
        Assert.Equal("Fertilizer", data.GetProperty("group").GetString());
        Assert.Equal("Kilogram", data.GetProperty("unit").GetString());
        Assert.Equal(30m, data.GetProperty("minStockLevel").GetDecimal());
        Assert.Equal(20000m, data.GetProperty("referencePrice").GetDecimal());
        Assert.Equal(0m, data.GetProperty("averageUnitCost").GetDecimal());
        Assert.Equal(0m, data.GetProperty("stockQuantity").GetDecimal());
        Assert.Equal("Active", data.GetProperty("status").GetString());
        Assert.False(data.GetProperty("unitLocked").GetBoolean());
        Assert.True(data.GetProperty("version").GetUInt32() > 0);
        Assert.Equal($"{SuppliesPath}/{data.GetProperty("id").GetGuid()}", first.Headers.Location!.AbsolutePath);

        Assert.Equal("VT002", (await second.ReadJsonAsync(ct)).GetProperty("data").GetProperty("code").GetString());
    }

    [Fact(DisplayName = "BR-CAT-01: mã tự sinh lấy số lớn nhất trong các mã đúng mẫu + 1, bỏ qua mã người dùng tự đặt")]
    public async Task Create_WithoutCode_ContinuesFromLargestMatchingCode()
    {
        var ct = TestContext.Current.CancellationToken;
        var household = await factory.CreateHouseholdAsync(ct);
        await SupplyTestData.CreateSupplyAsync(factory.Postgres, household, "VT007", ct);
        await SupplyTestData.CreateSupplyAsync(factory.Postgres, household, "VT-NPK", ct);

        var response = await factory.CreateClientFor(household.Owner).PostAsJsonAsync(SuppliesPath, NewSupply(code: null), ct);

        Assert.Equal("VT008", (await response.ReadJsonAsync(ct)).GetProperty("data").GetProperty("code").GetString());
    }

    [Fact(DisplayName = "BR-CAT-01: nhiều yêu cầu tạo vật tư cùng lúc với mã tự sinh đều thành công, mỗi vật tư một mã")]
    public async Task Create_ConcurrentlyWithoutCode_RetriesAndGivesDistinctCodes()
    {
        var ct = TestContext.Current.CancellationToken;
        var household = await factory.CreateHouseholdAsync(ct);
        var client = factory.CreateClientFor(household.Owner);

        var responses = await Task.WhenAll(
            Enumerable.Range(0, 5).Select(_ => client.PostAsJsonAsync(SuppliesPath, NewSupply(code: null), ct)));

        Assert.All(responses, r => Assert.Equal(HttpStatusCode.Created, r.StatusCode));
        await using var db = factory.Postgres.CreateDbContext();
        var codes = await db.Supplies.IgnoreQueryFilters()
            .Where(s => s.HouseholdId == household.Household.Id)
            .Select(s => s.Code)
            .OrderBy(c => c)
            .ToListAsync(ct);
        Assert.Equal(["VT001", "VT002", "VT003", "VT004", "VT005"], codes);
    }

    [Fact(DisplayName = "BR-CAT-01: tạo, sửa vật tư với mã đã có trong hộ trả CODE_ALREADY_USED; hộ khác dùng cùng mã được")]
    public async Task CreateOrUpdate_WithUsedCode_ReturnsCodeAlreadyUsed()
    {
        var ct = TestContext.Current.CancellationToken;
        var household = await factory.CreateHouseholdAsync(ct);
        var otherHousehold = await factory.CreateHouseholdAsync(ct);
        await SupplyTestData.CreateSupplyAsync(factory.Postgres, household, "VT001", ct);
        var second = await SupplyTestData.CreateSupplyAsync(factory.Postgres, household, "VT002", ct);
        var client = factory.CreateClientFor(household.Owner);

        await (await client.PostAsJsonAsync(SuppliesPath, NewSupply("VT001"), ct))
            .AssertProblemAsync(HttpStatusCode.Conflict, ErrorCodes.CodeAlreadyUsed, ct);
        await (await client.PutAsJsonAsync($"{SuppliesPath}/{second.Id}", UpdateOf("VT001", second.Version), ct))
            .AssertProblemAsync(HttpStatusCode.Conflict, ErrorCodes.CodeAlreadyUsed, ct);

        var sameCodeOtherHousehold = await factory.CreateClientFor(otherHousehold.Owner)
            .PostAsJsonAsync(SuppliesPath, NewSupply("VT001"), ct);
        Assert.Equal(HttpStatusCode.Created, sameCodeOtherHousehold.StatusCode);
    }

    [Fact(DisplayName = "Ma trận 2.5: Thành viên hộ (kể cả có quyền Kho), Nhân công tạo, sửa vật tư trả FORBIDDEN")]
    public async Task CreateOrUpdate_NotOwner_ReturnsForbidden()
    {
        var ct = TestContext.Current.CancellationToken;
        var household = await factory.CreateHouseholdAsync(ct);
        var supply = await SupplyTestData.CreateSupplyAsync(factory.Postgres, household, "VT001", ct);

        foreach (var user in new[] { household.MemberWithAllPermissions, household.Member, household.Worker })
        {
            var client = factory.CreateClientFor(user);

            await (await client.PostAsJsonAsync(SuppliesPath, NewSupply(code: null), ct))
                .AssertProblemAsync(HttpStatusCode.Forbidden, ErrorCodes.Forbidden, ct);
            await (await client.PutAsJsonAsync($"{SuppliesPath}/{supply.Id}", UpdateOf("VT001", supply.Version), ct))
                .AssertProblemAsync(HttpStatusCode.Forbidden, ErrorCodes.Forbidden, ct);
        }
    }

    [Fact(DisplayName = "Ma trận 2.5: Nhân công xem danh sách, chi tiết vật tư trả FORBIDDEN")]
    public async Task Read_Worker_ReturnsForbidden()
    {
        var ct = TestContext.Current.CancellationToken;
        var household = await factory.CreateHouseholdAsync(ct);
        var supply = await SupplyTestData.CreateSupplyAsync(factory.Postgres, household, "VT001", ct);
        var client = factory.CreateClientFor(household.Worker);

        await (await client.GetAsync(SuppliesPath, ct))
            .AssertProblemAsync(HttpStatusCode.Forbidden, ErrorCodes.Forbidden, ct);
        await (await client.GetAsync($"{SuppliesPath}/{supply.Id}", ct))
            .AssertProblemAsync(HttpStatusCode.Forbidden, ErrorCodes.Forbidden, ct);
    }

    [Fact(DisplayName = "Mục 3.3: Thành viên hộ không có quyền Kho xem vật tư không có trường giá; TV+Kho, Chủ hộ thấy giá")]
    public async Task Read_HidesPriceFieldsWithoutManageStock()
    {
        var ct = TestContext.Current.CancellationToken;
        var household = await factory.CreateHouseholdAsync(ct);
        var supply = await SupplyTestData.CreateSupplyAsync(
            factory.Postgres, household, "VT001", ct, referencePrice: 20000m, averageUnitCost: 22000m);

        var member = factory.CreateClientFor(household.Member);
        var detail = (await (await member.GetAsync($"{SuppliesPath}/{supply.Id}", ct)).ReadJsonAsync(ct)).GetProperty("data");
        var row = (await (await member.GetAsync(SuppliesPath, ct)).ReadJsonAsync(ct)).GetProperty("data")[0];
        foreach (var hidden in new[] { detail, row })
        {
            Assert.False(hidden.TryGetProperty("referencePrice", out _));
            Assert.False(hidden.TryGetProperty("averageUnitCost", out _));
            Assert.Equal(0m, hidden.GetProperty("stockQuantity").GetDecimal());
        }

        foreach (var user in new[] { household.MemberWithAllPermissions, household.Owner })
        {
            var client = factory.CreateClientFor(user);
            var seen = (await (await client.GetAsync($"{SuppliesPath}/{supply.Id}", ct)).ReadJsonAsync(ct)).GetProperty("data");
            var seenRow = (await (await client.GetAsync(SuppliesPath, ct)).ReadJsonAsync(ct)).GetProperty("data")[0];
            Assert.Equal(20000m, seen.GetProperty("referencePrice").GetDecimal());
            Assert.Equal(22000m, seen.GetProperty("averageUnitCost").GetDecimal());
            Assert.Equal(20000m, seenRow.GetProperty("referencePrice").GetDecimal());
            Assert.Equal(22000m, seenRow.GetProperty("averageUnitCost").GetDecimal());
        }
    }

    [Fact(DisplayName = "BR-GEN-01: xem, sửa vật tư của hộ khác bằng id trả NOT_FOUND")]
    public async Task AccessOtherHouseholdSupply_ReturnsNotFound()
    {
        var ct = TestContext.Current.CancellationToken;
        var householdA = await factory.CreateHouseholdAsync(ct);
        var householdB = await factory.CreateHouseholdAsync(ct);
        var supplyB = await SupplyTestData.CreateSupplyAsync(factory.Postgres, householdB, "VT001", ct);
        var client = factory.CreateClientFor(householdA.Owner);

        await (await client.GetAsync($"{SuppliesPath}/{supplyB.Id}", ct))
            .AssertProblemAsync(HttpStatusCode.NotFound, ErrorCodes.NotFound, ct);
        await (await client.PutAsJsonAsync($"{SuppliesPath}/{supplyB.Id}", UpdateOf("VT009", supplyB.Version), ct))
            .AssertProblemAsync(HttpStatusCode.NotFound, ErrorCodes.NotFound, ct);
    }

    [Fact(DisplayName = "FR-SUP-01: sửa vật tư, đổi trạng thái; giá trị mới đọc lại được")]
    public async Task Update_ChangesFieldsAndStatus()
    {
        var ct = TestContext.Current.CancellationToken;
        var household = await factory.CreateHouseholdAsync(ct);
        var supply = await SupplyTestData.CreateSupplyAsync(factory.Postgres, household, "VT001", ct);
        var client = factory.CreateClientFor(household.Owner);

        var response = await client.PutAsJsonAsync(
            $"{SuppliesPath}/{supply.Id}",
            UpdateOf("VT-NPK", supply.Version, status: "Inactive", name: "  Phân NPK loại 2  "),
            ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var read = await (await client.GetAsync($"{SuppliesPath}/{supply.Id}", ct)).ReadJsonAsync(ct);
        var data = read.GetProperty("data");
        Assert.Equal("VT-NPK", data.GetProperty("code").GetString());
        Assert.Equal("Phân NPK loại 2", data.GetProperty("name").GetString());
        Assert.Equal("Inactive", data.GetProperty("status").GetString());
    }

    [Fact(DisplayName = "BR-GEN-11: sửa vật tư bằng version cũ trả CONCURRENCY_CONFLICT")]
    public async Task Update_WithStaleVersion_ReturnsConcurrencyConflict()
    {
        var ct = TestContext.Current.CancellationToken;
        var household = await factory.CreateHouseholdAsync(ct);
        var supply = await SupplyTestData.CreateSupplyAsync(factory.Postgres, household, "VT001", ct);
        var client = factory.CreateClientFor(household.Owner);

        var first = await client.PutAsJsonAsync($"{SuppliesPath}/{supply.Id}", UpdateOf("VT001", supply.Version), ct);
        var second = await client.PutAsJsonAsync($"{SuppliesPath}/{supply.Id}", UpdateOf("VT001", supply.Version), ct);

        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        await second.AssertProblemAsync(HttpStatusCode.Conflict, ErrorCodes.ConcurrencyConflict, ct);
    }

    [Fact(DisplayName = "BR-CAT-05: đổi đơn vị khi vật tư đã có giao dịch kho, kể cả giao dịch đã Void, trả UNIT_LOCKED và trả cờ unitLocked")]
    public async Task Update_UnitWithStockTransactions_ReturnsUnitLocked()
    {
        var ct = TestContext.Current.CancellationToken;
        var household = await factory.CreateHouseholdAsync(ct);
        var supply = await SupplyTestData.CreateSupplyAsync(factory.Postgres, household, "VT001", ct);
        await SupplyTestData.CreateTransactionAsync(
            factory.Postgres, household, supply, StockTransactionType.In, 10m, ct, voided: true);
        var client = factory.CreateClientFor(household.Owner);

        var detail = (await (await client.GetAsync($"{SuppliesPath}/{supply.Id}", ct)).ReadJsonAsync(ct)).GetProperty("data");
        Assert.True(detail.GetProperty("unitLocked").GetBoolean());

        var response = await client.PutAsJsonAsync(
            $"{SuppliesPath}/{supply.Id}",
            UpdateOf("VT001", supply.Version, unit: "Bag"),
            ct);

        await response.AssertProblemAsync(HttpStatusCode.UnprocessableEntity, ErrorCodes.UnitLocked, ct);
    }

    [Fact(DisplayName = "BR-CAT-05: vật tư chưa có giao dịch kho đổi được đơn vị; đã có giao dịch vẫn sửa được trường khác")]
    public async Task Update_UnitWithoutTransactions_Succeeds()
    {
        var ct = TestContext.Current.CancellationToken;
        var household = await factory.CreateHouseholdAsync(ct);
        var free = await SupplyTestData.CreateSupplyAsync(factory.Postgres, household, "VT001", ct);
        var locked = await SupplyTestData.CreateSupplyAsync(factory.Postgres, household, "VT002", ct);
        await SupplyTestData.CreateTransactionAsync(factory.Postgres, household, locked, StockTransactionType.In, 10m, ct);
        var client = factory.CreateClientFor(household.Owner);

        var freeResponse = await client.PutAsJsonAsync($"{SuppliesPath}/{free.Id}", UpdateOf("VT001", free.Version, unit: "Bag"), ct);
        var lockedResponse = await client.PutAsJsonAsync($"{SuppliesPath}/{locked.Id}", UpdateOf("VT002", locked.Version), ct);

        Assert.Equal(HttpStatusCode.OK, freeResponse.StatusCode);
        Assert.Equal("Bag", (await freeResponse.ReadJsonAsync(ct)).GetProperty("data").GetProperty("unit").GetString());
        Assert.Equal(HttpStatusCode.OK, lockedResponse.StatusCode);
        var data = (await lockedResponse.ReadJsonAsync(ct)).GetProperty("data");
        Assert.True(data.GetProperty("unitLocked").GetBoolean());
        Assert.Equal(10m, data.GetProperty("stockQuantity").GetDecimal());
    }

    [Fact(DisplayName = "BR-CAT-06: gửi kèm averageUnitCost, stockQuantity khi tạo, sửa không có tác dụng")]
    public async Task CreateOrUpdate_IgnoresAverageUnitCostAndStock()
    {
        var ct = TestContext.Current.CancellationToken;
        var household = await factory.CreateHouseholdAsync(ct);
        var supply = await SupplyTestData.CreateSupplyAsync(factory.Postgres, household, "VT001", ct, averageUnitCost: 22000m);
        var client = factory.CreateClientFor(household.Owner);

        var created = await client.PostAsJsonAsync(
            SuppliesPath,
            new { name = "Thuốc", group = "Pesticide", unit = "Bottle", averageUnitCost = 99m, stockQuantity = 50m },
            ct);
        var updated = await client.PutAsJsonAsync(
            $"{SuppliesPath}/{supply.Id}",
            new
            {
                code = "VT001",
                name = "Phân",
                group = "Fertilizer",
                unit = "Kilogram",
                status = "Active",
                averageUnitCost = 1m,
                stockQuantity = 50m,
                version = supply.Version,
            },
            ct);

        var createdData = (await created.ReadJsonAsync(ct)).GetProperty("data");
        Assert.Equal(0m, createdData.GetProperty("averageUnitCost").GetDecimal());
        Assert.Equal(0m, createdData.GetProperty("stockQuantity").GetDecimal());
        var updatedData = (await updated.ReadJsonAsync(ct)).GetProperty("data");
        Assert.Equal(22000m, updatedData.GetProperty("averageUnitCost").GetDecimal());
        Assert.Equal(0m, updatedData.GetProperty("stockQuantity").GetDecimal());
    }

    [Theory(DisplayName = "Dữ liệu sai khi tạo vật tư trả VALIDATION_ERROR ở đúng trường")]
    [InlineData("""{"group":"Seed","unit":"Kilogram"}""", "name")]
    [InlineData("""{"name":"A","unit":"Kilogram"}""", "group")]
    [InlineData("""{"name":"A","group":"Seed"}""", "unit")]
    [InlineData("""{"name":"A","group":"Moon","unit":"Kilogram"}""", "group")]
    [InlineData("""{"name":"A","group":"Seed","unit":"Kilogram","minStockLevel":-1}""", "minStockLevel")]
    [InlineData("""{"name":"A","group":"Seed","unit":"Kilogram","minStockLevel":1.2345}""", "minStockLevel")]
    [InlineData("""{"name":"A","group":"Seed","unit":"Kilogram","referencePrice":-1}""", "referencePrice")]
    public async Task Create_InvalidData_ReturnsValidationError(string json, string field)
    {
        var ct = TestContext.Current.CancellationToken;
        var household = await factory.CreateHouseholdAsync(ct);

        var response = await factory.CreateClientFor(household.Owner).PostAsync(
            SuppliesPath,
            new StringContent(json, System.Text.Encoding.UTF8, "application/json"),
            ct);

        var problem = await response.AssertProblemAsync(HttpStatusCode.BadRequest, ErrorCodes.ValidationError, ct);
        Assert.True(problem.GetProperty("errors").TryGetProperty(field, out _));
    }

    [Fact(DisplayName = "BR-SUP-01: tồn đúng sau In, Out, Adjustment; giao dịch Void không tính; danh sách và chi tiết cùng một số")]
    public async Task Stock_SumsSignedQuantitiesExceptVoided()
    {
        var ct = TestContext.Current.CancellationToken;
        var household = await factory.CreateHouseholdAsync(ct);
        var supply = await SupplyTestData.CreateSupplyAsync(factory.Postgres, household, "VT001", ct);
        var plot = await TestData.CreatePlotAsync(factory.Postgres, household, "TD001", ct);
        var season = await TestData.CreateSeasonAsync(factory.Postgres, household, plot, SeasonStatus.InProgress, ct);
        var client = factory.CreateClientFor(household.Owner);

        await SupplyTestData.CreateTransactionAsync(factory.Postgres, household, supply, StockTransactionType.In, 100m, ct);
        Assert.Equal(100m, await StockOfAsync(client, supply.Id, ct));

        await SupplyTestData.CreateTransactionAsync(
            factory.Postgres, household, supply, StockTransactionType.Out, -30.5m, ct, seasonId: season.Id);
        Assert.Equal(69.5m, await StockOfAsync(client, supply.Id, ct));

        await SupplyTestData.CreateTransactionAsync(factory.Postgres, household, supply, StockTransactionType.Adjustment, -4.5m, ct);
        await SupplyTestData.CreateTransactionAsync(factory.Postgres, household, supply, StockTransactionType.Adjustment, 10m, ct);
        Assert.Equal(75m, await StockOfAsync(client, supply.Id, ct));

        await SupplyTestData.CreateTransactionAsync(
            factory.Postgres, household, supply, StockTransactionType.In, 1000m, ct, voided: true);
        Assert.Equal(75m, await StockOfAsync(client, supply.Id, ct));

        var row = (await (await client.GetAsync(SuppliesPath, ct)).ReadJsonAsync(ct)).GetProperty("data")[0];
        Assert.Equal(75m, row.GetProperty("stockQuantity").GetDecimal());
    }

    [Fact(DisplayName = "FR-SUP-02: không có API xóa vật tư")]
    public async Task Delete_IsNotAvailable()
    {
        var ct = TestContext.Current.CancellationToken;
        var household = await factory.CreateHouseholdAsync(ct);
        var supply = await SupplyTestData.CreateSupplyAsync(factory.Postgres, household, "VT001", ct);

        var response = await factory.CreateClientFor(household.Owner).DeleteAsync($"{SuppliesPath}/{supply.Id}", ct);

        Assert.Equal(HttpStatusCode.MethodNotAllowed, response.StatusCode);
    }

    private static async Task<decimal> StockOfAsync(HttpClient client, Guid supplyId, CancellationToken ct)
    {
        var body = await (await client.GetAsync($"{SuppliesPath}/{supplyId}", ct)).ReadJsonAsync(ct);
        return body.GetProperty("data").GetProperty("stockQuantity").GetDecimal();
    }

    private static object NewSupply(string? code) => new
    {
        code,
        name = "Phân NPK",
        group = "Fertilizer",
        unit = "Kilogram",
        minStockLevel = 30m,
        referencePrice = 20000m,
        note = "Mua ở đại lý",
    };

    private static object UpdateOf(
        string code,
        uint version,
        string unit = "Kilogram",
        string status = "Active",
        string name = "Phân NPK") => new
    {
        code,
        name,
        group = "Fertilizer",
        unit,
        minStockLevel = 30m,
        referencePrice = 20000m,
        status,
        version,
    };
}
