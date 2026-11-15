using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using HouseholdFarm.Api.Common.Auditing;
using HouseholdFarm.Api.Common.Errors;
using HouseholdFarm.Api.Domain.Entities;
using HouseholdFarm.Api.Domain.Enums;
using HouseholdFarm.Api.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace HouseholdFarm.Api.Tests.Features.Harvests;

/// <summary>Kiểm thử phiếu thu hoạch: tạo, danh sách, chi tiết, đính chính và hủy (FR-HAR-01, FR-HAR-03, FR-HAR-04).</summary>
public class HarvestRecordTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private const string HarvestsPath = "/api/harvests";

    [Fact(DisplayName = "FR-HAR-01: Chủ hộ và thành viên trong phân công tạo phiếu thu hoạch hợp lệ, có AuditLog")]
    public async Task Create_OwnerAndAssignedMember_SucceedsAndLogsAudit()
    {
        var ct = TestContext.Current.CancellationToken;
        var (household, season) = await ArrangeAsync(SeasonStatus.InProgress, ct);
        await TestData.AssignMemberAsync(factory.Postgres, household, season, household.Member, ct);

        foreach (var actor in new[] { household.Owner, household.Member })
        {
            var client = factory.CreateClientFor(actor);
            var response = await client.PostAsJsonAsync(HarvestsPath, NewCreateBody(season.Id, quantity: 350.5m), ct);

            Assert.Equal(HttpStatusCode.Created, response.StatusCode);
            var data = (await response.ReadJsonAsync(ct)).GetProperty("data");
            var id = data.GetProperty("id").GetGuid();
            Assert.Equal(season.Id, data.GetProperty("seasonId").GetGuid());
            Assert.Equal("Grade1", data.GetProperty("qualityGrade").GetString());
            Assert.Equal(350.5m, data.GetProperty("quantity").GetDecimal());
            Assert.Equal("Kilogram", data.GetProperty("yieldUnit").GetString());
            Assert.True(data.GetProperty("canModify").GetBoolean());
            Assert.Equal($"{HarvestsPath}/{id}", response.Headers.Location?.AbsolutePath);

            await AuditLogAssertions.AssertSingleAsync(
                factory.Postgres,
                household.Household.Id,
                AuditActions.HarvestRecordCreate,
                id,
                ct);
        }
    }

    [Fact(DisplayName = "BR-GEN-10: gửi lại cùng requestId trả bản ghi cũ kể cả khi mùa vụ đã chốt")]
    public async Task Create_DuplicateRequestId_ReturnsExistingWithoutDuplicateAudit()
    {
        var ct = TestContext.Current.CancellationToken;
        var (household, season) = await ArrangeAsync(SeasonStatus.InProgress, ct);
        var client = factory.CreateClientFor(household.Owner);
        var requestId = Guid.CreateVersion7();

        var first = await client.PostAsJsonAsync(HarvestsPath, NewCreateBody(season.Id, requestId: requestId), ct);
        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        var firstId = (await first.ReadJsonAsync(ct)).GetProperty("data").GetProperty("id").GetGuid();

        await using (var db = factory.Postgres.CreateDbContext(TestCurrentUser.For(household.Owner)))
        {
            var s = await db.Seasons.SingleAsync(x => x.Id == season.Id, ct);
            s.Status = SeasonStatus.Closed;
            await db.SaveChangesAsync(ct);
        }

        var second = await client.PostAsJsonAsync(HarvestsPath, NewCreateBody(season.Id, requestId: requestId), ct);
        Assert.Equal(HttpStatusCode.Created, second.StatusCode);
        var secondId = (await second.ReadJsonAsync(ct)).GetProperty("data").GetProperty("id").GetGuid();

        Assert.Equal(firstId, secondId);
        await AuditLogAssertions.AssertSingleAsync(
            factory.Postgres,
            household.Household.Id,
            AuditActions.HarvestRecordCreate,
            firstId,
            ct);
    }

    [Theory(DisplayName = "BR-HAR-01, BR-GEN-05: ngày thu trước ngày bắt đầu vụ, sau hôm nay, hoặc sản lượng <= 0 trả VALIDATION_ERROR")]
    [InlineData("beforeStart")]
    [InlineData("futureDate")]
    [InlineData("zeroQty")]
    [InlineData("missingGrade")]
    public async Task Create_InvalidInput_ReturnsValidationError(string invalidCase)
    {
        var ct = TestContext.Current.CancellationToken;
        var (household, season) = await ArrangeAsync(SeasonStatus.InProgress, ct);
        var client = factory.CreateClientFor(household.Owner);
        var tomorrow = DateOnly.FromDateTime(TimeProvider.System.GetUtcNow().UtcDateTime).AddDays(2);

        object body = invalidCase switch
        {
            "beforeStart" => NewCreateBody(season.Id, harvestDate: new DateOnly(2026, 9, 30)),
            "futureDate" => NewCreateBody(season.Id, harvestDate: tomorrow),
            "zeroQty" => NewCreateBody(season.Id, quantity: 0m),
            _ => new
            {
                requestId = Guid.CreateVersion7(),
                seasonId = season.Id,
                harvestDate = new DateOnly(2026, 10, 5),
                quantity = 100m,
            },
        };

        var response = await client.PostAsJsonAsync(HarvestsPath, body, ct);
        var expectedErrorCode = ErrorCodes.ValidationError;
        await response.AssertProblemAsync(HttpStatusCode.BadRequest, expectedErrorCode, ct);

        await using var db = factory.Postgres.CreateDbContext();
        Assert.False(await db.HarvestRecords.IgnoreQueryFilters().AnyAsync(h => h.SeasonId == season.Id, ct));
    }

    [Theory(DisplayName = "BR-SEA-10: tạo phiếu thu cho mùa vụ không InProgress trả SEASON_INVALID_STATE")]
    [InlineData(SeasonStatus.Planned)]
    [InlineData(SeasonStatus.Closed)]
    [InlineData(SeasonStatus.Cancelled)]
    public async Task Create_SeasonNotInProgress_ReturnsSeasonInvalidState(SeasonStatus status)
    {
        var ct = TestContext.Current.CancellationToken;
        var (household, season) = await ArrangeAsync(status, ct);
        var client = factory.CreateClientFor(household.Owner);

        var response = await client.PostAsJsonAsync(HarvestsPath, NewCreateBody(season.Id), ct);
        await response.AssertProblemAsync(HttpStatusCode.UnprocessableEntity, ErrorCodes.SeasonInvalidState, ct);
    }

    [Fact(DisplayName = "Ma trận 2.5 & BR-GEN-02: thành viên ngoài phân công (kể cả TV+TC) không tạo/sửa/hủy được; nhân công nhận FORBIDDEN")]
    public async Task Mutations_UnauthorizedRoles_Blocked()
    {
        var ct = TestContext.Current.CancellationToken;
        var (household, season) = await ArrangeAsync(SeasonStatus.InProgress, ct);
        var existing = await CreateHarvestInDbAsync(household, season.Id, QualityGrade.Grade1, 200m, ct);

        // Nhân công -> 403
        var workerClient = factory.CreateClientFor(household.Worker);
        await (await workerClient.PostAsJsonAsync(HarvestsPath, NewCreateBody(season.Id), ct))
            .AssertProblemAsync(HttpStatusCode.Forbidden, ErrorCodes.Forbidden, ct);
        await (await workerClient.GetAsync(HarvestsPath, ct))
            .AssertProblemAsync(HttpStatusCode.Forbidden, ErrorCodes.Forbidden, ct);

        // Thành viên đủ quyền (TV+TC, TV+Kho) nhưng chưa được phân công vào mùa vụ -> không được tạo, đính chính, hủy (NOT_FOUND)
        var outOfScopeClient = factory.CreateClientFor(household.MemberWithAllPermissions);
        await (await outOfScopeClient.PostAsJsonAsync(HarvestsPath, NewCreateBody(season.Id), ct))
            .AssertProblemAsync(HttpStatusCode.NotFound, ErrorCodes.NotFound, ct);
        await (await outOfScopeClient.PostAsJsonAsync($"{HarvestsPath}/{existing.Id}/correct", NewCorrectBody(existing.Version), ct))
            .AssertProblemAsync(HttpStatusCode.NotFound, ErrorCodes.NotFound, ct);
        await (await outOfScopeClient.PostAsJsonAsync($"{HarvestsPath}/{existing.Id}/void", new { reason = "Thử hủy" }, ct))
            .AssertProblemAsync(HttpStatusCode.NotFound, ErrorCodes.NotFound, ct);
    }

    [Fact(DisplayName = "FR-HAR-04: danh sách lọc đúng; TV+TC ngoài phân công xem được danh sách & chi tiết với canModify = false")]
    public async Task GetListAndDetail_FiltersAndFinanceScope_WorkCorrectly()
    {
        var ct = TestContext.Current.CancellationToken;
        var (household, season) = await ArrangeAsync(SeasonStatus.InProgress, ct);
        var plot2 = await TestData.CreatePlotAsync(factory.Postgres, household, "TD002", ct);
        var season2 = await TestData.CreateSeasonAsync(factory.Postgres, household, plot2, SeasonStatus.InProgress, ct);

        var h1 = await CreateHarvestInDbAsync(household, season.Id, QualityGrade.Grade1, 300m, ct, date: new DateOnly(2026, 10, 5), note: "Đợt đầu");
        var h2 = await CreateHarvestInDbAsync(household, season2.Id, QualityGrade.Grade2, 150m, ct, date: new DateOnly(2026, 10, 12), note: "Đợt hai");
        var hVoid = await CreateHarvestInDbAsync(household, season.Id, QualityGrade.Grade1, 50m, ct, voided: true);

        // TV thường ngoài phân công -> thấy 0
        var plainMemberRes = await factory.CreateClientFor(household.Member).GetAsync(HarvestsPath, ct);
        Assert.Equal(HttpStatusCode.OK, plainMemberRes.StatusCode);
        Assert.Empty((await plainMemberRes.ReadJsonAsync(ct)).GetProperty("data").EnumerateArray());

        // TV+TC ngoài phân công -> xem được cả 2 phiếu chưa hủy, detail có canModify = false
        var financeClient = factory.CreateClientFor(household.MemberWithAllPermissions);
        var finListRes = await financeClient.GetAsync(HarvestsPath, ct);
        Assert.Equal(HttpStatusCode.OK, finListRes.StatusCode);
        var finIds = (await finListRes.ReadJsonAsync(ct)).GetProperty("data").EnumerateArray().Select(x => x.GetProperty("id").GetGuid()).ToList();
        Assert.Contains(h1.Id, finIds);
        Assert.Contains(h2.Id, finIds);
        Assert.DoesNotContain(hVoid.Id, finIds);

        var finDetailRes = await financeClient.GetAsync($"{HarvestsPath}/{h1.Id}", ct);
        Assert.Equal(HttpStatusCode.OK, finDetailRes.StatusCode);
        Assert.False((await finDetailRes.ReadJsonAsync(ct)).GetProperty("data").GetProperty("canModify").GetBoolean());

        // Chủ hộ lọc theo seasonId + qualityGrade + from/to + includeVoided
        var ownerClient = factory.CreateClientFor(household.Owner);
        var filteredRes = await ownerClient.GetAsync(
            $"{HarvestsPath}?seasonId={season.Id}&qualityGrade=Grade1&from=2026-10-01&to=2026-10-10&includeVoided=true",
            ct);
        var filteredIds = (await filteredRes.ReadJsonAsync(ct)).GetProperty("data").EnumerateArray().Select(x => x.GetProperty("id").GetGuid()).ToList();
        Assert.Equal(2, filteredIds.Count);
        Assert.Contains(h1.Id, filteredIds);
        Assert.Contains(hVoid.Id, filteredIds);
    }

    [Fact(DisplayName = "FR-HAR-03 & BR-GEN-08, BR-GEN-11: đính chính ghi AuditLog đúng trường đổi và chặn version cũ")]
    public async Task Correct_UpdatesAndLogsChangedFields_BlocksStaleVersion()
    {
        var ct = TestContext.Current.CancellationToken;
        var (household, season) = await ArrangeAsync(SeasonStatus.InProgress, ct);
        var harvest = await CreateHarvestInDbAsync(household, season.Id, QualityGrade.Grade1, 400m, ct);
        var client = factory.CreateClientFor(household.Owner);

        var detail = (await (await client.GetAsync($"{HarvestsPath}/{harvest.Id}", ct)).ReadJsonAsync(ct)).GetProperty("data");
        var version = detail.GetProperty("version").GetUInt32();

        var correctRes = await client.PostAsJsonAsync(
            $"{HarvestsPath}/{harvest.Id}/correct",
            NewCorrectBody(version, quantity: 450m, qualityGrade: "Grade1", reason: "Cân lại bao"),
            ct);

        Assert.Equal(HttpStatusCode.OK, correctRes.StatusCode);
        Assert.Equal(450m, (await correctRes.ReadJsonAsync(ct)).GetProperty("data").GetProperty("quantity").GetDecimal());

        var log = await AuditLogAssertions.AssertSingleAsync(
            factory.Postgres,
            household.Household.Id,
            AuditActions.HarvestRecordCorrect,
            harvest.Id,
            ct,
            reason: "Cân lại bao");
        var oldDoc = JsonDocument.Parse(log.OldValues!).RootElement;
        var newDoc = JsonDocument.Parse(log.NewValues!).RootElement;
        Assert.Equal(["quantity"], oldDoc.EnumerateObject().Select(p => p.Name));
        Assert.Equal(400m, oldDoc.GetProperty("quantity").GetDecimal());
        Assert.Equal(450m, newDoc.GetProperty("quantity").GetDecimal());

        // Gửi lại với version cũ -> CONCURRENCY_CONFLICT
        var staleRes = await client.PostAsJsonAsync(
            $"{HarvestsPath}/{harvest.Id}/correct",
            NewCorrectBody(version, quantity: 420m),
            ct);
        await staleRes.AssertProblemAsync(HttpStatusCode.Conflict, ErrorCodes.ConcurrencyConflict, ct);
    }

    [Fact(DisplayName = "TC-13, BR-HAR-03, BR-HAR-04: đính chính giảm hoặc đổi loại chất lượng / hủy làm tồn âm trả PRODUCE_STOCK_NEGATIVE")]
    public async Task CorrectOrVoid_WhenResultingStockNegative_ReturnsProduceStockNegative()
    {
        var ct = TestContext.Current.CancellationToken;
        var (household, season) = await ArrangeAsync(SeasonStatus.InProgress, ct);
        var harvest = await CreateHarvestInDbAsync(household, season.Id, QualityGrade.Grade1, 1000m, ct);
        await CreateSaleInDbAsync(household, season.Id, QualityGrade.Grade1, 700m, ct);

        var client = factory.CreateClientFor(household.Owner);
        var detail = (await (await client.GetAsync($"{HarvestsPath}/{harvest.Id}", ct)).ReadJsonAsync(ct)).GetProperty("data");
        var version = detail.GetProperty("version").GetUInt32();

        // TC-13: giảm từ 1000 -> 500 trong khi đã bán 700 => -200 -> PRODUCE_STOCK_NEGATIVE
        var reduceRes = await client.PostAsJsonAsync(
            $"{HarvestsPath}/{harvest.Id}/correct",
            NewCorrectBody(version, quantity: 500m),
            ct);
        await reduceRes.AssertProblemAsync(HttpStatusCode.UnprocessableEntity, ErrorCodes.ProduceStockNegative, ct);

        // Đổi từ Grade1 sang Grade2 làm Grade1 âm (-700) -> PRODUCE_STOCK_NEGATIVE
        var changeGradeRes = await client.PostAsJsonAsync(
            $"{HarvestsPath}/{harvest.Id}/correct",
            NewCorrectBody(version, quantity: 1000m, qualityGrade: "Grade2"),
            ct);
        await changeGradeRes.AssertProblemAsync(HttpStatusCode.UnprocessableEntity, ErrorCodes.ProduceStockNegative, ct);

        // Hủy phiếu đã bán 700 -> PRODUCE_STOCK_NEGATIVE
        var voidRes = await client.PostAsJsonAsync(
            $"{HarvestsPath}/{harvest.Id}/void",
            new { reason = "Thử hủy phiếu đã bán" },
            ct);
        await voidRes.AssertProblemAsync(HttpStatusCode.UnprocessableEntity, ErrorCodes.ProduceStockNegative, ct);
        await AuditLogAssertions.AssertNoneAsync(factory.Postgres, harvest.Id, ct);

        // Nhưng đính chính từ 1000 -> 800 vẫn còn 100 tồn >= 0 => thành công
        var validReduce = await client.PostAsJsonAsync(
            $"{HarvestsPath}/{harvest.Id}/correct",
            NewCorrectBody(version, quantity: 800m, reason: "Trừ tạp chất"),
            ct);
        Assert.Equal(HttpStatusCode.OK, validReduce.StatusCode);
    }

    [Fact(DisplayName = "BR-HAR-04 & BR-GEN-09: hủy phiếu thu hoạch thành công ghi nhật ký; hủy hoặc sửa lần nữa trả ALREADY_VOIDED")]
    public async Task Void_ValidHarvest_LogsAuditAndBlocksSecondVoidOrCorrect()
    {
        var ct = TestContext.Current.CancellationToken;
        var (household, season) = await ArrangeAsync(SeasonStatus.InProgress, ct);
        var harvest = await CreateHarvestInDbAsync(household, season.Id, QualityGrade.Grade2, 250m, ct);
        var client = factory.CreateClientFor(household.Owner);

        var first = await client.PostAsJsonAsync($"{HarvestsPath}/{harvest.Id}/void", new { reason = "Nhập nhầm lô" }, ct);
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        var data = (await first.ReadJsonAsync(ct)).GetProperty("data");
        Assert.NotEqual(JsonValueKind.Null, data.GetProperty("voidedAt").ValueKind);
        Assert.Equal("Nhập nhầm lô", data.GetProperty("voidReason").GetString());
        Assert.False(data.GetProperty("canModify").GetBoolean());

        await AuditLogAssertions.AssertSingleAsync(
            factory.Postgres,
            household.Household.Id,
            AuditActions.HarvestRecordVoid,
            harvest.Id,
            ct,
            reason: "Nhập nhầm lô");

        var secondVoid = await client.PostAsJsonAsync($"{HarvestsPath}/{harvest.Id}/void", new { reason = "Hủy lại" }, ct);
        await secondVoid.AssertProblemAsync(HttpStatusCode.UnprocessableEntity, ErrorCodes.AlreadyVoided, ct);

        var version = data.GetProperty("version").GetUInt32();
        var correctAfterVoid = await client.PostAsJsonAsync(
            $"{HarvestsPath}/{harvest.Id}/correct",
            NewCorrectBody(version, quantity: 100m),
            ct);
        await correctAfterVoid.AssertProblemAsync(HttpStatusCode.UnprocessableEntity, ErrorCodes.AlreadyVoided, ct);
    }

    private async Task<(TestHousehold Household, Season Season)> ArrangeAsync(SeasonStatus status, CancellationToken ct)
    {
        var household = await factory.CreateHouseholdAsync(ct);
        var plot = await TestData.CreatePlotAsync(factory.Postgres, household, "TD001", ct);
        var season = await TestData.CreateSeasonAsync(factory.Postgres, household, plot, status, ct);
        return (household, season);
    }

    private async Task<HarvestRecord> CreateHarvestInDbAsync(
        TestHousehold household,
        Guid seasonId,
        QualityGrade grade,
        decimal quantity,
        CancellationToken ct,
        DateOnly? date = null,
        string? note = null,
        bool voided = false)
    {
        var record = new HarvestRecord
        {
            SeasonId = seasonId,
            HarvestDate = date ?? new DateOnly(2026, 10, 5),
            QualityGrade = grade,
            Quantity = quantity,
            Note = note,
            RequestId = Guid.CreateVersion7(),
            VoidedAt = voided ? TimeProvider.System.GetUtcNow().UtcDateTime : null,
            VoidReason = voided ? "Phiếu hủy sẵn" : null,
        };
        await using var db = factory.Postgres.CreateDbContext(TestCurrentUser.For(household.Owner));
        db.HarvestRecords.Add(record);
        await db.SaveChangesAsync(ct);
        return record;
    }

    private async Task<Sale> CreateSaleInDbAsync(
        TestHousehold household,
        Guid seasonId,
        QualityGrade grade,
        decimal quantity,
        CancellationToken ct)
    {
        var sale = new Sale
        {
            SeasonId = seasonId,
            Channel = SalesChannel.Trader,
            QualityGrade = grade,
            Quantity = quantity,
            UnitPrice = 10_000m,
            SaleDate = new DateOnly(2026, 10, 6),
            RequestId = Guid.CreateVersion7(),
        };
        await using var db = factory.Postgres.CreateDbContext(TestCurrentUser.For(household.Owner));
        db.Sales.Add(sale);
        await db.SaveChangesAsync(ct);
        return sale;
    }

    private static object NewCreateBody(
        Guid seasonId,
        decimal quantity = 200m,
        string qualityGrade = "Grade1",
        DateOnly? harvestDate = null,
        Guid? requestId = null) => new
    {
        requestId = requestId ?? Guid.CreateVersion7(),
        seasonId,
        harvestDate = harvestDate ?? new DateOnly(2026, 10, 5),
        qualityGrade,
        quantity,
        note = "Thu hoạch buổi sáng",
    };

    private static object NewCorrectBody(
        uint version,
        decimal quantity = 200m,
        string qualityGrade = "Grade1",
        DateOnly? harvestDate = null,
        string? note = null,
        string reason = "Ghi nhầm sản lượng") => new
    {
        harvestDate = harvestDate ?? new DateOnly(2026, 10, 5),
        qualityGrade,
        quantity,
        note,
        reason,
        version,
    };
}
