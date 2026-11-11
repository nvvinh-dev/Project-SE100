using System.Net;
using System.Net.Http.Json;
using HouseholdFarm.Api.Common.Auditing;
using HouseholdFarm.Api.Common.Errors;
using HouseholdFarm.Api.Domain.Enums;
using HouseholdFarm.Api.Features.Harvests;
using HouseholdFarm.Api.Features.Seasons;
using HouseholdFarm.Api.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;

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

    [Fact(DisplayName = "FR-SEA-07: sửa mùa vụ Planned thành công mọi trường, version cập nhật")]
    public async Task Update_PlannedSeason_AllFields_ReturnsOk()
    {
        var ct = TestContext.Current.CancellationToken;
        var household = await factory.CreateHouseholdAsync(ct);
        var plot1 = await TestData.CreatePlotAsync(factory.Postgres, household, "TD001", ct);
        var plot2 = await TestData.CreatePlotAsync(factory.Postgres, household, "TD002", ct);
        var crop1 = await TestData.CreateCropAsync(factory.Postgres, household, "Lúa ST25", ct);
        var crop2 = await TestData.CreateCropAsync(factory.Postgres, household, "Ngô nếp", ct, yieldUnit: Unit.Quintal);

        var client = factory.CreateClientFor(household.Owner);

        // Lấy version ban đầu của cả hai thửa và hai cây
        uint plot1VersionBefore, plot2VersionBefore, crop1VersionBefore, crop2VersionBefore;
        await using (var dbCheck = factory.Postgres.CreateDbContext(TestCurrentUser.For(household.Owner)))
        {
            plot1VersionBefore = await dbCheck.Plots.Where(p => p.Id == plot1.Id).Select(p => p.Version).FirstAsync(ct);
            plot2VersionBefore = await dbCheck.Plots.Where(p => p.Id == plot2.Id).Select(p => p.Version).FirstAsync(ct);
            crop1VersionBefore = await dbCheck.Crops.Where(c => c.Id == crop1.Id).Select(c => c.Version).FirstAsync(ct);
            crop2VersionBefore = await dbCheck.Crops.Where(c => c.Id == crop2.Id).Select(c => c.Version).FirstAsync(ct);
        }

        // Tạo mùa vụ Planned
        var createRequest = new CreateSeasonRequest(
            plot1.Id,
            crop1.Id,
            StartDate: new DateOnly(2026, 11, 1),
            ExpectedHarvestDate: new DateOnly(2027, 2, 1),
            TargetYield: 3000m);
        var createResponse = await client.PostAsJsonAsync(SeasonsPath, createRequest, ct);
        var created = (await createResponse.ReadJsonAsync(ct)).GetProperty("data");
        var seasonId = created.GetProperty("id").GetGuid();
        var initialVersion = created.GetProperty("version").GetUInt32();

        // Sửa sang thửa 2, cây 2, ngày mới, sản lượng mới
        var updateRequest = new UpdateSeasonRequest(
            plot2.Id,
            crop2.Id,
            StartDate: new DateOnly(2026, 12, 1),
            ExpectedHarvestDate: new DateOnly(2027, 3, 1),
            TargetYield: 5000m,
            Version: initialVersion);

        var updateResponse = await client.PutAsJsonAsync($"{SeasonsPath}/{seasonId}", updateRequest, ct);
        Assert.Equal(HttpStatusCode.OK, updateResponse.StatusCode);

        var updated = (await updateResponse.ReadJsonAsync(ct)).GetProperty("data");
        Assert.Equal(plot2.Id, updated.GetProperty("plotId").GetGuid());
        Assert.Equal("TD002", updated.GetProperty("plotCode").GetString());
        Assert.Equal(crop2.Id, updated.GetProperty("cropId").GetGuid());
        Assert.Equal("Ngô nếp", updated.GetProperty("cropName").GetString());
        Assert.Equal("Quintal", updated.GetProperty("yieldUnit").GetString());
        Assert.Equal("2026-12-01", updated.GetProperty("startDate").GetString());
        Assert.Equal("2027-03-01", updated.GetProperty("expectedHarvestDate").GetString());
        Assert.Equal(5000m, updated.GetProperty("targetYield").GetDecimal());
        Assert.True(updated.GetProperty("version").GetUInt32() > initialVersion);

        // Kiểm tra version của cả hai thửa và hai cây không bị thay đổi khi đổi thửa, cây của mùa vụ
        await using (var dbCheck = factory.Postgres.CreateDbContext(TestCurrentUser.For(household.Owner)))
        {
            var plot1VersionAfter = await dbCheck.Plots.Where(p => p.Id == plot1.Id).Select(p => p.Version).FirstAsync(ct);
            var plot2VersionAfter = await dbCheck.Plots.Where(p => p.Id == plot2.Id).Select(p => p.Version).FirstAsync(ct);
            var crop1VersionAfter = await dbCheck.Crops.Where(c => c.Id == crop1.Id).Select(c => c.Version).FirstAsync(ct);
            var crop2VersionAfter = await dbCheck.Crops.Where(c => c.Id == crop2.Id).Select(c => c.Version).FirstAsync(ct);

            Assert.Equal(plot1VersionBefore, plot1VersionAfter);
            Assert.Equal(plot2VersionBefore, plot2VersionAfter);
            Assert.Equal(crop1VersionBefore, crop1VersionAfter);
            Assert.Equal(crop2VersionBefore, crop2VersionAfter);
        }
    }

    [Fact(DisplayName = "FR-SEA-07, BR-SEA-10: mùa vụ InProgress đổi ngày và sản lượng thành công; đổi thửa hoặc cây trả SEASON_INVALID_STATE")]
    public async Task Update_InProgressSeason_StatusRestrictions()
    {
        var ct = TestContext.Current.CancellationToken;
        var household = await factory.CreateHouseholdAsync(ct);
        var plot1 = await TestData.CreatePlotAsync(factory.Postgres, household, "TD001", ct);
        var plot2 = await TestData.CreatePlotAsync(factory.Postgres, household, "TD002", ct);
        var crop1 = await TestData.CreateCropAsync(factory.Postgres, household, "Lúa ST25", ct);
        var crop2 = await TestData.CreateCropAsync(factory.Postgres, household, "Ngô nếp", ct);

        // Mùa vụ đang canh tác (InProgress)
        var season = await TestData.CreateSeasonAsync(factory.Postgres, household, plot1, SeasonStatus.InProgress, ct);

        var client = factory.CreateClientFor(household.Owner);
        var getResponse = await client.GetAsync($"{SeasonsPath}/{season.Id}", ct);
        var currentData = (await getResponse.ReadJsonAsync(ct)).GetProperty("data");
        var version = currentData.GetProperty("version").GetUInt32();

        // 1. Đổi thửa khi InProgress -> SEASON_INVALID_STATE
        var updatePlotRequest = new UpdateSeasonRequest(
            plot2.Id,
            season.CropId,
            season.StartDate,
            season.ExpectedHarvestDate,
            season.TargetYield,
            version);
        var plotResponse = await client.PutAsJsonAsync($"{SeasonsPath}/{season.Id}", updatePlotRequest, ct);
        await plotResponse.AssertProblemAsync(HttpStatusCode.UnprocessableEntity, ErrorCodes.SeasonInvalidState, ct);

        // 2. Đổi cây khi InProgress -> SEASON_INVALID_STATE
        var updateCropRequest = new UpdateSeasonRequest(
            season.PlotId,
            crop2.Id,
            season.StartDate,
            season.ExpectedHarvestDate,
            season.TargetYield,
            version);
        var cropResponse = await client.PutAsJsonAsync($"{SeasonsPath}/{season.Id}", updateCropRequest, ct);
        await cropResponse.AssertProblemAsync(HttpStatusCode.UnprocessableEntity, ErrorCodes.SeasonInvalidState, ct);

        // 3. Đổi ngày và mục tiêu sản lượng khi InProgress -> thành công
        var validUpdateRequest = new UpdateSeasonRequest(
            season.PlotId,
            season.CropId,
            StartDate: season.StartDate,
            ExpectedHarvestDate: season.ExpectedHarvestDate.AddMonths(1),
            TargetYield: 6000m,
            version);
        var validResponse = await client.PutAsJsonAsync($"{SeasonsPath}/{season.Id}", validUpdateRequest, ct);
        Assert.Equal(HttpStatusCode.OK, validResponse.StatusCode);
    }

    [Fact(DisplayName = "FR-SEA-07, BR-SEA-10: sửa mùa vụ đã Closed hoặc Cancelled trả SEASON_INVALID_STATE")]
    public async Task Update_ClosedOrCancelledSeason_ThrowsSeasonInvalidState()
    {
        var ct = TestContext.Current.CancellationToken;
        var household = await factory.CreateHouseholdAsync(ct);
        var plot = await TestData.CreatePlotAsync(factory.Postgres, household, "TD001", ct);
        var closedSeason = await TestData.CreateSeasonAsync(factory.Postgres, household, plot, SeasonStatus.Closed, ct);
        var cancelledSeason = await TestData.CreateSeasonAsync(factory.Postgres, household, plot, SeasonStatus.Cancelled, ct);

        var client = factory.CreateClientFor(household.Owner);

        // Đọc version của closedSeason
        var closedData = (await (await client.GetAsync($"{SeasonsPath}/{closedSeason.Id}", ct)).ReadJsonAsync(ct)).GetProperty("data");
        var closedVersion = closedData.GetProperty("version").GetUInt32();

        var updateClosed = new UpdateSeasonRequest(
            closedSeason.PlotId,
            closedSeason.CropId,
            closedSeason.StartDate,
            closedSeason.ExpectedHarvestDate,
            closedSeason.TargetYield + 100m,
            closedVersion);
        var closedResponse = await client.PutAsJsonAsync($"{SeasonsPath}/{closedSeason.Id}", updateClosed, ct);
        await closedResponse.AssertProblemAsync(HttpStatusCode.UnprocessableEntity, ErrorCodes.SeasonInvalidState, ct);

        // Đọc version của cancelledSeason
        var cancelledData = (await (await client.GetAsync($"{SeasonsPath}/{cancelledSeason.Id}", ct)).ReadJsonAsync(ct)).GetProperty("data");
        var cancelledVersion = cancelledData.GetProperty("version").GetUInt32();

        var updateCancelled = new UpdateSeasonRequest(
            cancelledSeason.PlotId,
            cancelledSeason.CropId,
            cancelledSeason.StartDate,
            cancelledSeason.ExpectedHarvestDate,
            cancelledSeason.TargetYield + 100m,
            cancelledVersion);
        var cancelledResponse = await client.PutAsJsonAsync($"{SeasonsPath}/{cancelledSeason.Id}", updateCancelled, ct);
        await cancelledResponse.AssertProblemAsync(HttpStatusCode.UnprocessableEntity, ErrorCodes.SeasonInvalidState, ct);
    }

    [Fact(DisplayName = "FR-SEA-07, BR-SEA-02: sửa ngày mùa vụ gây trùng với mùa vụ khác cùng thửa trả SEASON_OVERLAP")]
    public async Task Update_DatesOverlapWithOtherSeason_ThrowsSeasonOverlap()
    {
        var ct = TestContext.Current.CancellationToken;
        var household = await factory.CreateHouseholdAsync(ct);
        var plot = await TestData.CreatePlotAsync(factory.Postgres, household, "TD001", ct);
        var crop = await TestData.CreateCropAsync(factory.Postgres, household, "Lúa ST25", ct);

        var client = factory.CreateClientFor(household.Owner);

        // Mùa vụ 1: 01/01/2026 – 31/03/2026
        var season1 = await client.PostAsJsonAsync(SeasonsPath, new CreateSeasonRequest(
            plot.Id, crop.Id, new DateOnly(2026, 1, 1), new DateOnly(2026, 3, 31), 4000m), ct);
        var season1Id = (await season1.ReadJsonAsync(ct)).GetProperty("data").GetProperty("id").GetGuid();

        // Mùa vụ 2: 01/04/2026 – 30/06/2026
        var season2 = await client.PostAsJsonAsync(SeasonsPath, new CreateSeasonRequest(
            plot.Id, crop.Id, new DateOnly(2026, 4, 1), new DateOnly(2026, 6, 30), 4000m), ct);
        var season2Data = (await season2.ReadJsonAsync(ct)).GetProperty("data");
        var season2Id = season2Data.GetProperty("id").GetGuid();
        var season2Version = season2Data.GetProperty("version").GetUInt32();

        // Sửa mùa vụ 2: lùi ngày bắt đầu về 20/03/2026 (trùng với mùa 1) -> SEASON_OVERLAP
        var overlapRequest = new UpdateSeasonRequest(
            plot.Id, crop.Id, new DateOnly(2026, 3, 20), new DateOnly(2026, 6, 30), 4000m, season2Version);
        var overlapResponse = await client.PutAsJsonAsync($"{SeasonsPath}/{season2Id}", overlapRequest, ct);
        await overlapResponse.AssertProblemAsync(HttpStatusCode.UnprocessableEntity, ErrorCodes.SeasonOverlap, ct);

        // Sửa mùa vụ 2: chỉ thay đổi trong khoảng của chính nó (05/04/2026 - 25/06/2026) -> thành công
        var validRequest = new UpdateSeasonRequest(
            plot.Id, crop.Id, new DateOnly(2026, 4, 5), new DateOnly(2026, 6, 25), 4500m, season2Version);
        var validResponse = await client.PutAsJsonAsync($"{SeasonsPath}/{season2Id}", validRequest, ct);
        Assert.Equal(HttpStatusCode.OK, validResponse.StatusCode);
    }

    [Fact(DisplayName = "FR-SEA-07, BR-SEA-02: sửa mùa vụ Planned chỉ đổi sang thửa khác trùng ngày với mùa vụ trên thửa đó trả SEASON_OVERLAP")]
    public async Task Update_ChangePlotOnly_OverlapWithExistingSeason_ThrowsSeasonOverlap()
    {
        var ct = TestContext.Current.CancellationToken;
        var household = await factory.CreateHouseholdAsync(ct);
        var plot1 = await TestData.CreatePlotAsync(factory.Postgres, household, "TD001", ct);
        var plot2 = await TestData.CreatePlotAsync(factory.Postgres, household, "TD002", ct);
        var crop = await TestData.CreateCropAsync(factory.Postgres, household, "Lúa ST25", ct);

        var client = factory.CreateClientFor(household.Owner);

        // Mùa vụ trên thửa 2: 01/01/2026 – 31/03/2026
        await client.PostAsJsonAsync(SeasonsPath, new CreateSeasonRequest(
            plot2.Id, crop.Id, new DateOnly(2026, 1, 1), new DateOnly(2026, 3, 31), 4000m), ct);

        // Mùa vụ trên thửa 1: cùng ngày 01/01/2026 – 31/03/2026
        var res1 = await client.PostAsJsonAsync(SeasonsPath, new CreateSeasonRequest(
            plot1.Id, crop.Id, new DateOnly(2026, 1, 1), new DateOnly(2026, 3, 31), 3000m), ct);
        var s1Data = (await res1.ReadJsonAsync(ct)).GetProperty("data");
        var s1Id = s1Data.GetProperty("id").GetGuid();
        var s1Version = s1Data.GetProperty("version").GetUInt32();

        // Sửa mùa vụ 1: GIỮ NGUYÊN NGÀY nhưng đổi PlotId sang thửa 2 (đang có mùa vụ khác trùng ngày) -> SEASON_OVERLAP
        var changePlotRequest = new UpdateSeasonRequest(
            plot2.Id, crop.Id, new DateOnly(2026, 1, 1), new DateOnly(2026, 3, 31), 3000m, s1Version);
        var changePlotResponse = await client.PutAsJsonAsync($"{SeasonsPath}/{s1Id}", changePlotRequest, ct);
        await changePlotResponse.AssertProblemAsync(HttpStatusCode.UnprocessableEntity, ErrorCodes.SeasonOverlap, ct);
    }

    [Fact(DisplayName = "BR-GEN-11: sửa mùa vụ với version cũ trả CONCURRENCY_CONFLICT")]
    public async Task Update_StaleVersion_ReturnsConcurrencyConflict()
    {
        var ct = TestContext.Current.CancellationToken;
        var household = await factory.CreateHouseholdAsync(ct);
        var plot = await TestData.CreatePlotAsync(factory.Postgres, household, "TD001", ct);
        var crop = await TestData.CreateCropAsync(factory.Postgres, household, "Lúa ST25", ct);

        var client = factory.CreateClientFor(household.Owner);

        var create = await client.PostAsJsonAsync(SeasonsPath, new CreateSeasonRequest(
            plot.Id, crop.Id, new DateOnly(2026, 11, 1), new DateOnly(2027, 2, 1), 3000m), ct);
        var created = (await create.ReadJsonAsync(ct)).GetProperty("data");
        var seasonId = created.GetProperty("id").GetGuid();
        var version = created.GetProperty("version").GetUInt32();

        // Thiết bị 1 sửa thành công -> version trong DB đã tăng
        var update1 = new UpdateSeasonRequest(
            plot.Id, crop.Id, new DateOnly(2026, 11, 1), new DateOnly(2027, 2, 15), 3200m, version);
        var res1 = await client.PutAsJsonAsync($"{SeasonsPath}/{seasonId}", update1, ct);
        Assert.Equal(HttpStatusCode.OK, res1.StatusCode);

        // Thiết bị 2 gửi sửa với version cũ ban đầu -> CONCURRENCY_CONFLICT
        var update2 = new UpdateSeasonRequest(
            plot.Id, crop.Id, new DateOnly(2026, 11, 1), new DateOnly(2027, 2, 20), 3500m, version);
        var res2 = await client.PutAsJsonAsync($"{SeasonsPath}/{seasonId}", update2, ct);
        await res2.AssertProblemAsync(HttpStatusCode.Conflict, ErrorCodes.ConcurrencyConflict, ct);
    }

    [Fact(DisplayName = "TC-01 (BR-SEA-02, BR-SEA-04): tạo mùa vụ trùng khoảng ngày bị SEASON_OVERLAP; bật AllowOverlappingSeasons thì tạo thành công")]
    public async Task TC01_Create_OverlapWithExistingSeason_ReturnsSeasonOverlap_OrSucceedsWhenAllowed()
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
        var user = TestCurrentUser.For(household.Owner);
        var clock = TestClock.At(DateTimeOffset.UtcNow);
        var audit = new DummyAuditLogger();
        var produceStock = new ProduceStockService();
        var seasonService = new SeasonService(db, user, clock, audit, produceStock);

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

    private sealed class DummyAuditLogger : IAuditLogger
    {
        public void Log(string action, Guid? entityId, object? oldValues, object? newValues, string? reason) { }
    }
}

