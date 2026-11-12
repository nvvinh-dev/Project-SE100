using System.Net;
using HouseholdFarm.Api.Common.Errors;
using HouseholdFarm.Api.Domain.Enums;
using HouseholdFarm.Api.Features.Seasons;
using HouseholdFarm.Api.Tests.Infrastructure;

namespace HouseholdFarm.Api.Tests.Features.Seasons;

/// <summary>
/// Kiểm thử danh sách mùa vụ, bộ lọc và API ô chọn mùa vụ dùng chung (FR-SEA-11, BR-USR-13, BR-SEA-10).
/// </summary>
public class SeasonListTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private const string SeasonsPath = "/api/seasons";
    private const string OptionsPath = "/api/seasons/options";

    [Fact(DisplayName = "FR-SEA-11: lọc mùa vụ theo thửa, cây, trạng thái, khoảng ngày và phân trang")]
    public async Task GetList_FiltersAndPaging_ReturnsMatchingSeasons()
    {
        var ct = TestContext.Current.CancellationToken;
        var household = await factory.CreateHouseholdAsync(ct);
        var plot1 = await TestData.CreatePlotAsync(factory.Postgres, household, "TD001", ct);
        var plot2 = await TestData.CreatePlotAsync(factory.Postgres, household, "TD002", ct);
        var crop1 = await TestData.CreateCropAsync(factory.Postgres, household, "Lúa ST25", ct);
        var crop2 = await TestData.CreateCropAsync(factory.Postgres, household, "Ngô nếp", ct);

        // Tạo 3 mùa vụ với thông tin khác nhau
        var s1 = await TestData.CreateSeasonAsync(factory.Postgres, household, plot1, SeasonStatus.Planned, ct, crop: crop1); // Lúa ST25 trên TD001
        var s2 = await TestData.CreateSeasonAsync(factory.Postgres, household, plot2, SeasonStatus.InProgress, ct, crop: crop1); // Lúa ST25 trên TD002

        await using (var db = factory.Postgres.CreateDbContext(TestCurrentUser.For(household.Owner)))
        {
            db.Seasons.Add(new HouseholdFarm.Api.Domain.Entities.Season
            {
                PlotId = plot1.Id,
                CropId = crop2.Id,
                StartDate = new DateOnly(2027, 3, 1),
                ExpectedHarvestDate = new DateOnly(2027, 6, 30),
                TargetYield = 2000m,
                Status = SeasonStatus.Planned,
            });
            await db.SaveChangesAsync(ct);
        }

        var client = factory.CreateClientFor(household.Owner);

        // 1. Lọc theo PlotId = plot1.Id -> ra 2 mùa vụ
        var resPlot = await client.GetAsync($"{SeasonsPath}?plotId={plot1.Id}", ct);
        Assert.Equal(HttpStatusCode.OK, resPlot.StatusCode);
        var bodyPlot = await resPlot.ReadJsonAsync(ct);
        Assert.Equal(2, bodyPlot.GetProperty("data").GetArrayLength());

        // 2. Lọc theo Status = InProgress -> ra 1 mùa vụ (s2)
        var resStatus = await client.GetAsync($"{SeasonsPath}?status=InProgress", ct);
        Assert.Equal(HttpStatusCode.OK, resStatus.StatusCode);
        var bodyStatus = await resStatus.ReadJsonAsync(ct);
        var itemStatus = Assert.Single(bodyStatus.GetProperty("data").EnumerateArray());
        Assert.Equal(s2.Id, itemStatus.GetProperty("id").GetGuid());

        // 3. Lọc theo khoảng ngày [from, to] giao với [startDate, expectedHarvestDate]
        var resDates = await client.GetAsync($"{SeasonsPath}?from=2027-04-01&to=2027-05-01", ct);
        Assert.Equal(HttpStatusCode.OK, resDates.StatusCode);
        var bodyDates = await resDates.ReadJsonAsync(ct);
        Assert.Equal(1, bodyDates.GetProperty("data").GetArrayLength());

        // 4. Lọc theo CropId = crop1.Id -> ra 2 mùa vụ trồng Lúa ST25 (s1, s2)
        var resCrop = await client.GetAsync($"{SeasonsPath}?cropId={crop1.Id}", ct);
        Assert.Equal(HttpStatusCode.OK, resCrop.StatusCode);
        var bodyCrop = await resCrop.ReadJsonAsync(ct);
        Assert.Equal(2, bodyCrop.GetProperty("data").GetArrayLength());

        // 5. Tìm kiếm theo search (mã thửa hoặc tên cây)
        var resSearchPlot = await client.GetAsync($"{SeasonsPath}?search=TD002", ct);
        Assert.Equal(HttpStatusCode.OK, resSearchPlot.StatusCode);
        var bodySearchPlot = await resSearchPlot.ReadJsonAsync(ct);
        Assert.Equal(1, bodySearchPlot.GetProperty("data").GetArrayLength());

        var resSearchCrop = await client.GetAsync($"{SeasonsPath}?search=Ngô", ct);
        Assert.Equal(HttpStatusCode.OK, resSearchCrop.StatusCode);
        var bodySearchCrop = await resSearchCrop.ReadJsonAsync(ct);
        Assert.Equal(1, bodySearchCrop.GetProperty("data").GetArrayLength());

        // 6. Phân trang: pageSize = 1 -> trang 1 trả 1 phần tử, meta.totalCount = 3
        var resPage = await client.GetAsync($"{SeasonsPath}?page=1&pageSize=1", ct);
        Assert.Equal(HttpStatusCode.OK, resPage.StatusCode);
        var bodyPage = await resPage.ReadJsonAsync(ct);
        Assert.Equal(1, bodyPage.GetProperty("data").GetArrayLength());
        Assert.Equal(3, bodyPage.GetProperty("meta").GetProperty("totalCount").GetInt32());
    }

    [Fact(DisplayName = "FR-SEA-11, BR-GEN-02: Thành viên chỉ thấy mùa vụ được phân công trong danh sách")]
    public async Task GetList_Member_SeesOnlyAssignedSeasons()
    {
        var ct = TestContext.Current.CancellationToken;
        var household = await factory.CreateHouseholdAsync(ct);
        var plot = await TestData.CreatePlotAsync(factory.Postgres, household, "TD001", ct);
        var s1 = await TestData.CreateSeasonAsync(factory.Postgres, household, plot, SeasonStatus.InProgress, ct);
        var s2 = await TestData.CreateSeasonAsync(factory.Postgres, household, plot, SeasonStatus.Planned, ct);

        // Phân công Member vào mùa vụ s1, không phân công vào s2
        await TestData.AssignMemberAsync(factory.Postgres, household, s1, household.Member, ct);

        var memberClient = factory.CreateClientFor(household.Member);
        var response = await memberClient.GetAsync(SeasonsPath, ct);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.ReadJsonAsync(ct);
        var items = body.GetProperty("data").EnumerateArray().ToList();
        var item = Assert.Single(items);
        Assert.Equal(s1.Id, item.GetProperty("id").GetGuid());
    }

    [Fact(DisplayName = "Ma trận phân quyền: Nhân công xem danh sách mùa vụ trả FORBIDDEN")]
    public async Task GetList_Worker_ReturnsForbidden()
    {
        var ct = TestContext.Current.CancellationToken;
        var household = await factory.CreateHouseholdAsync(ct);

        var workerClient = factory.CreateClientFor(household.Worker);
        var response = await workerClient.GetAsync(SeasonsPath, ct);
        await response.AssertProblemAsync(HttpStatusCode.Forbidden, ErrorCodes.Forbidden, ct);
    }

    [Fact(DisplayName = "FR-SEA-11, BR-SEA-10: ô chọn mùa vụ trả đúng các trạng thái được phép theo loại thao tác")]
    public async Task GetOptions_ReturnsAllowedStatusesForOperation()
    {
        var ct = TestContext.Current.CancellationToken;
        var household = await factory.CreateHouseholdAsync(ct);
        var plot = await TestData.CreatePlotAsync(factory.Postgres, household, "TD001", ct);

        var planned = await TestData.CreateSeasonAsync(factory.Postgres, household, plot, SeasonStatus.Planned, ct);
        var inProgress = await TestData.CreateSeasonAsync(factory.Postgres, household, plot, SeasonStatus.InProgress, ct);
        var closed = await TestData.CreateSeasonAsync(factory.Postgres, household, plot, SeasonStatus.Closed, ct);
        var cancelled = await TestData.CreateSeasonAsync(factory.Postgres, household, plot, SeasonStatus.Cancelled, ct);

        var client = factory.CreateClientFor(household.Owner);

        // 1. ManageFarmTasks (công việc): Planned, InProgress
        var resTasks = await client.GetAsync($"{OptionsPath}?operation=ManageFarmTasks", ct);
        Assert.Equal(HttpStatusCode.OK, resTasks.StatusCode);
        var itemsTasks = (await resTasks.ReadJsonAsync(ct)).GetProperty("data").EnumerateArray().Select(x => x.GetProperty("id").GetGuid()).ToList();
        Assert.Contains(planned.Id, itemsTasks);
        Assert.Contains(inProgress.Id, itemsTasks);
        Assert.DoesNotContain(closed.Id, itemsTasks);
        Assert.DoesNotContain(cancelled.Id, itemsTasks);

        // 2. ManageHarvests (thu hoạch): chỉ InProgress
        var resHarvests = await client.GetAsync($"{OptionsPath}?operation=ManageHarvests", ct);
        Assert.Equal(HttpStatusCode.OK, resHarvests.StatusCode);
        var bodyHarvests = await resHarvests.ReadJsonAsync(ct);
        var itemsHarvests = bodyHarvests.GetProperty("data").EnumerateArray().Select(x => x.GetProperty("id").GetGuid()).ToList();
        Assert.DoesNotContain(planned.Id, itemsHarvests);
        Assert.Contains(inProgress.Id, itemsHarvests);
        Assert.DoesNotContain(closed.Id, itemsHarvests);

        // 3. RecordPayment (thanh toán): InProgress, Closed
        var resPayment = await client.GetAsync($"{OptionsPath}?operation=RecordPayment", ct);
        Assert.Equal(HttpStatusCode.OK, resPayment.StatusCode);
        var itemsPayment = (await resPayment.ReadJsonAsync(ct)).GetProperty("data").EnumerateArray().Select(x => x.GetProperty("id").GetGuid()).ToList();
        Assert.Contains(inProgress.Id, itemsPayment);
        Assert.Contains(closed.Id, itemsPayment);
        Assert.DoesNotContain(planned.Id, itemsPayment);

        // Kiểm tra định dạng nhãn Label "{CropName} · {PlotCode} · {StartDate}"
        var firstItem = bodyHarvests.GetProperty("data").EnumerateArray().First();
        var label = firstItem.GetProperty("label").GetString();
        Assert.NotNull(label);
        Assert.Contains("TD001", label);
        Assert.Contains("·", label);
    }

    [Fact(DisplayName = "BR-USR-13: Thành viên có quyền Kho thấy mùa vụ ngoài phân công khi operation=StockOut")]
    public async Task GetOptions_StockPermission_WidenedScope()
    {
        var ct = TestContext.Current.CancellationToken;
        var household = await factory.CreateHouseholdAsync(ct);
        var plot = await TestData.CreatePlotAsync(factory.Postgres, household, "TD001", ct);
        var season = await TestData.CreateSeasonAsync(factory.Postgres, household, plot, SeasonStatus.InProgress, ct);

        // Thành viên có Kho (MemberWithAllPermissions.CanManageStock = true) nhưng không được phân công
        var stockMemberClient = factory.CreateClientFor(household.MemberWithAllPermissions);
        var resWithStock = await stockMemberClient.GetAsync($"{OptionsPath}?operation=StockOut", ct);
        Assert.Equal(HttpStatusCode.OK, resWithStock.StatusCode);
        var itemsWithStock = (await resWithStock.ReadJsonAsync(ct)).GetProperty("data").EnumerateArray().Select(x => x.GetProperty("id").GetGuid()).ToList();
        Assert.Contains(season.Id, itemsWithStock);

        // Thành viên không có quyền Kho cũng không được phân công
        var normalMemberClient = factory.CreateClientFor(household.Member);
        var resNoStock = await normalMemberClient.GetAsync($"{OptionsPath}?operation=StockOut", ct);
        Assert.Equal(HttpStatusCode.OK, resNoStock.StatusCode);
        var itemsNoStock = (await resNoStock.ReadJsonAsync(ct)).GetProperty("data").EnumerateArray().Select(x => x.GetProperty("id").GetGuid()).ToList();
        Assert.DoesNotContain(season.Id, itemsNoStock);
    }

    [Fact(DisplayName = "BR-USR-13: Thành viên có quyền Tài chính thấy mùa vụ ngoài phân công khi operation=ManageSales")]
    public async Task GetOptions_FinancePermission_WidenedScope()
    {
        var ct = TestContext.Current.CancellationToken;
        var household = await factory.CreateHouseholdAsync(ct);
        var plot = await TestData.CreatePlotAsync(factory.Postgres, household, "TD001", ct);
        var season = await TestData.CreateSeasonAsync(factory.Postgres, household, plot, SeasonStatus.InProgress, ct);

        // Thành viên có TC (MemberWithAllPermissions.CanManageFinance = true) nhưng không được phân công
        var financeMemberClient = factory.CreateClientFor(household.MemberWithAllPermissions);
        var resWithFinance = await financeMemberClient.GetAsync($"{OptionsPath}?operation=ManageSales", ct);
        Assert.Equal(HttpStatusCode.OK, resWithFinance.StatusCode);
        var itemsWithFinance = (await resWithFinance.ReadJsonAsync(ct)).GetProperty("data").EnumerateArray().Select(x => x.GetProperty("id").GetGuid()).ToList();
        Assert.Contains(season.Id, itemsWithFinance);

        // Thành viên không có quyền TC cũng không được phân công
        var normalMemberClient = factory.CreateClientFor(household.Member);
        var resNoFinance = await normalMemberClient.GetAsync($"{OptionsPath}?operation=ManageSales", ct);
        Assert.Equal(HttpStatusCode.OK, resNoFinance.StatusCode);
        var itemsNoFinance = (await resNoFinance.ReadJsonAsync(ct)).GetProperty("data").EnumerateArray().Select(x => x.GetProperty("id").GetGuid()).ToList();
        Assert.DoesNotContain(season.Id, itemsNoFinance);
    }

    [Fact(DisplayName = "Ma trận phân quyền: Nhân công gọi API ô chọn mùa vụ trả FORBIDDEN")]
    public async Task GetOptions_Worker_ReturnsForbidden()
    {
        var ct = TestContext.Current.CancellationToken;
        var household = await factory.CreateHouseholdAsync(ct);

        var workerClient = factory.CreateClientFor(household.Worker);
        var response = await workerClient.GetAsync($"{OptionsPath}?operation=StockOut", ct);
        await response.AssertProblemAsync(HttpStatusCode.Forbidden, ErrorCodes.Forbidden, ct);
    }

    [Fact(DisplayName = "FR-SEA-11: ô chọn mùa vụ khi không truyền operation trả về mọi trạng thái để phục vụ bộ lọc")]
    public async Task GetOptions_WithoutOperation_ReturnsAllStatusesForFilter()
    {
        var ct = TestContext.Current.CancellationToken;
        var household = await factory.CreateHouseholdAsync(ct);
        var plot = await TestData.CreatePlotAsync(factory.Postgres, household, "TD001", ct);

        var planned = await TestData.CreateSeasonAsync(factory.Postgres, household, plot, SeasonStatus.Planned, ct);
        var inProgress = await TestData.CreateSeasonAsync(factory.Postgres, household, plot, SeasonStatus.InProgress, ct);
        var closed = await TestData.CreateSeasonAsync(factory.Postgres, household, plot, SeasonStatus.Closed, ct);
        var cancelled = await TestData.CreateSeasonAsync(factory.Postgres, household, plot, SeasonStatus.Cancelled, ct);

        var client = factory.CreateClientFor(household.Owner);

        // Gọi không truyền operation: phải trả về cả 4 trạng thái
        var response = await client.GetAsync(OptionsPath, ct);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var items = (await response.ReadJsonAsync(ct)).GetProperty("data").EnumerateArray().Select(x => x.GetProperty("id").GetGuid()).ToList();
        Assert.Contains(planned.Id, items);
        Assert.Contains(inProgress.Id, items);
        Assert.Contains(closed.Id, items);
        Assert.Contains(cancelled.Id, items);
    }

    [Fact(DisplayName = "BR-USR-13, FR-SUP-08: Thành viên có quyền Kho thấy mùa vụ đã chốt ngoài phân công khi không truyền operation")]
    public async Task GetOptions_WithoutOperation_StockPermission_WidenedScope()
    {
        var ct = TestContext.Current.CancellationToken;
        var household = await factory.CreateHouseholdAsync(ct);
        var plot = await TestData.CreatePlotAsync(factory.Postgres, household, "TD001", ct);

        // Tạo mùa vụ đã chốt nhưng thành viên không được phân công
        var closedSeason = await TestData.CreateSeasonAsync(factory.Postgres, household, plot, SeasonStatus.Closed, ct);

        // Thành viên có quyền Kho nhưng không được phân công
        var stockMemberClient = factory.CreateClientFor(household.MemberWithAllPermissions);
        var resWithStock = await stockMemberClient.GetAsync(OptionsPath, ct);
        Assert.Equal(HttpStatusCode.OK, resWithStock.StatusCode);
        var itemsWithStock = (await resWithStock.ReadJsonAsync(ct)).GetProperty("data").EnumerateArray().Select(x => x.GetProperty("id").GetGuid()).ToList();
        Assert.Contains(closedSeason.Id, itemsWithStock);

        // Thành viên không có quyền Kho và không được phân công
        var normalMemberClient = factory.CreateClientFor(household.Member);
        var resNoStock = await normalMemberClient.GetAsync(OptionsPath, ct);
        Assert.Equal(HttpStatusCode.OK, resNoStock.StatusCode);
        var itemsNoStock = (await resNoStock.ReadJsonAsync(ct)).GetProperty("data").EnumerateArray().Select(x => x.GetProperty("id").GetGuid()).ToList();
        Assert.DoesNotContain(closedSeason.Id, itemsNoStock);
    }
}

