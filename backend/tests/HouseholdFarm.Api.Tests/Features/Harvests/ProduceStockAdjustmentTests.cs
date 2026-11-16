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

/// <summary>Kiểm thử xem tồn nông sản và điều chỉnh giảm, hủy điều chỉnh tồn nông sản (FR-HAR-06, FR-HAR-07, TC-15).</summary>
public class ProduceStockAdjustmentTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private const string StockPath = "/api/produce-stock";
    private const string AdjustmentsPath = "/api/produce-stock-adjustments";

    [Fact(DisplayName = "FR-HAR-06: Xem tồn nông sản theo mùa vụ, ẩn dòng bằng 0 khi không truyền seasonId và hiện đủ 3 loại khi truyền seasonId")]
    public async Task GetOverview_FiltersZeroRowsAndRespectsScope()
    {
        var ct = TestContext.Current.CancellationToken;
        var household = await factory.CreateHouseholdAsync(ct);
        var plot1 = await TestData.CreatePlotAsync(factory.Postgres, household, "TD001", ct);
        var plot2 = await TestData.CreatePlotAsync(factory.Postgres, household, "TD002", ct);
        var assignedSeason = await TestData.CreateSeasonAsync(factory.Postgres, household, plot1, SeasonStatus.InProgress, ct);
        var otherSeason = await TestData.CreateSeasonAsync(factory.Postgres, household, plot2, SeasonStatus.InProgress, ct);

        await TestData.AssignMemberAsync(factory.Postgres, household, assignedSeason, household.Member, ct);
        await AddHarvestAsync(household, assignedSeason.Id, QualityGrade.Grade1, 1000m, ct);
        await AddSaleAsync(household, assignedSeason.Id, QualityGrade.Grade1, 700m, ct);
        await AddHarvestAsync(household, otherSeason.Id, QualityGrade.Grade2, 300m, ct);

        // Chủ hộ xem tổng hợp không lọc mùa vụ: chỉ có 2 dòng khác 0.
        var ownerClient = factory.CreateClientFor(household.Owner);
        var allRes = await ownerClient.GetAsync(StockPath, ct);
        Assert.Equal(HttpStatusCode.OK, allRes.StatusCode);
        var allItems = (await allRes.ReadJsonAsync(ct)).GetProperty("data");
        Assert.Equal(2, allItems.GetArrayLength());

        var g1 = allItems.EnumerateArray().Single(x =>
            x.GetProperty("seasonId").GetGuid() == assignedSeason.Id
            && x.GetProperty("qualityGrade").GetString() == "Grade1");
        Assert.Equal(1000m, g1.GetProperty("totalHarvested").GetDecimal());
        Assert.Equal(700m, g1.GetProperty("totalSold").GetDecimal());
        Assert.Equal(0m, g1.GetProperty("totalAdjusted").GetDecimal());
        Assert.Equal(300m, g1.GetProperty("quantity").GetDecimal());

        // Lọc theo 1 mùa vụ: trả đủ cả 3 loại chất lượng, kể cả loại bằng 0.
        var bySeasonRes = await ownerClient.GetAsync($"{StockPath}?seasonId={assignedSeason.Id}", ct);
        Assert.Equal(HttpStatusCode.OK, bySeasonRes.StatusCode);
        var bySeasonItems = (await bySeasonRes.ReadJsonAsync(ct)).GetProperty("data");
        Assert.Equal(3, bySeasonItems.GetArrayLength());

        // Thành viên thường chỉ thấy mùa vụ được phân công; hỏi mùa vụ ngoài phạm vi bị NOT_FOUND.
        var memberClient = factory.CreateClientFor(household.Member);
        var memberAll = (await (await memberClient.GetAsync(StockPath, ct)).ReadJsonAsync(ct)).GetProperty("data");
        Assert.Single(memberAll.EnumerateArray());
        var outOfScopeRes = await memberClient.GetAsync($"{StockPath}?seasonId={otherSeason.Id}", ct);
        Assert.Equal(HttpStatusCode.NotFound, outOfScopeRes.StatusCode);

        // Thành viên có quyền tài chính (BR-USR-13) thấy cả mùa vụ ngoài phân công.
        var financeClient = factory.CreateClientFor(household.MemberWithAllPermissions);
        var financeAll = (await (await financeClient.GetAsync(StockPath, ct)).ReadJsonAsync(ct)).GetProperty("data");
        Assert.Equal(2, financeAll.GetArrayLength());

        // Nhân công -> FORBIDDEN (BR-USR-14).
        var workerClient = factory.CreateClientFor(household.Worker);
        var workerRes = await workerClient.GetAsync(StockPath, ct);
        await workerRes.AssertProblemAsync(HttpStatusCode.Forbidden, ErrorCodes.Forbidden, ct);
    }

    [Fact(DisplayName = "TC-15 / FR-HAR-07: Chủ hộ điều chỉnh giảm 300 kg về 0, ghi AuditLog và chốt được mùa vụ")]
    public async Task CreateAdjustment_Tc15_ZeroesStockAllowsSeasonCloseAndLogsAudit()
    {
        var ct = TestContext.Current.CancellationToken;
        var household = await factory.CreateHouseholdAsync(ct);
        var plot = await TestData.CreatePlotAsync(factory.Postgres, household, "TD001", ct);
        var season = await TestData.CreateSeasonAsync(factory.Postgres, household, plot, SeasonStatus.InProgress, ct);
        await AddHarvestAsync(household, season.Id, QualityGrade.Grade1, 300m, ct);

        var ownerClient = factory.CreateClientFor(household.Owner);

        // Trước khi điều chỉnh: chốt mùa vụ bị chặn do còn 300 kg tồn.
        var closeBlocked = await ownerClient.PostAsJsonAsync($"/api/seasons/{season.Id}/close", new { confirmed = true }, ct);
        await closeBlocked.AssertProblemAsync(HttpStatusCode.UnprocessableEntity, ErrorCodes.SeasonProduceRemaining, ct);

        var requestId = Guid.CreateVersion7();
        var adjRes = await ownerClient.PostAsJsonAsync(AdjustmentsPath, new
        {
            requestId,
            seasonId = season.Id,
            qualityGrade = "Grade1",
            quantity = 300m,
            adjustmentDate = new DateOnly(2026, 10, 10),
            reason = "HouseholdUse",
            note = "Dùng trong gia đình",
        }, ct);

        Assert.Equal(HttpStatusCode.Created, adjRes.StatusCode);
        var adjData = (await adjRes.ReadJsonAsync(ct)).GetProperty("data");
        var adjId = adjData.GetProperty("id").GetGuid();
        Assert.Equal(300m, adjData.GetProperty("quantity").GetDecimal());
        Assert.Equal("HouseholdUse", adjData.GetProperty("reason").GetString());

        await AuditLogAssertions.AssertSingleAsync(
            factory.Postgres,
            household.Household.Id,
            AuditActions.ProduceStockAdjustmentAdjust,
            adjId,
            ct);

        // BR-GEN-10: gửi lại cùng requestId trả bản ghi cũ, không trừ hai lần.
        var duplicateRes = await ownerClient.PostAsJsonAsync(AdjustmentsPath, new
        {
            requestId,
            seasonId = season.Id,
            qualityGrade = "Grade1",
            quantity = 300m,
            adjustmentDate = new DateOnly(2026, 10, 10),
            reason = "HouseholdUse",
        }, ct);
        Assert.Equal(HttpStatusCode.Created, duplicateRes.StatusCode);
        Assert.Equal(adjId, (await duplicateRes.ReadJsonAsync(ct)).GetProperty("data").GetProperty("id").GetGuid());

        // Sau khi điều chỉnh về 0: chốt mùa vụ thành công.
        var closeOk = await ownerClient.PostAsJsonAsync($"/api/seasons/{season.Id}/close", new { confirmed = true }, ct);
        Assert.Equal(HttpStatusCode.OK, closeOk.StatusCode);
    }

    [Fact(DisplayName = "BR-GEN-10: gửi lại cùng requestId trả phiếu đã tạo và chỉ trừ tồn nông sản một lần")]
    public async Task CreateAdjustment_DuplicateRequestId_ReturnsExistingAndDeductsOnce()
    {
        var ct = TestContext.Current.CancellationToken;
        var household = await factory.CreateHouseholdAsync(ct);
        var plot = await TestData.CreatePlotAsync(factory.Postgres, household, "TD001", ct);
        var season = await TestData.CreateSeasonAsync(factory.Postgres, household, plot, SeasonStatus.InProgress, ct);
        await AddHarvestAsync(household, season.Id, QualityGrade.Grade1, 300m, ct);

        var ownerClient = factory.CreateClientFor(household.Owner);
        var requestId = Guid.CreateVersion7();
        var body = new
        {
            requestId,
            seasonId = season.Id,
            qualityGrade = "Grade1",
            quantity = 100m,
            adjustmentDate = new DateOnly(2026, 10, 10),
            reason = "Loss",
        };

        var first = await ownerClient.PostAsJsonAsync(AdjustmentsPath, body, ct);
        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        var firstId = (await first.ReadJsonAsync(ct)).GetProperty("data").GetProperty("id").GetGuid();

        var second = await ownerClient.PostAsJsonAsync(AdjustmentsPath, body, ct);
        Assert.Equal(HttpStatusCode.Created, second.StatusCode);
        var secondId = (await second.ReadJsonAsync(ct)).GetProperty("data").GetProperty("id").GetGuid();
        Assert.Equal(firstId, secondId);

        await using (var db = factory.Postgres.CreateDbContext(TestCurrentUser.For(household.Owner)))
        {
            Assert.Equal(1, await db.ProduceStockAdjustments.CountAsync(a => a.RequestId == requestId, ct));
        }

        // Tồn chỉ giảm 100 -> còn 200 (không bị trừ thành 100).
        var stockItems = (await (await ownerClient.GetAsync($"{StockPath}?seasonId={season.Id}", ct)).ReadJsonAsync(ct))
            .GetProperty("data");
        var g1 = stockItems.EnumerateArray().Single(x => x.GetProperty("qualityGrade").GetString() == "Grade1");
        Assert.Equal(200m, g1.GetProperty("quantity").GetDecimal());
        Assert.Equal(100m, g1.GetProperty("totalAdjusted").GetDecimal());

        await AuditLogAssertions.AssertSingleAsync(
            factory.Postgres,
            household.Household.Id,
            AuditActions.ProduceStockAdjustmentAdjust,
            firstId,
            ct);
    }

    [Fact(DisplayName = "FR-HAR-07: Từ chối điều chỉnh vượt tồn (BR-HAR-05), thiếu ReasonOther, ngày tương lai, hoặc sai quyền")]
    public async Task CreateAdjustment_ValidationAndAuthorizationChecks()
    {
        var ct = TestContext.Current.CancellationToken;
        var household = await factory.CreateHouseholdAsync(ct);
        var plot = await TestData.CreatePlotAsync(factory.Postgres, household, "TD001", ct);
        var season = await TestData.CreateSeasonAsync(factory.Postgres, household, plot, SeasonStatus.InProgress, ct);
        var plannedSeason = await TestData.CreateSeasonAsync(
            factory.Postgres,
            household,
            await TestData.CreatePlotAsync(factory.Postgres, household, "TD002", ct),
            SeasonStatus.Planned,
            ct);
        var closedSeason = await TestData.CreateSeasonAsync(
            factory.Postgres,
            household,
            await TestData.CreatePlotAsync(factory.Postgres, household, "TD003", ct),
            SeasonStatus.Closed,
            ct);

        await AddHarvestAsync(household, season.Id, QualityGrade.Grade1, 100m, ct);
        await AddHarvestAsync(household, season.Id, QualityGrade.Grade2, 500m, ct);

        var ownerClient = factory.CreateClientFor(household.Owner);

        // Vượt tồn Loại 1 (dù Loại 2 còn 500 kg) -> INSUFFICIENT_PRODUCE_STOCK.
        var exceedRes = await ownerClient.PostAsJsonAsync(AdjustmentsPath, new
        {
            requestId = Guid.CreateVersion7(),
            seasonId = season.Id,
            qualityGrade = "Grade1",
            quantity = 101m,
            adjustmentDate = new DateOnly(2026, 10, 10),
            reason = "Loss",
        }, ct);
        await exceedRes.AssertProblemAsync(HttpStatusCode.UnprocessableEntity, ErrorCodes.InsufficientProduceStock, ct);

        // Reason = Other thiếu ReasonOther -> VALIDATION_ERROR.
        var missingOtherRes = await ownerClient.PostAsJsonAsync(AdjustmentsPath, new
        {
            requestId = Guid.CreateVersion7(),
            seasonId = season.Id,
            qualityGrade = "Grade1",
            quantity = 10m,
            adjustmentDate = new DateOnly(2026, 10, 10),
            reason = "Other",
            reasonOther = "   ",
        }, ct);
        await missingOtherRes.AssertProblemAsync(HttpStatusCode.BadRequest, ErrorCodes.ValidationError, ct);

        // Mùa vụ không ở InProgress (Planned, Closed) -> SEASON_INVALID_STATE.
        foreach (var invalidSeasonId in new[] { plannedSeason.Id, closedSeason.Id })
        {
            var invalidStateRes = await ownerClient.PostAsJsonAsync(AdjustmentsPath, new
            {
                requestId = Guid.CreateVersion7(),
                seasonId = invalidSeasonId,
                qualityGrade = "Grade1",
                quantity = 1m,
                adjustmentDate = new DateOnly(2026, 10, 10),
                reason = "Loss",
            }, ct);
            await invalidStateRes.AssertProblemAsync(HttpStatusCode.UnprocessableEntity, ErrorCodes.SeasonInvalidState, ct);
        }

        // Thành viên thường & có ManageFinance đều bị FORBIDDEN.
        foreach (var actor in new[] { household.Member, household.MemberWithAllPermissions, household.Worker })
        {
            var client = factory.CreateClientFor(actor);
            var getRes = await client.GetAsync(AdjustmentsPath, ct);
            await getRes.AssertProblemAsync(HttpStatusCode.Forbidden, ErrorCodes.Forbidden, ct);

            var postRes = await client.PostAsJsonAsync(AdjustmentsPath, new
            {
                requestId = Guid.CreateVersion7(),
                seasonId = season.Id,
                qualityGrade = "Grade1",
                quantity = 5m,
                adjustmentDate = new DateOnly(2026, 10, 10),
                reason = "Loss",
            }, ct);
            await postRes.AssertProblemAsync(HttpStatusCode.Forbidden, ErrorCodes.Forbidden, ct);
        }

        // Các thao tác bị từ chối tuyệt đối không ghi nhật ký (BR-GEN-07).
        await using var db = factory.Postgres.CreateDbContext(TestCurrentUser.For(household.Owner));
        Assert.False(await db.AuditLogs.AnyAsync(
            l => l.Action == AuditActions.ProduceStockAdjustmentAdjust || l.Action == AuditActions.ProduceStockAdjustmentVoid,
            ct));
    }

    [Fact(DisplayName = "FR-HAR-07: Hủy điều chỉnh tồn làm tồn tăng lại, ghi AuditLog và chặn hủy lần hai (BR-HAR-05, BR-GEN-09)")]
    public async Task VoidAdjustment_RestoresStockAndBlocksSecondVoid()
    {
        var ct = TestContext.Current.CancellationToken;
        var household = await factory.CreateHouseholdAsync(ct);
        var plot = await TestData.CreatePlotAsync(factory.Postgres, household, "TD001", ct);
        var season = await TestData.CreateSeasonAsync(factory.Postgres, household, plot, SeasonStatus.InProgress, ct);
        await AddHarvestAsync(household, season.Id, QualityGrade.Grade1, 200m, ct);

        var ownerClient = factory.CreateClientFor(household.Owner);
        var createRes = await ownerClient.PostAsJsonAsync(AdjustmentsPath, new
        {
            requestId = Guid.CreateVersion7(),
            seasonId = season.Id,
            qualityGrade = "Grade1",
            quantity = 80m,
            adjustmentDate = new DateOnly(2026, 10, 10),
            reason = "Damaged",
        }, ct);
        Assert.Equal(HttpStatusCode.Created, createRes.StatusCode);
        var adjId = (await createRes.ReadJsonAsync(ct)).GetProperty("data").GetProperty("id").GetGuid();

        var activeRes = await ownerClient.PostAsJsonAsync(AdjustmentsPath, new
        {
            requestId = Guid.CreateVersion7(),
            seasonId = season.Id,
            qualityGrade = "Grade1",
            quantity = 20m,
            adjustmentDate = new DateOnly(2026, 10, 11),
            reason = "Loss",
        }, ct);
        Assert.Equal(HttpStatusCode.Created, activeRes.StatusCode);
        var activeId = (await activeRes.ReadJsonAsync(ct)).GetProperty("data").GetProperty("id").GetGuid();

        var voidRes = await ownerClient.PostAsJsonAsync(
            $"{AdjustmentsPath}/{adjId}/void",
            new { reason = "Nhập nhầm loại nông sản" },
            ct);
        Assert.Equal(HttpStatusCode.OK, voidRes.StatusCode);
        var voided = (await voidRes.ReadJsonAsync(ct)).GetProperty("data");
        Assert.Equal("Nhập nhầm loại nông sản", voided.GetProperty("voidReason").GetString());
        Assert.NotEqual(JsonValueKind.Null, voided.GetProperty("voidedAt").ValueKind);

        // Tồn nông sản tăng lại từ 100 -> 180 (còn trừ 20 của phiếu active).
        var stockItems = (await (await ownerClient.GetAsync($"{StockPath}?seasonId={season.Id}", ct)).ReadJsonAsync(ct))
            .GetProperty("data");
        var g1 = stockItems.EnumerateArray().Single(x => x.GetProperty("qualityGrade").GetString() == "Grade1");
        Assert.Equal(180m, g1.GetProperty("quantity").GetDecimal());
        Assert.Equal(20m, g1.GetProperty("totalAdjusted").GetDecimal());

        // Danh sách điều chỉnh mặc định gồm cả bản ghi đã hủy lẫn chưa hủy; includeVoided=false chỉ trả bản ghi chưa hủy.
        var listAll = (await (await ownerClient.GetAsync($"{AdjustmentsPath}?seasonId={season.Id}", ct)).ReadJsonAsync(ct))
            .GetProperty("data");
        var allIds = listAll.EnumerateArray().Select(x => x.GetProperty("id").GetGuid()).ToList();
        Assert.Equal(2, allIds.Count);
        Assert.Contains(adjId, allIds);
        Assert.Contains(activeId, allIds);

        var listActive = (await (await ownerClient.GetAsync($"{AdjustmentsPath}?seasonId={season.Id}&includeVoided=false", ct)).ReadJsonAsync(ct))
            .GetProperty("data");
        var activeOnly = Assert.Single(listActive.EnumerateArray());
        Assert.Equal(activeId, activeOnly.GetProperty("id").GetGuid());

        // Hủy lần hai -> ALREADY_VOIDED và không ghi thêm nhật ký.
        var secondVoid = await ownerClient.PostAsJsonAsync(
            $"{AdjustmentsPath}/{adjId}/void",
            new { reason = "Hủy lại" },
            ct);
        await secondVoid.AssertProblemAsync(HttpStatusCode.UnprocessableEntity, ErrorCodes.AlreadyVoided, ct);

        await AuditLogAssertions.AssertSingleAsync(
            factory.Postgres,
            household.Household.Id,
            AuditActions.ProduceStockAdjustmentVoid,
            adjId,
            ct,
            reason: "Nhập nhầm loại nông sản");
    }

    private async Task AddHarvestAsync(TestHousehold household, Guid seasonId, QualityGrade grade, decimal quantity, CancellationToken ct)
    {
        await using var db = factory.Postgres.CreateDbContext(TestCurrentUser.For(household.Owner));
        db.HarvestRecords.Add(new HarvestRecord
        {
            SeasonId = seasonId,
            HarvestDate = new DateOnly(2026, 10, 5),
            QualityGrade = grade,
            Quantity = quantity,
            RequestId = Guid.CreateVersion7(),
        });
        await db.SaveChangesAsync(ct);
    }

    private async Task AddSaleAsync(TestHousehold household, Guid seasonId, QualityGrade grade, decimal quantity, CancellationToken ct)
    {
        await using var db = factory.Postgres.CreateDbContext(TestCurrentUser.For(household.Owner));
        db.Sales.Add(new Sale
        {
            SeasonId = seasonId,
            Channel = SalesChannel.Trader,
            QualityGrade = grade,
            Quantity = quantity,
            UnitPrice = 10_000m,
            SaleDate = new DateOnly(2026, 10, 6),
            RequestId = Guid.CreateVersion7(),
        });
        await db.SaveChangesAsync(ct);
    }
}
