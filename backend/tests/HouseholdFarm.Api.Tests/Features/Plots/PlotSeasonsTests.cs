using System.Net;
using System.Text.Json;
using HouseholdFarm.Api.Domain.Enums;
using HouseholdFarm.Api.Tests.Infrastructure;

namespace HouseholdFarm.Api.Tests.Features.Plots;

/// <summary>
/// Danh sách mùa vụ trên một thửa ở trang chi tiết thửa (FR-PLOT-04).
/// Trang dùng lại API danh sách mùa vụ của module Mùa vụ với tham số plotId.
/// </summary>
public class PlotSeasonsTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private const string SeasonsPath = "/api/seasons";

    [Fact(DisplayName = "FR-PLOT-04: Chủ hộ thấy mọi mùa vụ của thửa, không lẫn mùa vụ thửa khác, đủ trường để hiển thị")]
    public async Task PlotSeasons_Owner_SeesAllSeasonsOfPlot()
    {
        var ct = TestContext.Current.CancellationToken;
        var household = await factory.CreateHouseholdAsync(ct);
        var plot = await TestData.CreatePlotAsync(factory.Postgres, household, "TD001", ct);
        var otherPlot = await TestData.CreatePlotAsync(factory.Postgres, household, "TD002", ct);
        var crop = await TestData.CreateCropAsync(factory.Postgres, household, "Lúa ST25", ct);
        var inProgress = await TestData.CreateSeasonAsync(factory.Postgres, household, plot, SeasonStatus.InProgress, ct, crop: crop);
        var closed = await TestData.CreateSeasonAsync(factory.Postgres, household, plot, SeasonStatus.Closed, ct, crop: crop);
        await TestData.CreateSeasonAsync(factory.Postgres, household, otherPlot, SeasonStatus.InProgress, ct, crop: crop);

        var body = await GetPlotSeasonsAsync(factory.CreateClientFor(household.Owner), plot.Id, ct);

        var items = body.GetProperty("data").EnumerateArray().ToList();
        Assert.Equal(
            new[] { inProgress.Id, closed.Id }.Order(),
            items.Select(s => s.GetProperty("id").GetGuid()).Order());
        Assert.All(items, s => Assert.Equal(plot.Id, s.GetProperty("plotId").GetGuid()));

        // Khối "Mùa vụ trên thửa" cần tên cây, hai ngày, sản lượng đã thu so với mục tiêu và trạng thái.
        var item = items.First(s => s.GetProperty("id").GetGuid() == inProgress.Id);
        Assert.Equal("Lúa ST25", item.GetProperty("cropName").GetString());
        Assert.Equal(inProgress.StartDate.ToString("yyyy-MM-dd"), item.GetProperty("startDate").GetString());
        Assert.Equal(inProgress.ExpectedHarvestDate.ToString("yyyy-MM-dd"), item.GetProperty("expectedHarvestDate").GetString());
        Assert.Equal(inProgress.TargetYield, item.GetProperty("targetYield").GetDecimal());
        Assert.Equal(0m, item.GetProperty("harvestedYield").GetDecimal());
        Assert.Equal("Kilogram", item.GetProperty("yieldUnit").GetString());
        Assert.Equal("InProgress", item.GetProperty("status").GetString());
    }

    [Fact(DisplayName = "FR-PLOT-04, BR-GEN-02: Thành viên hộ xem mùa vụ của một thửa chỉ thấy mùa vụ được phân công")]
    public async Task PlotSeasons_Member_SeesOnlyAssignedSeasons()
    {
        var ct = TestContext.Current.CancellationToken;
        var household = await factory.CreateHouseholdAsync(ct);
        var plot = await TestData.CreatePlotAsync(factory.Postgres, household, "TD001", ct);
        var otherPlot = await TestData.CreatePlotAsync(factory.Postgres, household, "TD002", ct);
        var assigned = await TestData.CreateSeasonAsync(factory.Postgres, household, plot, SeasonStatus.InProgress, ct);
        await TestData.CreateSeasonAsync(factory.Postgres, household, plot, SeasonStatus.Closed, ct);
        var assignedOtherPlot = await TestData.CreateSeasonAsync(factory.Postgres, household, otherPlot, SeasonStatus.InProgress, ct);
        await TestData.AssignMemberAsync(factory.Postgres, household, assigned, household.Member, ct);
        await TestData.AssignMemberAsync(factory.Postgres, household, assignedOtherPlot, household.Member, ct);

        var body = await GetPlotSeasonsAsync(factory.CreateClientFor(household.Member), plot.Id, ct);

        var item = Assert.Single(body.GetProperty("data").EnumerateArray());
        Assert.Equal(assigned.Id, item.GetProperty("id").GetGuid());
        Assert.Equal(1, body.GetProperty("meta").GetProperty("totalCount").GetInt32());
    }

    private static async Task<JsonElement> GetPlotSeasonsAsync(HttpClient client, Guid plotId, CancellationToken ct)
    {
        var response = await client.GetAsync($"{SeasonsPath}?plotId={plotId}", ct);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await response.ReadJsonAsync(ct);
    }
}
