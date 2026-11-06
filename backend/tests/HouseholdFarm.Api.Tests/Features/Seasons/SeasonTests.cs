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

    [Fact(DisplayName = "BR-SEA-01, BR-GEN-13: chọn thửa đất đang ngừng sử dụng (Inactive) trả CATALOG_INACTIVE")]
    public async Task Create_PlotInactive_ReturnsCatalogInactive()
    {
        var ct = TestContext.Current.CancellationToken;
        var household = await factory.CreateHouseholdAsync(ct);
        var plot = await TestData.CreatePlotAsync(factory.Postgres, household, "TD001", ct, status: RecordStatus.Inactive);
        var crop = await TestData.CreateCropAsync(factory.Postgres, household, "Lúa ST25", ct, status: RecordStatus.Active);

        var client = factory.CreateClientFor(household.Owner);

        var request = new CreateSeasonRequest(
            plot.Id,
            crop.Id,
            StartDate: new DateOnly(2026, 11, 1),
            ExpectedHarvestDate: new DateOnly(2027, 2, 1),
            TargetYield: 3000m);

        var response = await client.PostAsJsonAsync(SeasonsPath, request, ct);
        var body = await response.AssertProblemAsync(HttpStatusCode.UnprocessableEntity, ErrorCodes.CatalogInactive, ct);
        Assert.Contains("TD001", body.GetProperty("detail").GetString());
    }

    [Fact(DisplayName = "BR-SEA-01, BR-GEN-13: chọn cây trồng đang ngừng sử dụng (Inactive) trả CATALOG_INACTIVE")]
    public async Task Create_CropInactive_ReturnsCatalogInactive()
    {
        var ct = TestContext.Current.CancellationToken;
        var household = await factory.CreateHouseholdAsync(ct);
        var plot = await TestData.CreatePlotAsync(factory.Postgres, household, "TD001", ct, status: RecordStatus.Active);
        var crop = await TestData.CreateCropAsync(factory.Postgres, household, "Lúa ST25", ct, status: RecordStatus.Inactive);

        var client = factory.CreateClientFor(household.Owner);

        var request = new CreateSeasonRequest(
            plot.Id,
            crop.Id,
            StartDate: new DateOnly(2026, 11, 1),
            ExpectedHarvestDate: new DateOnly(2027, 2, 1),
            TargetYield: 3000m);

        var response = await client.PostAsJsonAsync(SeasonsPath, request, ct);
        var body = await response.AssertProblemAsync(HttpStatusCode.UnprocessableEntity, ErrorCodes.CatalogInactive, ct);
        Assert.Contains("Lúa ST25", body.GetProperty("detail").GetString());
    }

    [Fact(DisplayName = "TC-01 (BR-SEA-02, BR-SEA-04): tạo mùa vụ trùng khoảng ngày bị SEASON_OVERLAP; bật AllowOverlappingSeasons thì tạo thành công")]
    public async Task Create_OverlapWithExistingSeason_TC01()
    {
        var ct = TestContext.Current.CancellationToken;
        var household = await factory.CreateHouseholdAsync(ct);
        var plot = await TestData.CreatePlotAsync(factory.Postgres, household, "TD001", ct);
        var crop = await TestData.CreateCropAsync(factory.Postgres, household, "Lúa ST25", ct);

        var client = factory.CreateClientFor(household.Owner);

        // Mùa vụ 1: 01/01/2026 – 31/03/2026
        var request1 = new CreateSeasonRequest(
            plot.Id,
            crop.Id,
            StartDate: new DateOnly(2026, 1, 1),
            ExpectedHarvestDate: new DateOnly(2026, 3, 31),
            TargetYield: 4000m);
        var response1 = await client.PostAsJsonAsync(SeasonsPath, request1, ct);
        Assert.Equal(HttpStatusCode.Created, response1.StatusCode);

        // Mùa vụ 2: 15/03/2026 – 15/06/2026 (trùng từ 15/03 đến 31/03)
        var request2 = new CreateSeasonRequest(
            plot.Id,
            crop.Id,
            StartDate: new DateOnly(2026, 3, 15),
            ExpectedHarvestDate: new DateOnly(2026, 6, 15),
            TargetYield: 3500m);
        var response2 = await client.PostAsJsonAsync(SeasonsPath, request2, ct);
        var bodyOverlap = await response2.AssertProblemAsync(HttpStatusCode.UnprocessableEntity, ErrorCodes.SeasonOverlap, ct);
        Assert.Contains("TD001", bodyOverlap.GetProperty("detail").GetString());

        // Bật cài đặt AllowOverlappingSeasons cho hộ (BR-SEA-04)
        await TestData.SetAllowOverlappingSeasonsAsync(factory.Postgres, household, allow: true, ct);

        // Gửi lại yêu cầu tạo mùa vụ 2: thành công
        var responseRetry = await client.PostAsJsonAsync(SeasonsPath, request2, ct);
        Assert.Equal(HttpStatusCode.Created, responseRetry.StatusCode);
    }

    [Fact(DisplayName = "BR-SEA-02: trùng đúng một ngày biên (ngày kết thúc mùa trước = ngày bắt đầu mùa sau) trả SEASON_OVERLAP")]
    public async Task Create_SingleBoundaryDayOverlap_ReturnsSeasonOverlap()
    {
        var ct = TestContext.Current.CancellationToken;
        var household = await factory.CreateHouseholdAsync(ct);
        var plot = await TestData.CreatePlotAsync(factory.Postgres, household, "TD001", ct);
        var crop = await TestData.CreateCropAsync(factory.Postgres, household, "Lúa ST25", ct);

        var client = factory.CreateClientFor(household.Owner);

        // Mùa vụ 1: 01/01/2026 – 31/03/2026
        var request1 = new CreateSeasonRequest(
            plot.Id,
            crop.Id,
            StartDate: new DateOnly(2026, 1, 1),
            ExpectedHarvestDate: new DateOnly(2026, 3, 31),
            TargetYield: 4000m);
        await client.PostAsJsonAsync(SeasonsPath, request1, ct);

        // Mùa vụ 2: bắt đầu đúng ngày 31/03/2026
        var request2 = new CreateSeasonRequest(
            plot.Id,
            crop.Id,
            StartDate: new DateOnly(2026, 3, 31),
            ExpectedHarvestDate: new DateOnly(2026, 6, 30),
            TargetYield: 3500m);
        var response2 = await client.PostAsJsonAsync(SeasonsPath, request2, ct);
        await response2.AssertProblemAsync(HttpStatusCode.UnprocessableEntity, ErrorCodes.SeasonOverlap, ct);
    }

    [Fact(DisplayName = "BR-SEA-02: hai khoảng thời gian liền kề không giao nhau thì tạo thành công")]
    public async Task Create_AdjacentRangesNonOverlapping_ReturnsCreated()
    {
        var ct = TestContext.Current.CancellationToken;
        var household = await factory.CreateHouseholdAsync(ct);
        var plot = await TestData.CreatePlotAsync(factory.Postgres, household, "TD001", ct);
        var crop = await TestData.CreateCropAsync(factory.Postgres, household, "Lúa ST25", ct);

        var client = factory.CreateClientFor(household.Owner);

        // Mùa vụ 1: 01/01/2026 – 31/03/2026
        var request1 = new CreateSeasonRequest(
            plot.Id,
            crop.Id,
            StartDate: new DateOnly(2026, 1, 1),
            ExpectedHarvestDate: new DateOnly(2026, 3, 31),
            TargetYield: 4000m);
        await client.PostAsJsonAsync(SeasonsPath, request1, ct);

        // Mùa vụ 2: bắt đầu ngay ngày hôm sau 01/04/2026
        var request2 = new CreateSeasonRequest(
            plot.Id,
            crop.Id,
            StartDate: new DateOnly(2026, 4, 1),
            ExpectedHarvestDate: new DateOnly(2026, 6, 30),
            TargetYield: 3500m);
        var response2 = await client.PostAsJsonAsync(SeasonsPath, request2, ct);
        Assert.Equal(HttpStatusCode.Created, response2.StatusCode);
    }

    [Fact(DisplayName = "BR-SEA-02: mùa vụ đã hủy (Cancelled) không gây chồng lấn với mùa vụ mới")]
    public async Task Create_CancelledSeasonDoesNotCauseOverlap()
    {
        var ct = TestContext.Current.CancellationToken;
        var household = await factory.CreateHouseholdAsync(ct);
        var plot = await TestData.CreatePlotAsync(factory.Postgres, household, "TD001", ct);
        var crop = await TestData.CreateCropAsync(factory.Postgres, household, "Lúa ST25", ct);

        // Mùa vụ cũ đã hủy
        await TestData.CreateSeasonAsync(factory.Postgres, household, plot, SeasonStatus.Cancelled, ct);

        var client = factory.CreateClientFor(household.Owner);

        // Mùa vụ mới cùng thửa và trùng thời gian với mùa vụ đã hủy
        var request = new CreateSeasonRequest(
            plot.Id,
            crop.Id,
            StartDate: new DateOnly(2026, 10, 1),
            ExpectedHarvestDate: new DateOnly(2027, 1, 15),
            TargetYield: 5000m);

        var response = await client.PostAsJsonAsync(SeasonsPath, request, ct);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    [Fact(DisplayName = "BR-SEA-03: Lớp 2 - thửa có mùa vụ đang canh tác (InProgress) trả SEASON_PLOT_BUSY, bật AllowOverlappingSeasons thì bỏ qua")]
    public async Task EnsurePlotNotBusy_Layer2Validation()
    {
        var ct = TestContext.Current.CancellationToken;
        var household = await TestData.CreateHouseholdAsync(postgres: factory.Postgres, ct);
        var plot = await TestData.CreatePlotAsync(factory.Postgres, household, "TD001", ct);
        await TestData.CreateSeasonAsync(factory.Postgres, household, plot, SeasonStatus.InProgress, ct);

        await using var db = factory.Postgres.CreateDbContext(TestCurrentUser.For(household.Owner));
        var seasonService = new SeasonService(db, TestCurrentUser.For(household.Owner));

        // Chưa bật AllowOverlappingSeasons: ném SEASON_PLOT_BUSY
        var ex = await Assert.ThrowsAsync<AppException>(
            () => seasonService.EnsurePlotNotBusyAsync(plot.Id, excludeSeasonId: null, ct));
        Assert.Equal(ErrorCodes.SeasonPlotBusy, ex.Code);
        Assert.Contains("đang canh tác", ex.Message);

        // Bật AllowOverlappingSeasons: bỏ qua kiểm tra
        await TestData.SetAllowOverlappingSeasonsAsync(factory.Postgres, household, allow: true, ct);
        await seasonService.EnsurePlotNotBusyAsync(plot.Id, excludeSeasonId: null, ct);
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

