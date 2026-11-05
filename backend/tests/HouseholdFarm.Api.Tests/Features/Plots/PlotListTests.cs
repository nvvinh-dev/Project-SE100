using System.Net;
using System.Text.Json;
using HouseholdFarm.Api.Common.Errors;
using HouseholdFarm.Api.Domain.Entities;
using HouseholdFarm.Api.Domain.Enums;
using HouseholdFarm.Api.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace HouseholdFarm.Api.Tests.Features.Plots;

/// <summary>Danh sách thửa đất: tìm kiếm, lọc, sắp xếp, phân trang (FR-PLOT-03).</summary>
public class PlotListTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private const string PlotsPath = "/api/plots";

    [Fact(DisplayName = "FR-PLOT-03: lọc theo tình trạng, loại đất, nguồn nước")]
    public async Task List_FiltersByStatusSoilTypeAndWaterSource()
    {
        var ct = TestContext.Current.CancellationToken;
        var household = await factory.CreateHouseholdAsync(ct);
        await TestData.CreatePlotAsync(factory.Postgres, household, "TD001", ct);
        var clay = await TestData.CreatePlotAsync(factory.Postgres, household, "TD002", ct);
        var inactive = await TestData.CreatePlotAsync(factory.Postgres, household, "TD003", ct);
        await SetAsync(household, clay.Id, p => p.SoilType = SoilType.Clay, ct);
        await SetAsync(household, clay.Id, p => p.WaterSource = WaterSource.Well, ct);
        await SetAsync(household, inactive.Id, p => p.Status = RecordStatus.Inactive, ct);
        var client = factory.CreateClientFor(household.Owner);

        Assert.Equal(["TD003"], await CodesAsync(client, "?status=Inactive", ct));
        Assert.Equal(["TD002"], await CodesAsync(client, "?soilType=Clay", ct));
        Assert.Equal(["TD002"], await CodesAsync(client, "?waterSource=Well", ct));
        Assert.Equal(["TD001", "TD003"], await CodesAsync(client, "?soilType=Alluvial&waterSource=RiverCanal", ct));
    }

    [Fact(DisplayName = "FR-PLOT-03: tìm theo mã và vị trí mô tả, không phân biệt hoa thường; % trong chuỗi tìm không phải ký tự đại diện")]
    public async Task List_SearchesCodeAndLocation()
    {
        var ct = TestContext.Current.CancellationToken;
        var household = await factory.CreateHouseholdAsync(ct);
        await TestData.CreatePlotAsync(factory.Postgres, household, "TD001", ct, locationDescription: "Ruộng Bờ Kênh");
        await TestData.CreatePlotAsync(factory.Postgres, household, "TD002", ct, locationDescription: "Sau nhà 50% cát");
        await TestData.CreatePlotAsync(factory.Postgres, household, "VUON-1", ct);
        var client = factory.CreateClientFor(household.Owner);

        Assert.Equal(["TD001"], await CodesAsync(client, "?search=bờ%20kênh", ct));
        Assert.Equal(["VUON-1"], await CodesAsync(client, "?search=vuon", ct));
        Assert.Equal(["TD002"], await CodesAsync(client, "?search=50%25", ct));
        Assert.Empty(await CodesAsync(client, "?search=%25%25%25", ct));
    }

    [Fact(DisplayName = "FR-PLOT-03: phân trang trả đúng meta; sắp xếp mặc định theo mã, -areaM2 là diện tích giảm dần")]
    public async Task List_PagesAndSorts()
    {
        var ct = TestContext.Current.CancellationToken;
        var household = await factory.CreateHouseholdAsync(ct);
        await TestData.CreatePlotAsync(factory.Postgres, household, "TD003", ct, areaM2: 300m);
        await TestData.CreatePlotAsync(factory.Postgres, household, "TD001", ct, areaM2: 100m);
        await TestData.CreatePlotAsync(factory.Postgres, household, "TD002", ct, areaM2: 500m);
        var client = factory.CreateClientFor(household.Owner);

        var page2 = await (await client.GetAsync($"{PlotsPath}?page=2&pageSize=2", ct)).ReadJsonAsync(ct);

        Assert.Equal(["TD003"], Codes(page2));
        var meta = page2.GetProperty("meta");
        Assert.Equal(2, meta.GetProperty("page").GetInt32());
        Assert.Equal(2, meta.GetProperty("pageSize").GetInt32());
        Assert.Equal(3, meta.GetProperty("totalCount").GetInt32());
        Assert.Equal(["TD002", "TD003", "TD001"], await CodesAsync(client, "?sort=-areaM2", ct));
    }

    [Fact(DisplayName = "Sắp xếp theo trường không hỗ trợ, trang ngoài khoảng trả VALIDATION_ERROR")]
    public async Task List_InvalidSortOrPage_ReturnsValidationError()
    {
        var ct = TestContext.Current.CancellationToken;
        var household = await factory.CreateHouseholdAsync(ct);
        var client = factory.CreateClientFor(household.Owner);

        var badSort = await (await client.GetAsync($"{PlotsPath}?sort=note", ct))
            .AssertProblemAsync(HttpStatusCode.BadRequest, ErrorCodes.ValidationError, ct);
        await (await client.GetAsync($"{PlotsPath}?page=0", ct))
            .AssertProblemAsync(HttpStatusCode.BadRequest, ErrorCodes.ValidationError, ct);

        Assert.Equal(
            "Chỉ sắp xếp được theo: code, areaM2, createdAt.",
            badSort.GetProperty("errors").GetProperty("sort")[0].GetString());
    }

    [Fact(DisplayName = "TC-10: danh sách thửa không có thửa của hộ khác")]
    public async Task TC10_List_ExcludesOtherHousehold()
    {
        var ct = TestContext.Current.CancellationToken;
        var householdA = await factory.CreateHouseholdAsync(ct);
        var householdB = await factory.CreateHouseholdAsync(ct);
        await TestData.CreatePlotAsync(factory.Postgres, householdA, "TD001", ct);
        await TestData.CreatePlotAsync(factory.Postgres, householdB, "TD001", ct);
        await TestData.CreatePlotAsync(factory.Postgres, householdB, "TD002", ct);

        var body = await (await factory.CreateClientFor(householdA.Member).GetAsync(PlotsPath, ct)).ReadJsonAsync(ct);

        Assert.Equal(["TD001"], Codes(body));
        Assert.Equal(1, body.GetProperty("meta").GetProperty("totalCount").GetInt32());
    }

    [Fact(DisplayName = "BR-USR-14: danh sách của Nhân công chỉ có thửa có việc giao cho mình, chỉ mã và vị trí")]
    public async Task List_Worker_SeesOnlyAssignedPlots()
    {
        var ct = TestContext.Current.CancellationToken;
        var household = await factory.CreateHouseholdAsync(ct);
        var assigned = await TestData.CreatePlotAsync(factory.Postgres, household, "TD001", ct, locationDescription: "Bờ kênh");
        await TestData.CreatePlotAsync(factory.Postgres, household, "TD002", ct);
        await TestData.CreateSeasonAsync(factory.Postgres, household, assigned, SeasonStatus.InProgress, ct, household.Worker);

        var body = await (await factory.CreateClientFor(household.Worker).GetAsync(PlotsPath, ct)).ReadJsonAsync(ct);

        var item = Assert.Single(body.GetProperty("data").EnumerateArray());
        Assert.Equal("TD001", item.GetProperty("code").GetString());
        Assert.Equal("Bờ kênh", item.GetProperty("locationDescription").GetString());
        Assert.Equal(JsonValueKind.Null, item.GetProperty("areaM2").ValueKind);
        Assert.Equal(JsonValueKind.Null, item.GetProperty("status").ValueKind);
    }

    [Fact(DisplayName = "BR-USR-14: Nhân công lọc theo tình trạng, loại đất, nguồn nước, sắp theo diện tích không dò ra được giá trị")]
    public async Task List_Worker_IgnoresHiddenFiltersAndSort()
    {
        var ct = TestContext.Current.CancellationToken;
        var household = await factory.CreateHouseholdAsync(ct);
        var large = await TestData.CreatePlotAsync(factory.Postgres, household, "TD001", ct, areaM2: 5000m);
        var small = await TestData.CreatePlotAsync(factory.Postgres, household, "TD002", ct, areaM2: 100m);
        await TestData.CreateSeasonAsync(factory.Postgres, household, large, SeasonStatus.InProgress, ct, household.Worker);
        await TestData.CreateSeasonAsync(factory.Postgres, household, small, SeasonStatus.InProgress, ct, household.Worker);
        var client = factory.CreateClientFor(household.Worker);

        // Cả hai thửa đều Active, phù sa, sông/kênh: nếu bộ lọc có tác dụng thì kết quả sẽ rỗng.
        Assert.Equal(["TD001", "TD002"], await CodesAsync(client, "?status=Inactive", ct));
        Assert.Equal(["TD001", "TD002"], await CodesAsync(client, "?soilType=Clay&waterSource=Well", ct));
        // Sắp theo diện tích tăng dần sẽ cho TD002 trước; Nhân công luôn được sắp theo mã.
        Assert.Equal(["TD001", "TD002"], await CodesAsync(client, "?sort=areaM2", ct));
        Assert.Equal(["TD001", "TD002"], await CodesAsync(client, "?sort=-code", ct));
        // Mã, vị trí thì Nhân công được thấy nên vẫn tìm được.
        Assert.Equal(["TD002"], await CodesAsync(client, "?search=TD002", ct));
    }

    private async Task<List<string?>> CodesAsync(HttpClient client, string queryString, CancellationToken ct)
    {
        var response = await client.GetAsync(PlotsPath + queryString, ct);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return Codes(await response.ReadJsonAsync(ct));
    }

    private static List<string?> Codes(JsonElement body) =>
        body.GetProperty("data").EnumerateArray().Select(p => p.GetProperty("code").GetString()).ToList();

    private async Task SetAsync(TestHousehold household, Guid plotId, Action<Plot> change, CancellationToken ct)
    {
        await using var db = factory.Postgres.CreateDbContext(TestCurrentUser.For(household.Owner));
        var plot = await db.Plots.SingleAsync(p => p.Id == plotId, ct);
        change(plot);
        await db.SaveChangesAsync(ct);
    }
}
