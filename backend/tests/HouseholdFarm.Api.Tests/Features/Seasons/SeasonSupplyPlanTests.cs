using System.Net;
using System.Net.Http.Json;
using HouseholdFarm.Api.Common.Errors;
using HouseholdFarm.Api.Common.Responses;
using HouseholdFarm.Api.Domain.Entities;
using HouseholdFarm.Api.Domain.Enums;
using HouseholdFarm.Api.Features.Seasons;
using HouseholdFarm.Api.Tests.Features.Supplies;
using HouseholdFarm.Api.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace HouseholdFarm.Api.Tests.Features.Seasons;

public class SeasonSupplyPlanTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private const string SupplyPlansPath = "/api/season-supply-plans";

    [Fact(DisplayName = "FR-SEA-04: Chủ hộ thêm vật tư dự kiến khi đủ tồn kho không có cảnh báo")]
    public async Task Create_WhenSufficientStock_ReturnsOkWithoutWarning()
    {
        var ct = TestContext.Current.CancellationToken;
        var household = await factory.CreateHouseholdAsync(ct);
        var plot = await TestData.CreatePlotAsync(factory.Postgres, household, "TD001", ct);
        var season = await TestData.CreateSeasonAsync(factory.Postgres, household, plot, SeasonStatus.Planned, ct);

        // Tạo vật tư tồn 100, ngưỡng tối thiểu 20
        var supply = await SupplyTestData.CreateSupplyAsync(factory.Postgres, household, "VT001", ct, minStockLevel: 20m);
        await SupplyTestData.CreateTransactionAsync(factory.Postgres, household, supply, StockTransactionType.In, 100m, ct, unitCost: 10000m);

        var client = factory.CreateClientFor(household.Owner);
        var request = new CreateSeasonSupplyPlanRequest(season.Id, supply.Id, 50m);
        var response = await client.PostAsJsonAsync(SupplyPlansPath, request, ct);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var root = await response.ReadJsonAsync(ct);
        var data = root.GetProperty("data");

        Assert.Equal(50m, data.GetProperty("plannedQuantity").GetDecimal());
        Assert.Equal(100m, data.GetProperty("currentStock").GetDecimal());
        Assert.Equal(20m, data.GetProperty("minStockLevel").GetDecimal());
        Assert.True(data.GetProperty("warningCode").ValueKind is System.Text.Json.JsonValueKind.Null);

        // Cảnh báo trong envelope rỗng
        Assert.False(root.TryGetProperty("warnings", out var warnings) && warnings.GetArrayLength() > 0);
    }

    [Fact(DisplayName = "BR-SEA-09: Thêm vật tư dự kiến lớn hơn tồn hiện tại trả cảnh báo SUPPLY_SHORTAGE")]
    public async Task Create_WhenPlannedQuantityExceedsStock_ReturnsSupplyShortageWarning()
    {
        var ct = TestContext.Current.CancellationToken;
        var household = await factory.CreateHouseholdAsync(ct);
        var plot = await TestData.CreatePlotAsync(factory.Postgres, household, "TD001", ct);
        var season = await TestData.CreateSeasonAsync(factory.Postgres, household, plot, SeasonStatus.Planned, ct);

        // Tồn 30, dự kiến 50
        var supply = await SupplyTestData.CreateSupplyAsync(factory.Postgres, household, "VT001", ct, minStockLevel: 10m);
        await SupplyTestData.CreateTransactionAsync(factory.Postgres, household, supply, StockTransactionType.In, 30m, ct, unitCost: 10000m);

        var client = factory.CreateClientFor(household.Owner);
        var request = new CreateSeasonSupplyPlanRequest(season.Id, supply.Id, 50m);
        var response = await client.PostAsJsonAsync(SupplyPlansPath, request, ct);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var root = await response.ReadJsonAsync(ct);
        var data = root.GetProperty("data");

        Assert.Equal(WarningCodes.SupplyShortage, data.GetProperty("warningCode").GetString());

        // Kiểm tra warnings trong envelope
        Assert.True(root.TryGetProperty("warnings", out var warnings));
        Assert.Contains(warnings.EnumerateArray(), w => w.GetProperty("code").GetString() == WarningCodes.SupplyShortage);
    }

    [Fact(DisplayName = "BR-SEA-09: Thêm vật tư dự kiến khiến tồn sau khi dùng dưới ngưỡng tối thiểu trả LOW_STOCK")]
    public async Task Create_WhenRemainingBelowMinStock_ReturnsLowStockWarning()
    {
        var ct = TestContext.Current.CancellationToken;
        var household = await factory.CreateHouseholdAsync(ct);
        var plot = await TestData.CreatePlotAsync(factory.Postgres, household, "TD001", ct);
        var season = await TestData.CreateSeasonAsync(factory.Postgres, household, plot, SeasonStatus.Planned, ct);

        // Tồn 100, ngưỡng tối thiểu 40, dự kiến dùng 70 -> còn 30 < 40
        var supply = await SupplyTestData.CreateSupplyAsync(factory.Postgres, household, "VT001", ct, minStockLevel: 40m);
        await SupplyTestData.CreateTransactionAsync(factory.Postgres, household, supply, StockTransactionType.In, 100m, ct, unitCost: 10000m);

        var client = factory.CreateClientFor(household.Owner);
        var request = new CreateSeasonSupplyPlanRequest(season.Id, supply.Id, 70m);
        var response = await client.PostAsJsonAsync(SupplyPlansPath, request, ct);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var root = await response.ReadJsonAsync(ct);
        var data = root.GetProperty("data");

        Assert.Equal(WarningCodes.LowStock, data.GetProperty("warningCode").GetString());

        Assert.True(root.TryGetProperty("warnings", out var warnings));
        Assert.Contains(warnings.EnumerateArray(), w => w.GetProperty("code").GetString() == WarningCodes.LowStock);
    }

    [Fact(DisplayName = "BR-SEA-10: Thêm vật tư dự kiến khi mùa vụ InProgress trả SEASON_INVALID_STATE")]
    public async Task Create_WhenSeasonNotPlanned_ReturnsSeasonInvalidState()
    {
        var ct = TestContext.Current.CancellationToken;
        var household = await factory.CreateHouseholdAsync(ct);
        var plot = await TestData.CreatePlotAsync(factory.Postgres, household, "TD001", ct);
        var season = await TestData.CreateSeasonAsync(factory.Postgres, household, plot, SeasonStatus.InProgress, ct);
        var supply = await SupplyTestData.CreateSupplyAsync(factory.Postgres, household, "VT001", ct);

        var client = factory.CreateClientFor(household.Owner);
        var request = new CreateSeasonSupplyPlanRequest(season.Id, supply.Id, 10m);
        var response = await client.PostAsJsonAsync(SupplyPlansPath, request, ct);

        await response.AssertProblemAsync(HttpStatusCode.UnprocessableEntity, ErrorCodes.SeasonInvalidState, ct);
    }

    [Fact(DisplayName = "BR-GEN-13: Thêm vật tư đang Inactive trả CATALOG_INACTIVE")]
    public async Task Create_WhenSupplyInactive_ReturnsCatalogInactive()
    {
        var ct = TestContext.Current.CancellationToken;
        var household = await factory.CreateHouseholdAsync(ct);
        var plot = await TestData.CreatePlotAsync(factory.Postgres, household, "TD001", ct);
        var season = await TestData.CreateSeasonAsync(factory.Postgres, household, plot, SeasonStatus.Planned, ct);
        var supply = await SupplyTestData.CreateSupplyAsync(factory.Postgres, household, "VT001", ct, status: RecordStatus.Inactive);

        var client = factory.CreateClientFor(household.Owner);
        var request = new CreateSeasonSupplyPlanRequest(season.Id, supply.Id, 10m);
        var response = await client.PostAsJsonAsync(SupplyPlansPath, request, ct);

        await response.AssertProblemAsync(HttpStatusCode.UnprocessableEntity, ErrorCodes.CatalogInactive, ct);
    }

    [Fact(DisplayName = "FR-SEA-04: Thêm trùng vật tư trong cùng mùa vụ trả VALIDATION_ERROR")]
    public async Task Create_WhenDuplicateSupply_ReturnsValidationError()
    {
        var ct = TestContext.Current.CancellationToken;
        var household = await factory.CreateHouseholdAsync(ct);
        var plot = await TestData.CreatePlotAsync(factory.Postgres, household, "TD001", ct);
        var season = await TestData.CreateSeasonAsync(factory.Postgres, household, plot, SeasonStatus.Planned, ct);
        var supply = await SupplyTestData.CreateSupplyAsync(factory.Postgres, household, "VT001", ct);

        var client = factory.CreateClientFor(household.Owner);
        var request = new CreateSeasonSupplyPlanRequest(season.Id, supply.Id, 10m);
        var res1 = await client.PostAsJsonAsync(SupplyPlansPath, request, ct);
        Assert.Equal(HttpStatusCode.Created, res1.StatusCode);

        var res2 = await client.PostAsJsonAsync(SupplyPlansPath, request, ct);
        await res2.AssertProblemAsync(HttpStatusCode.BadRequest, ErrorCodes.ValidationError, ct);
    }

    [Fact(DisplayName = "FR-SEA-04: Thành viên không phải Chủ hộ không được khai báo vật tư dự kiến")]
    public async Task Create_WhenMember_ReturnsForbidden()
    {
        var ct = TestContext.Current.CancellationToken;
        var household = await factory.CreateHouseholdAsync(ct);
        var member = household.Member;
        var plot = await TestData.CreatePlotAsync(factory.Postgres, household, "TD001", ct);
        var season = await TestData.CreateSeasonAsync(factory.Postgres, household, plot, SeasonStatus.Planned, ct);
        var supply = await SupplyTestData.CreateSupplyAsync(factory.Postgres, household, "VT001", ct);

        var client = factory.CreateClientFor(member);
        var request = new CreateSeasonSupplyPlanRequest(season.Id, supply.Id, 10m);
        var response = await client.PostAsJsonAsync(SupplyPlansPath, request, ct);

        await response.AssertProblemAsync(HttpStatusCode.Forbidden, ErrorCodes.Forbidden, ct);
    }

    [Fact(DisplayName = "FR-SEA-04: Chủ hộ sửa lượng vật tư dự kiến và cập nhật cảnh báo")]
    public async Task Update_WhenPlanned_UpdatesQuantityAndWarning()
    {
        var ct = TestContext.Current.CancellationToken;
        var household = await factory.CreateHouseholdAsync(ct);
        var plot = await TestData.CreatePlotAsync(factory.Postgres, household, "TD001", ct);
        var season = await TestData.CreateSeasonAsync(factory.Postgres, household, plot, SeasonStatus.Planned, ct);
        var supply = await SupplyTestData.CreateSupplyAsync(factory.Postgres, household, "VT001", ct, minStockLevel: 20m);
        await SupplyTestData.CreateTransactionAsync(factory.Postgres, household, supply, StockTransactionType.In, 50m, ct, unitCost: 10000m);

        var client = factory.CreateClientFor(household.Owner);
        var createRes = await client.PostAsJsonAsync(SupplyPlansPath, new CreateSeasonSupplyPlanRequest(season.Id, supply.Id, 20m), ct);
        Assert.Equal(HttpStatusCode.Created, createRes.StatusCode);
        var plan = (await createRes.ReadJsonAsync(ct)).GetProperty("data");
        var planId = plan.GetProperty("id").GetGuid();
        var version = plan.GetProperty("version").GetUInt32();

        // Sửa lên 60m (vượt quá 50m)
        var updateRes = await client.PutAsJsonAsync($"{SupplyPlansPath}/{planId}", new UpdateSeasonSupplyPlanRequest(60m, version), ct);
        Assert.Equal(HttpStatusCode.OK, updateRes.StatusCode);
        var updatedData = (await updateRes.ReadJsonAsync(ct)).GetProperty("data");

        Assert.Equal(60m, updatedData.GetProperty("plannedQuantity").GetDecimal());
        Assert.Equal(WarningCodes.SupplyShortage, updatedData.GetProperty("warningCode").GetString());
    }

    [Fact(DisplayName = "BR-SEA-10: Sửa lượng vật tư dự kiến khi mùa vụ InProgress trả SEASON_INVALID_STATE")]
    public async Task Update_WhenSeasonNotPlanned_ReturnsSeasonInvalidState()
    {
        var ct = TestContext.Current.CancellationToken;
        var household = await factory.CreateHouseholdAsync(ct);
        var plot = await TestData.CreatePlotAsync(factory.Postgres, household, "TD001", ct);
        var season = await TestData.CreateSeasonAsync(factory.Postgres, household, plot, SeasonStatus.Planned, ct);
        var supply = await SupplyTestData.CreateSupplyAsync(factory.Postgres, household, "VT001", ct);

        var client = factory.CreateClientFor(household.Owner);
        var createRes = await client.PostAsJsonAsync(SupplyPlansPath, new CreateSeasonSupplyPlanRequest(season.Id, supply.Id, 20m), ct);
        Assert.Equal(HttpStatusCode.Created, createRes.StatusCode);
        var plan = (await createRes.ReadJsonAsync(ct)).GetProperty("data");
        var planId = plan.GetProperty("id").GetGuid();
        var version = plan.GetProperty("version").GetUInt32();

        // Chuyển mùa vụ sang InProgress
        await using (var db = factory.Postgres.CreateDbContext(TestCurrentUser.For(household.Owner)))
        {
            var s = await db.Seasons.FindAsync([season.Id], ct);
            s!.Status = SeasonStatus.InProgress;
            await db.SaveChangesAsync(ct);
        }

        var updateRes = await client.PutAsJsonAsync($"{SupplyPlansPath}/{planId}", new UpdateSeasonSupplyPlanRequest(30m, version), ct);
        await updateRes.AssertProblemAsync(HttpStatusCode.UnprocessableEntity, ErrorCodes.SeasonInvalidState, ct);
    }

    [Fact(DisplayName = "BR-GEN-11: Sửa vật tư dự kiến với phiên bản cũ trả CONCURRENCY_CONFLICT")]
    public async Task Update_WhenVersionMismatch_ReturnsConcurrencyConflict()
    {
        var ct = TestContext.Current.CancellationToken;
        var household = await factory.CreateHouseholdAsync(ct);
        var plot = await TestData.CreatePlotAsync(factory.Postgres, household, "TD001", ct);
        var season = await TestData.CreateSeasonAsync(factory.Postgres, household, plot, SeasonStatus.Planned, ct);
        var supply = await SupplyTestData.CreateSupplyAsync(factory.Postgres, household, "VT001", ct);

        var client = factory.CreateClientFor(household.Owner);
        var createRes = await client.PostAsJsonAsync(SupplyPlansPath, new CreateSeasonSupplyPlanRequest(season.Id, supply.Id, 20m), ct);
        Assert.Equal(HttpStatusCode.Created, createRes.StatusCode);
        var plan = (await createRes.ReadJsonAsync(ct)).GetProperty("data");
        var planId = plan.GetProperty("id").GetGuid();
        var version = plan.GetProperty("version").GetUInt32();

        // Sửa với version sai
        var updateRes = await client.PutAsJsonAsync($"{SupplyPlansPath}/{planId}", new UpdateSeasonSupplyPlanRequest(30m, version + 99), ct);
        await updateRes.AssertProblemAsync(HttpStatusCode.Conflict, ErrorCodes.ConcurrencyConflict, ct);
    }

    [Fact(DisplayName = "BR-SEA-10: Xóa vật tư dự kiến khi mùa vụ InProgress trả SEASON_INVALID_STATE")]
    public async Task Delete_WhenSeasonNotPlanned_ReturnsSeasonInvalidState()
    {
        var ct = TestContext.Current.CancellationToken;
        var household = await factory.CreateHouseholdAsync(ct);
        var plot = await TestData.CreatePlotAsync(factory.Postgres, household, "TD001", ct);
        var season = await TestData.CreateSeasonAsync(factory.Postgres, household, plot, SeasonStatus.Planned, ct);
        var supply = await SupplyTestData.CreateSupplyAsync(factory.Postgres, household, "VT001", ct);

        var client = factory.CreateClientFor(household.Owner);
        var createRes = await client.PostAsJsonAsync(SupplyPlansPath, new CreateSeasonSupplyPlanRequest(season.Id, supply.Id, 20m), ct);
        Assert.Equal(HttpStatusCode.Created, createRes.StatusCode);
        var planId = (await createRes.ReadJsonAsync(ct)).GetProperty("data").GetProperty("id").GetGuid();

        // Chuyển mùa vụ sang InProgress
        await using (var db = factory.Postgres.CreateDbContext(TestCurrentUser.For(household.Owner)))
        {
            var s = await db.Seasons.FindAsync([season.Id], ct);
            s!.Status = SeasonStatus.InProgress;
            await db.SaveChangesAsync(ct);
        }

        var deleteRes = await client.DeleteAsync($"{SupplyPlansPath}/{planId}", ct);
        await deleteRes.AssertProblemAsync(HttpStatusCode.UnprocessableEntity, ErrorCodes.SeasonInvalidState, ct);
    }

    [Fact(DisplayName = "BR-SEA-10, data model 2.6: Xóa thật dòng vật tư dự kiến khi mùa vụ Planned")]
    public async Task Delete_WhenPlanned_RemovesRecord()
    {
        var ct = TestContext.Current.CancellationToken;
        var household = await factory.CreateHouseholdAsync(ct);
        var plot = await TestData.CreatePlotAsync(factory.Postgres, household, "TD001", ct);
        var season = await TestData.CreateSeasonAsync(factory.Postgres, household, plot, SeasonStatus.Planned, ct);
        var supply = await SupplyTestData.CreateSupplyAsync(factory.Postgres, household, "VT001", ct);

        var client = factory.CreateClientFor(household.Owner);
        var createRes = await client.PostAsJsonAsync(SupplyPlansPath, new CreateSeasonSupplyPlanRequest(season.Id, supply.Id, 20m), ct);
        Assert.Equal(HttpStatusCode.Created, createRes.StatusCode);
        var planId = (await createRes.ReadJsonAsync(ct)).GetProperty("data").GetProperty("id").GetGuid();

        var deleteRes = await client.DeleteAsync($"{SupplyPlansPath}/{planId}", ct);
        Assert.Equal(HttpStatusCode.NoContent, deleteRes.StatusCode);

        // Kiểm tra database: đã bị xóa thật
        await using var db = factory.Postgres.CreateDbContext(TestCurrentUser.For(household.Owner));
        var exists = await db.SeasonSupplyPlans.AnyAsync(p => p.Id == planId, ct);
        Assert.False(exists);
    }

    [Fact(DisplayName = "FR-SEA-04: Lấy danh sách vật tư dự kiến kèm thông tin tồn và cảnh báo")]
    public async Task GetList_WhenOwner_ReturnsListWithStockAndWarning()
    {
        var ct = TestContext.Current.CancellationToken;
        var household = await factory.CreateHouseholdAsync(ct);
        var plot = await TestData.CreatePlotAsync(factory.Postgres, household, "TD001", ct);
        var season = await TestData.CreateSeasonAsync(factory.Postgres, household, plot, SeasonStatus.Planned, ct);

        var supply1 = await SupplyTestData.CreateSupplyAsync(factory.Postgres, household, "VT001", ct, name: "Phân lân");
        var supply2 = await SupplyTestData.CreateSupplyAsync(factory.Postgres, household, "VT002", ct, name: "Thuốc trừ sâu");

        var client = factory.CreateClientFor(household.Owner);
        await client.PostAsJsonAsync(SupplyPlansPath, new CreateSeasonSupplyPlanRequest(season.Id, supply1.Id, 10m), ct);
        await client.PostAsJsonAsync(SupplyPlansPath, new CreateSeasonSupplyPlanRequest(season.Id, supply2.Id, 5m), ct);

        var listRes = await client.GetAsync($"{SupplyPlansPath}?seasonId={season.Id}", ct);
        Assert.Equal(HttpStatusCode.OK, listRes.StatusCode);

        var items = (await listRes.ReadJsonAsync(ct)).GetProperty("data");
        Assert.Equal(2, items.GetArrayLength());
    }
}
