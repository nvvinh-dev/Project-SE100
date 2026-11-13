using System.Net;
using System.Text.Json;
using HouseholdFarm.Api.Common.Errors;
using HouseholdFarm.Api.Domain.Enums;
using HouseholdFarm.Api.Tests.Infrastructure;

namespace HouseholdFarm.Api.Tests.Features.Crops;

/// <summary>Danh sách cây trồng: tìm kiếm, lọc theo nhóm, trạng thái, phân trang (FR-CROP-04).</summary>
public class CropListTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private const string CropsPath = "/api/crops";

    [Fact(DisplayName = "FR-CROP-04: lọc theo nhóm cây, trạng thái; mỗi dòng có tên, nhóm, đơn vị, trạng thái")]
    public async Task List_FiltersByGroupAndStatus()
    {
        var ct = TestContext.Current.CancellationToken;
        var household = await factory.CreateHouseholdAsync(ct);
        await TestData.CreateCropAsync(factory.Postgres, household, "Lúa ST25", ct);
        await TestData.CreateCropAsync(factory.Postgres, household, "Cà chua", ct, group: CropGroup.Vegetable, yieldUnit: Unit.Quintal);
        await TestData.CreateCropAsync(factory.Postgres, household, "Lúa cũ", ct, status: RecordStatus.Inactive);
        var client = factory.CreateClientFor(household.Owner);

        Assert.Equal(["Cà chua"], await NamesAsync(client, "?group=Vegetable", ct));
        Assert.Equal(["Lúa cũ"], await NamesAsync(client, "?status=Inactive", ct));
        Assert.Equal(["Lúa ST25"], await NamesAsync(client, "?group=Staple&status=Active", ct));

        var body = await (await client.GetAsync($"{CropsPath}?group=Vegetable", ct)).ReadJsonAsync(ct);
        var item = Assert.Single(body.GetProperty("data").EnumerateArray());
        Assert.Equal("Vegetable", item.GetProperty("group").GetString());
        Assert.Equal("Quintal", item.GetProperty("yieldUnit").GetString());
        Assert.Equal("Active", item.GetProperty("status").GetString());
    }

    [Fact(DisplayName = "FR-CROP-04: tìm theo tên, không phân biệt hoa thường; % trong chuỗi tìm không phải ký tự đại diện")]
    public async Task List_SearchesName()
    {
        var ct = TestContext.Current.CancellationToken;
        var household = await factory.CreateHouseholdAsync(ct);
        await TestData.CreateCropAsync(factory.Postgres, household, "Lúa ST25", ct);
        await TestData.CreateCropAsync(factory.Postgres, household, "Bưởi da xanh", ct);
        await TestData.CreateCropAsync(factory.Postgres, household, "Ớt 100% cay", ct);
        var client = factory.CreateClientFor(household.Owner);

        Assert.Equal(["Lúa ST25"], await NamesAsync(client, "?search=lúa%20st", ct));
        Assert.Equal(["Bưởi da xanh"], await NamesAsync(client, "?search=DA%20XANH", ct));
        Assert.Equal(["Ớt 100% cay"], await NamesAsync(client, "?search=100%25", ct));
        Assert.Empty(await NamesAsync(client, "?search=%25%25%25", ct));
    }

    [Fact(DisplayName = "FR-CROP-04: phân trang trả đúng meta; sắp xếp mặc định theo tên, -name là tên giảm dần")]
    public async Task List_PagesAndSorts()
    {
        var ct = TestContext.Current.CancellationToken;
        var household = await factory.CreateHouseholdAsync(ct);
        await TestData.CreateCropAsync(factory.Postgres, household, "Cam", ct);
        await TestData.CreateCropAsync(factory.Postgres, household, "Bắp", ct);
        await TestData.CreateCropAsync(factory.Postgres, household, "Dưa", ct);
        var client = factory.CreateClientFor(household.Owner);

        var page2 = await (await client.GetAsync($"{CropsPath}?page=2&pageSize=2", ct)).ReadJsonAsync(ct);

        Assert.Equal(["Dưa"], Names(page2));
        var meta = page2.GetProperty("meta");
        Assert.Equal(2, meta.GetProperty("page").GetInt32());
        Assert.Equal(2, meta.GetProperty("pageSize").GetInt32());
        Assert.Equal(3, meta.GetProperty("totalCount").GetInt32());
        Assert.Equal(["Dưa", "Cam", "Bắp"], await NamesAsync(client, "?sort=-name", ct));
    }

    [Fact(DisplayName = "Sắp xếp theo trường không hỗ trợ, nhóm cây không hợp lệ trả VALIDATION_ERROR")]
    public async Task List_InvalidSortOrGroup_ReturnsValidationError()
    {
        var ct = TestContext.Current.CancellationToken;
        var household = await factory.CreateHouseholdAsync(ct);
        var client = factory.CreateClientFor(household.Owner);

        var badSort = await (await client.GetAsync($"{CropsPath}?sort=yieldUnit", ct))
            .AssertProblemAsync(HttpStatusCode.BadRequest, ErrorCodes.ValidationError, ct);
        await (await client.GetAsync($"{CropsPath}?group=Flower", ct))
            .AssertProblemAsync(HttpStatusCode.BadRequest, ErrorCodes.ValidationError, ct);

        Assert.Equal("Chỉ sắp xếp được theo: name, createdAt.", badSort.GetProperty("errors").GetProperty("sort")[0].GetString());
    }

    [Fact(DisplayName = "TC-10: danh sách cây không có cây của hộ khác")]
    public async Task TC10_List_ExcludesOtherHousehold()
    {
        var ct = TestContext.Current.CancellationToken;
        var householdA = await factory.CreateHouseholdAsync(ct);
        var householdB = await factory.CreateHouseholdAsync(ct);
        await TestData.CreateCropAsync(factory.Postgres, householdA, "Lúa ST25", ct);
        await TestData.CreateCropAsync(factory.Postgres, householdB, "Lúa ST25", ct);
        await TestData.CreateCropAsync(factory.Postgres, householdB, "Ngô", ct);

        var body = await (await factory.CreateClientFor(householdA.Member).GetAsync(CropsPath, ct)).ReadJsonAsync(ct);

        Assert.Equal(["Lúa ST25"], Names(body));
        Assert.Equal(1, body.GetProperty("meta").GetProperty("totalCount").GetInt32());
    }

    [Fact(DisplayName = "BR-USR-14: danh sách của Nhân công chỉ có tên cây của mùa vụ có việc giao cho mình; lọc, sắp xếp bị bỏ qua")]
    public async Task List_Worker_SeesOnlyNamesOfAssignedCrops()
    {
        var ct = TestContext.Current.CancellationToken;
        var household = await factory.CreateHouseholdAsync(ct);
        var plot = await TestData.CreatePlotAsync(factory.Postgres, household, "TD001", ct);
        var rice = await TestData.CreateCropAsync(factory.Postgres, household, "Lúa ST25", ct);
        var corn = await TestData.CreateCropAsync(factory.Postgres, household, "Bắp", ct);
        await TestData.CreateCropAsync(factory.Postgres, household, "Cà chua", ct);
        await TestData.CreateSeasonAsync(factory.Postgres, household, plot, SeasonStatus.InProgress, ct, household.Worker, rice);
        await TestData.CreateSeasonAsync(factory.Postgres, household, plot, SeasonStatus.Planned, ct, household.Worker, corn);
        var client = factory.CreateClientFor(household.Worker);

        var body = await (await client.GetAsync(CropsPath, ct)).ReadJsonAsync(ct);

        Assert.Equal(["Bắp", "Lúa ST25"], Names(body));
        foreach (var item in body.GetProperty("data").EnumerateArray())
        {
            Assert.Equal(JsonValueKind.Null, item.GetProperty("group").ValueKind);
            Assert.Equal(JsonValueKind.Null, item.GetProperty("yieldUnit").ValueKind);
            Assert.Equal(JsonValueKind.Null, item.GetProperty("status").ValueKind);
        }

        // Cả hai cây đều Active, nhóm lúa: nếu bộ lọc có tác dụng thì kết quả sẽ rỗng.
        Assert.Equal(["Bắp", "Lúa ST25"], await NamesAsync(client, "?status=Inactive&group=Vegetable", ct));
        Assert.Equal(["Bắp", "Lúa ST25"], await NamesAsync(client, "?sort=-name", ct));
        Assert.Equal(["Lúa ST25"], await NamesAsync(client, "?search=lúa", ct));
    }

    private async Task<List<string?>> NamesAsync(HttpClient client, string queryString, CancellationToken ct)
    {
        var response = await client.GetAsync(CropsPath + queryString, ct);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return Names(await response.ReadJsonAsync(ct));
    }

    private static List<string?> Names(JsonElement body) =>
        body.GetProperty("data").EnumerateArray().Select(c => c.GetProperty("name").GetString()).ToList();
}
