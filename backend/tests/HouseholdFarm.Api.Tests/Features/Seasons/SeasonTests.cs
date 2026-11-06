using System.Net;
using System.Net.Http.Json;
using HouseholdFarm.Api.Common.Errors;
using HouseholdFarm.Api.Domain.Enums;
using HouseholdFarm.Api.Features.Seasons;
using HouseholdFarm.Api.Tests.Infrastructure;

namespace HouseholdFarm.Api.Tests.Features.Seasons;

/// <summary>
/// Kiểm thử chức năng tạo mùa vụ (FR-SEA-01, BR-SEA-01, BR-GEN-02, BR-GEN-05).
/// </summary>
public class SeasonTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private const string SeasonsPath = "/api/seasons";

    [Fact(DisplayName = "FR-SEA-01: Chủ hộ tạo mùa vụ thành công ở trạng thái Lên kế hoạch (Planned), đọc lại bằng GET trả 200")]
    public async Task Create_ValidRequest_ReturnsCreatedAndBasicDetail()
    {
        var ct = TestContext.Current.CancellationToken;
        var household = await factory.CreateHouseholdAsync(ct);
        var plot = await TestData.CreatePlotAsync(factory.Postgres, household, "TD001", ct);
        var crop = await TestData.CreateCropAsync(factory.Postgres, household, "Lúa ST25", ct, yieldUnit: Unit.Kilogram);

        var client = factory.CreateClientFor(household.Owner);

        var request = new CreateSeasonRequest(
            plot.Id,
            crop.Id,
            StartDate: new DateOnly(2026, 11, 1),
            ExpectedHarvestDate: new DateOnly(2027, 2, 15),
            TargetYield: 4500.5m);

        var response = await client.PostAsJsonAsync(SeasonsPath, request, ct);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await response.ReadJsonAsync(ct);
        var data = body.GetProperty("data");

        var seasonId = data.GetProperty("id").GetGuid();
        Assert.Equal(plot.Id, data.GetProperty("plotId").GetGuid());
        Assert.Equal("TD001", data.GetProperty("plotCode").GetString());
        Assert.Equal(crop.Id, data.GetProperty("cropId").GetGuid());
        Assert.Equal("Lúa ST25", data.GetProperty("cropName").GetString());
        Assert.Equal("Kilogram", data.GetProperty("yieldUnit").GetString());
        Assert.Equal("2026-11-01", data.GetProperty("startDate").GetString());
        Assert.Equal("2027-02-15", data.GetProperty("expectedHarvestDate").GetString());
        Assert.Equal(4500.5m, data.GetProperty("targetYield").GetDecimal());
        Assert.Equal("Planned", data.GetProperty("status").GetString());
        Assert.True(data.GetProperty("version").GetUInt32() > 0);

        Assert.Equal($"{SeasonsPath}/{seasonId}", response.Headers.Location!.AbsolutePath);

        // Đọc lại bằng GET /api/seasons/{id}
        var getResponse = await client.GetAsync($"{SeasonsPath}/{seasonId}", ct);
        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);
        var getData = (await getResponse.ReadJsonAsync(ct)).GetProperty("data");
        Assert.Equal(seasonId, getData.GetProperty("id").GetGuid());
        Assert.Equal("Planned", getData.GetProperty("status").GetString());
    }

    [Fact(DisplayName = "BR-SEA-01: ngày dự kiến thu hoạch trước ngày bắt đầu trả VALIDATION_ERROR")]
    public async Task Create_ExpectedHarvestDateBeforeStartDate_ReturnsValidationError()
    {
        var ct = TestContext.Current.CancellationToken;
        var household = await factory.CreateHouseholdAsync(ct);
        var plot = await TestData.CreatePlotAsync(factory.Postgres, household, "TD001", ct);
        var crop = await TestData.CreateCropAsync(factory.Postgres, household, "Lúa ST25", ct);

        var client = factory.CreateClientFor(household.Owner);

        var request = new CreateSeasonRequest(
            plot.Id,
            crop.Id,
            StartDate: new DateOnly(2026, 11, 10),
            ExpectedHarvestDate: new DateOnly(2026, 11, 5),
            TargetYield: 3000m);

        var response = await client.PostAsJsonAsync(SeasonsPath, request, ct);
        await response.AssertProblemAsync(HttpStatusCode.BadRequest, ErrorCodes.ValidationError, ct);
    }

    [Fact(DisplayName = "BR-SEA-01: mục tiêu sản lượng bằng 0 hoặc âm trả VALIDATION_ERROR")]
    public async Task Create_TargetYieldZeroOrNegative_ReturnsValidationError()
    {
        var ct = TestContext.Current.CancellationToken;
        var household = await factory.CreateHouseholdAsync(ct);
        var plot = await TestData.CreatePlotAsync(factory.Postgres, household, "TD001", ct);
        var crop = await TestData.CreateCropAsync(factory.Postgres, household, "Lúa ST25", ct);

        var client = factory.CreateClientFor(household.Owner);

        var requestZero = new CreateSeasonRequest(
            plot.Id,
            crop.Id,
            StartDate: new DateOnly(2026, 11, 1),
            ExpectedHarvestDate: new DateOnly(2027, 2, 1),
            TargetYield: 0m);

        var responseZero = await client.PostAsJsonAsync(SeasonsPath, requestZero, ct);
        await responseZero.AssertProblemAsync(HttpStatusCode.BadRequest, ErrorCodes.ValidationError, ct);

        var requestNegative = new CreateSeasonRequest(
            plot.Id,
            crop.Id,
            StartDate: new DateOnly(2026, 11, 1),
            ExpectedHarvestDate: new DateOnly(2027, 2, 1),
            TargetYield: -100m);

        var responseNegative = await client.PostAsJsonAsync(SeasonsPath, requestNegative, ct);
        await responseNegative.AssertProblemAsync(HttpStatusCode.BadRequest, ErrorCodes.ValidationError, ct);
    }

    [Fact(DisplayName = "Ma trận phân quyền: Thành viên hộ tạo mùa vụ trả FORBIDDEN")]
    public async Task Create_ByMember_ReturnsForbidden()
    {
        var ct = TestContext.Current.CancellationToken;
        var household = await factory.CreateHouseholdAsync(ct);
        var plot = await TestData.CreatePlotAsync(factory.Postgres, household, "TD001", ct);
        var crop = await TestData.CreateCropAsync(factory.Postgres, household, "Lúa ST25", ct);

        // Thành viên hộ tạo mùa vụ
        var client = factory.CreateClientFor(household.Member);

        var request = new CreateSeasonRequest(
            plot.Id,
            crop.Id,
            StartDate: new DateOnly(2026, 11, 1),
            ExpectedHarvestDate: new DateOnly(2027, 2, 1),
            TargetYield: 2000m);

        var response = await client.PostAsJsonAsync(SeasonsPath, request, ct);
        await response.AssertProblemAsync(HttpStatusCode.Forbidden, ErrorCodes.Forbidden, ct);
    }

    [Fact(DisplayName = "BR-GEN-02: chọn thửa đất hoặc cây trồng của hộ khác trả NOT_FOUND")]
    public async Task Create_PlotOrCropFromOtherHousehold_ReturnsNotFound()
    {
        var ct = TestContext.Current.CancellationToken;
        var householdA = await factory.CreateHouseholdAsync(ct);
        var householdB = await factory.CreateHouseholdAsync(ct);

        var plotA = await TestData.CreatePlotAsync(factory.Postgres, householdA, "TD-A", ct);
        var plotB = await TestData.CreatePlotAsync(factory.Postgres, householdB, "TD-B", ct);
        var cropA = await TestData.CreateCropAsync(factory.Postgres, householdA, "Cây A", ct);
        var cropB = await TestData.CreateCropAsync(factory.Postgres, householdB, "Cây B", ct);

        var clientA = factory.CreateClientFor(householdA.Owner);

        // Thửa B của hộ khác
        var requestOtherPlot = new CreateSeasonRequest(
            plotB.Id,
            cropA.Id,
            StartDate: new DateOnly(2026, 11, 1),
            ExpectedHarvestDate: new DateOnly(2027, 2, 1),
            TargetYield: 1000m);
        var responseOtherPlot = await clientA.PostAsJsonAsync(SeasonsPath, requestOtherPlot, ct);
        await responseOtherPlot.AssertProblemAsync(HttpStatusCode.NotFound, ErrorCodes.NotFound, ct);

        // Cây B của hộ khác
        var requestOtherCrop = new CreateSeasonRequest(
            plotA.Id,
            cropB.Id,
            StartDate: new DateOnly(2026, 11, 1),
            ExpectedHarvestDate: new DateOnly(2027, 2, 1),
            TargetYield: 1000m);
        var responseOtherCrop = await clientA.PostAsJsonAsync(SeasonsPath, requestOtherCrop, ct);
        await responseOtherCrop.AssertProblemAsync(HttpStatusCode.NotFound, ErrorCodes.NotFound, ct);
    }

    [Fact(DisplayName = "BR-USR-14: Nhân công xem chi tiết mùa vụ trả FORBIDDEN")]
    public async Task Get_ByWorker_ReturnsForbidden()
    {
        var ct = TestContext.Current.CancellationToken;
        var household = await factory.CreateHouseholdAsync(ct);
        var plot = await TestData.CreatePlotAsync(factory.Postgres, household, "TD001", ct);
        var season = await TestData.CreateSeasonAsync(factory.Postgres, household, plot, SeasonStatus.Planned, ct);

        var client = factory.CreateClientFor(household.Worker);
        var response = await client.GetAsync($"{SeasonsPath}/{season.Id}", ct);
        await response.AssertProblemAsync(HttpStatusCode.Forbidden, ErrorCodes.Forbidden, ct);
    }
}

