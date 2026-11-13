using System.Net;
using System.Net.Http.Json;
using HouseholdFarm.Api.Common.Errors;
using HouseholdFarm.Api.Common.Responses;
using HouseholdFarm.Api.Domain.Enums;
using HouseholdFarm.Api.Features.FarmTasks;
using HouseholdFarm.Api.Tests.Features.Supplies;
using HouseholdFarm.Api.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace HouseholdFarm.Api.Tests.Features.FarmTasks;

/// <summary>
/// Kiểm thử ghi kết quả công việc, xuất kho vật tư, thiếu vật tư, bổ sung kết quả và chế độ Nhân công
/// (FR-TSK-04..07, FR-TSK-10, BR-TSK-05..10, BR-USR-14, TC-03, TC-04, TC-20).
/// </summary>
public class FarmTaskRecordResultTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private const string TasksPath = "/api/farm-tasks";

    [Fact(DisplayName = "FR-TSK-04, FR-TSK-05, TC-04: Ghi kết quả Hoàn thành xuất kho vật tư và cảnh báo LOW_STOCK (thành viên không cần quyền Kho)")]
    public async Task RecordResult_DoneWithSupply_StocksOutAndWarnsLowStock()
    {
        var ct = TestContext.Current.CancellationToken;
        var household = await factory.CreateHouseholdAsync(ct);
        var plot = await TestData.CreatePlotAsync(factory.Postgres, household, "TD001", ct);
        var season = await TestData.CreateSeasonAsync(factory.Postgres, household, plot, SeasonStatus.InProgress, ct);
        await TestData.AssignMemberAsync(factory.Postgres, household, season, household.Member, ct);

        // TC-04: Tồn 100 kg, ngưỡng tối thiểu 30 kg
        var supply = await SupplyTestData.CreateSupplyAsync(
            factory.Postgres, household, "VT001", ct, name: "NPK 16-16-8", minStockLevel: 30m);
        await SupplyTestData.CreateTransactionAsync(
            factory.Postgres, household, supply, StockTransactionType.In, 100m, ct, unitCost: 20_000m);

        var task = await FarmTaskTestData.CreateTaskAsync(
            factory.Postgres, household, season, new DateOnly(2026, 10, 10), ct,
            assignee: household.Member,
            createdBy: household.Owner,
            supplies: (supply, 80m, null, null));

        // Thành viên (không có CanManageStock) ghi kết quả dùng 80 kg cho việc giao cho mình
        var memberClient = factory.CreateClientFor(household.Member);
        var performedDate = new DateOnly(2026, 10, 11);
        var request = new RecordFarmTaskResultRequest(
            FarmTaskStatus.Done,
            performedDate,
            75,
            "Lá hơi vàng nhẹ",
            "Nắng ráo buổi sáng",
            [new FarmTaskActualSupplyInput(supply.Id, 80m)],
            null,
            task.Version);

        var response = await memberClient.PostAsJsonAsync(
            $"{TasksPath}/{task.Id}/record-result",
            request,
            FarmTaskTestData.JsonOptions,
            ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var json = await response.ReadJsonAsync(ct);
        var data = json.GetProperty("data");
        Assert.Equal("Done", data.GetProperty("status").GetString());
        Assert.Equal("2026-10-11", data.GetProperty("performedDate").GetString());
        Assert.Equal(75, data.GetProperty("actualDurationMinutes").GetInt32());
        Assert.NotEqual(System.Text.Json.JsonValueKind.Null, data.GetProperty("completedAt").ValueKind);

        var usageItem = data.GetProperty("supplyUsages")[0];
        Assert.Equal(80m, usageItem.GetProperty("actualQuantity").GetDecimal());
        Assert.Equal(20m, usageItem.GetProperty("currentStock").GetDecimal());
        var stockTxId = usageItem.GetProperty("stockTransactionId").GetGuid();

        var warnings = json.GetProperty("warnings").EnumerateArray().ToList();
        Assert.Contains(warnings, w => w.GetProperty("code").GetString() == WarningCodes.LowStock);

        await using var db = factory.Postgres.CreateDbContext(TestCurrentUser.For(household.Owner));
        var tx = await db.StockTransactions.SingleAsync(t => t.Id == stockTxId, ct);
        Assert.Equal(StockTransactionType.Out, tx.Type);
        Assert.Equal(-80m, tx.Quantity);
        Assert.Equal(season.Id, tx.SeasonId);
        Assert.Equal(performedDate, tx.TransactionDate);
        Assert.NotEqual(Guid.Empty, tx.RequestId);
    }

    [Fact(DisplayName = "TC-03, BR-TSK-09, FR-TSK-06: Thiếu tồn kho rollback toàn bộ; cho phép chuyển WaitingForSupply hoặc âm kho khi hộ bật")]
    public async Task RecordResult_InsufficientStock_RollsBackAndSupportsWaitingOrNegativeStock()
    {
        var ct = TestContext.Current.CancellationToken;
        var household = await factory.CreateHouseholdAsync(ct);
        var plot = await TestData.CreatePlotAsync(factory.Postgres, household, "TD001", ct);
        var season = await TestData.CreateSeasonAsync(factory.Postgres, household, plot, SeasonStatus.InProgress, ct);

        // TC-03: tồn 5 kg, hộ không bật âm kho
        var supply = await SupplyTestData.CreateSupplyAsync(factory.Postgres, household, "VT001", ct);
        await SupplyTestData.CreateTransactionAsync(
            factory.Postgres, household, supply, StockTransactionType.In, 5m, ct, unitCost: 10_000m);

        var task = await FarmTaskTestData.CreateTaskAsync(
            factory.Postgres, household, season, new DateOnly(2026, 10, 10), ct,
            supplies: (supply, 8m, null, null));

        var ownerClient = factory.CreateClientFor(household.Owner);
        var insufficientReq = new RecordFarmTaskResultRequest(
            FarmTaskStatus.Done,
            new DateOnly(2026, 10, 10),
            60,
            "Thiếu phân",
            null,
            [new FarmTaskActualSupplyInput(supply.Id, 8m)],
            null,
            task.Version);

        var insufficientRes = await ownerClient.PostAsJsonAsync(
            $"{TasksPath}/{task.Id}/record-result",
            insufficientReq,
            FarmTaskTestData.JsonOptions,
            ct);
        var problem = await insufficientRes.AssertProblemAsync(HttpStatusCode.UnprocessableEntity, ErrorCodes.InsufficientStock, ct);
        var details = problem.GetProperty("details");
        Assert.Equal(supply.Id, details.GetProperty("supplyId").GetGuid());
        Assert.Equal(5m, details.GetProperty("available").GetDecimal());
        Assert.Equal(8m, details.GetProperty("requested").GetDecimal());

        // Kiểm tra DB không có gì thay đổi sau khi rollback
        await using (var db = factory.Postgres.CreateDbContext(TestCurrentUser.For(household.Owner)))
        {
            var reloaded = await db.FarmTasks.Include(t => t.SupplyUsages).SingleAsync(t => t.Id == task.Id, ct);
            Assert.Equal(FarmTaskStatus.Pending, reloaded.Status);
            Assert.Null(reloaded.PerformedDate);
            Assert.Null(reloaded.SupplyUsages[0].StockTransactionId);
            Assert.Equal(1, await db.StockTransactions.CountAsync(t => t.SupplyId == supply.Id, ct));
        }

        // Lựa chọn (1): chuyển công việc sang Chờ bổ sung vật tư
        var waitingRes = await ownerClient.PostAsJsonAsync(
            $"{TasksPath}/{task.Id}/record-result",
            new RecordFarmTaskResultRequest(
                FarmTaskStatus.WaitingForSupply,
                null,
                null,
                null,
                "Chờ nhập thêm phân",
                [new FarmTaskActualSupplyInput(supply.Id, null)],
                null,
                task.Version),
            FarmTaskTestData.JsonOptions,
            ct);
        Assert.Equal(HttpStatusCode.OK, waitingRes.StatusCode);
        var waitingData = (await waitingRes.ReadJsonAsync(ct)).GetProperty("data");
        Assert.Equal("WaitingForSupply", waitingData.GetProperty("status").GetString());
        var waitingVersion = waitingData.GetProperty("version").GetUInt32();

        // Bật âm kho -> ghi dùng 8 kg thành công với cảnh báo NEGATIVE_STOCK, tồn còn -3 kg
        await SupplyTestData.SetAllowNegativeStockAsync(factory.Postgres, household, true, ct);
        var negativeRes = await ownerClient.PostAsJsonAsync(
            $"{TasksPath}/{task.Id}/record-result",
            insufficientReq with { Version = waitingVersion },
            FarmTaskTestData.JsonOptions,
            ct);
        Assert.Equal(HttpStatusCode.OK, negativeRes.StatusCode);
        var negativeJson = await negativeRes.ReadJsonAsync(ct);
        Assert.Equal(-3m, negativeJson.GetProperty("data").GetProperty("supplyUsages")[0].GetProperty("currentStock").GetDecimal());
        var warnings = negativeJson.GetProperty("warnings").EnumerateArray().ToList();
        Assert.Contains(warnings, w => w.GetProperty("code").GetString() == WarningCodes.NegativeStock);
    }

    [Fact(DisplayName = "FR-TSK-07, BR-TSK-08, BR-TSK-10: Bổ sung sau hoàn thành, chống xuất trùng, sửa/bỏ vật tư bắt buộc lý do và Void giao dịch cũ")]
    public async Task RecordResult_SupplementAfterDone_VoidAndReissueSuppliesAndPreserveStockTxDate()
    {
        var ct = TestContext.Current.CancellationToken;
        var household = await factory.CreateHouseholdAsync(ct);
        var plot = await TestData.CreatePlotAsync(factory.Postgres, household, "TD001", ct);
        var season = await TestData.CreateSeasonAsync(factory.Postgres, household, plot, SeasonStatus.InProgress, ct);
        var supplyA = await SupplyTestData.CreateSupplyAsync(factory.Postgres, household, "VT001", ct);
        var supplyB = await SupplyTestData.CreateSupplyAsync(factory.Postgres, household, "VT002", ct);
        await SupplyTestData.CreateTransactionAsync(factory.Postgres, household, supplyA, StockTransactionType.In, 50m, ct, unitCost: 10_000m);
        await SupplyTestData.CreateTransactionAsync(factory.Postgres, household, supplyB, StockTransactionType.In, 50m, ct, unitCost: 20_000m);

        var task = await FarmTaskTestData.CreateTaskAsync(
            factory.Postgres, household, season, new DateOnly(2026, 10, 10), ct,
            supplies: (supplyA, 10m, null, null));

        var ownerClient = factory.CreateClientFor(household.Owner);

        // Bước 1: Hoàn thành dùng 10 kg supplyA và thêm 5 kg supplyB (không nằm trong dự kiến)
        var initialDate = new DateOnly(2026, 10, 11);
        var step1Res = await ownerClient.PostAsJsonAsync(
            $"{TasksPath}/{task.Id}/record-result",
            new RecordFarmTaskResultRequest(
                FarmTaskStatus.Done,
                initialDate,
                60,
                null,
                "Ghi nhận lần đầu",
                [new FarmTaskActualSupplyInput(supplyA.Id, 10m), new FarmTaskActualSupplyInput(supplyB.Id, 5m)],
                null,
                task.Version),
            FarmTaskTestData.JsonOptions,
            ct);
        Assert.Equal(HttpStatusCode.OK, step1Res.StatusCode);
        var step1Data = (await step1Res.ReadJsonAsync(ct)).GetProperty("data");
        var v1 = step1Data.GetProperty("version").GetUInt32();
        var txA1 = step1Data.GetProperty("supplyUsages").EnumerateArray()
            .Single(u => u.GetProperty("supplyId").GetGuid() == supplyA.Id)
            .GetProperty("stockTransactionId").GetGuid();

        // Bước 2: Gửi lại đúng lượng 10 kg supplyA và 5 kg supplyB, chỉ đổi fieldNote -> không sinh thêm giao dịch xuất mới (BR-TSK-08)
        var step2Res = await ownerClient.PostAsJsonAsync(
            $"{TasksPath}/{task.Id}/record-result",
            new RecordFarmTaskResultRequest(
                FarmTaskStatus.Done,
                initialDate,
                65,
                null,
                "Bổ sung ghi chú",
                [new FarmTaskActualSupplyInput(supplyA.Id, 10m), new FarmTaskActualSupplyInput(supplyB.Id, 5m)],
                null,
                v1),
            FarmTaskTestData.JsonOptions,
            ct);
        Assert.Equal(HttpStatusCode.OK, step2Res.StatusCode);
        var step2Data = (await step2Res.ReadJsonAsync(ct)).GetProperty("data");
        var v2 = step2Data.GetProperty("version").GetUInt32();
        var txA2 = step2Data.GetProperty("supplyUsages").EnumerateArray()
            .Single(u => u.GetProperty("supplyId").GetGuid() == supplyA.Id)
            .GetProperty("stockTransactionId").GetGuid();
        Assert.Equal(txA1, txA2);

        // Bước 3: Sửa lượng supplyA (10 -> 6) mà không có lý do -> VALIDATION_ERROR
        var noReasonRes = await ownerClient.PostAsJsonAsync(
            $"{TasksPath}/{task.Id}/record-result",
            new RecordFarmTaskResultRequest(
                FarmTaskStatus.Done,
                initialDate,
                65,
                null,
                "Bổ sung ghi chú",
                [new FarmTaskActualSupplyInput(supplyA.Id, 6m), new FarmTaskActualSupplyInput(supplyB.Id, 5m)],
                null,
                v2),
            FarmTaskTestData.JsonOptions,
            ct);
        await noReasonRes.AssertProblemAsync(HttpStatusCode.BadRequest, ErrorCodes.ValidationError, ct);

        // Bước 4: Sửa supplyA (10 -> 6), bỏ hẳn supplyB và sửa performedDate (2026-10-12) có lý do -> Void cả 2 tx cũ, tạo tx mới -6 cho A
        var step4Res = await ownerClient.PostAsJsonAsync(
            $"{TasksPath}/{task.Id}/record-result",
            new RecordFarmTaskResultRequest(
                FarmTaskStatus.Done,
                new DateOnly(2026, 10, 12),
                65,
                null,
                "Nhập nhầm lượng phân",
                [new FarmTaskActualSupplyInput(supplyA.Id, 6m)],
                "Nhập sai số lượng thực tế",
                v2),
            FarmTaskTestData.JsonOptions,
            ct);
        Assert.Equal(HttpStatusCode.OK, step4Res.StatusCode);
        var step4Data = (await step4Res.ReadJsonAsync(ct)).GetProperty("data");
        Assert.NotEqual(System.Text.Json.JsonValueKind.Null, step4Data.GetProperty("updatedAt").ValueKind);
        var usagesAfter = step4Data.GetProperty("supplyUsages").EnumerateArray().ToList();
        Assert.Single(usagesAfter);
        Assert.Equal(supplyA.Id, usagesAfter[0].GetProperty("supplyId").GetGuid());
        Assert.Equal(6m, usagesAfter[0].GetProperty("actualQuantity").GetDecimal());
        Assert.Equal(44m, usagesAfter[0].GetProperty("currentStock").GetDecimal());

        await using var db = factory.Postgres.CreateDbContext(TestCurrentUser.For(household.Owner));
        var oldTxA = await db.StockTransactions.SingleAsync(t => t.Id == txA1, ct);
        Assert.NotNull(oldTxA.VoidedAt);
        Assert.Equal("Nhập sai số lượng thực tế", oldTxA.VoidReason);
        Assert.Equal(initialDate, oldTxA.TransactionDate);
        Assert.Equal(1, await db.FarmTaskSupplyUsages.CountAsync(u => u.FarmTaskId == task.Id, ct));
    }

    [Fact(DisplayName = "BR-TSK-05, BR-TSK-06, BR-SEA-10: Kiểm tra điều kiện trạng thái công việc và mùa vụ khi ghi kết quả")]
    public async Task RecordResult_InvalidStates_Rejected()
    {
        var ct = TestContext.Current.CancellationToken;
        var household = await factory.CreateHouseholdAsync(ct);
        var plot = await TestData.CreatePlotAsync(factory.Postgres, household, "TD001", ct);
        var plannedSeason = await TestData.CreateSeasonAsync(factory.Postgres, household, plot, SeasonStatus.Planned, ct);
        var activeSeason = await TestData.CreateSeasonAsync(factory.Postgres, household, plot, SeasonStatus.InProgress, ct);

        var plannedSeasonTask = await FarmTaskTestData.CreateTaskAsync(
            factory.Postgres, household, plannedSeason, new DateOnly(2026, 10, 5), ct);
        var doneTask = await FarmTaskTestData.CreateTaskAsync(
            factory.Postgres, household, activeSeason, new DateOnly(2026, 10, 6), ct,
            status: FarmTaskStatus.Done);
        var cancelledTask = await FarmTaskTestData.CreateTaskAsync(
            factory.Postgres, household, activeSeason, new DateOnly(2026, 10, 7), ct,
            status: FarmTaskStatus.Cancelled);

        var ownerClient = factory.CreateClientFor(household.Owner);

        // Mùa vụ Planned không được ghi kết quả -> SEASON_INVALID_STATE
        var plannedRes = await ownerClient.PostAsJsonAsync(
            $"{TasksPath}/{plannedSeasonTask.Id}/record-result",
            new RecordFarmTaskResultRequest(FarmTaskStatus.Done, new DateOnly(2026, 10, 5), null, null, null, null, null, plannedSeasonTask.Version),
            FarmTaskTestData.JsonOptions,
            ct);
        await plannedRes.AssertProblemAsync(HttpStatusCode.UnprocessableEntity, ErrorCodes.SeasonInvalidState, ct);

        // Done không quay về WaitingForSupply -> TASK_INVALID_STATE
        var revertDoneRes = await ownerClient.PostAsJsonAsync(
            $"{TasksPath}/{doneTask.Id}/record-result",
            new RecordFarmTaskResultRequest(FarmTaskStatus.WaitingForSupply, null, null, null, null, null, null, doneTask.Version),
            FarmTaskTestData.JsonOptions,
            ct);
        await revertDoneRes.AssertProblemAsync(HttpStatusCode.UnprocessableEntity, ErrorCodes.TaskInvalidState, ct);

        // Cancelled không ghi được kết quả -> TASK_INVALID_STATE
        var cancelledRes = await ownerClient.PostAsJsonAsync(
            $"{TasksPath}/{cancelledTask.Id}/record-result",
            new RecordFarmTaskResultRequest(FarmTaskStatus.Done, new DateOnly(2026, 10, 7), null, null, null, null, null, cancelledTask.Version),
            FarmTaskTestData.JsonOptions,
            ct);
        await cancelledRes.AssertProblemAsync(HttpStatusCode.UnprocessableEntity, ErrorCodes.TaskInvalidState, ct);

        // Done thiếu performedDate -> VALIDATION_ERROR (BR-TSK-05)
        var pendingTask = await FarmTaskTestData.CreateTaskAsync(
            factory.Postgres, household, activeSeason, new DateOnly(2026, 10, 8), ct);
        var missingDateRes = await ownerClient.PostAsJsonAsync(
            $"{TasksPath}/{pendingTask.Id}/record-result",
            new RecordFarmTaskResultRequest(FarmTaskStatus.Done, null, null, null, null, null, null, pendingTask.Version),
            FarmTaskTestData.JsonOptions,
            ct);
        await missingDateRes.AssertProblemAsync(HttpStatusCode.BadRequest, ErrorCodes.ValidationError, ct);
    }

    [Fact(DisplayName = "FR-TSK-10, BR-TSK-07, BR-USR-14, TC-20(b): Nhân công chỉ xem việc mình, JSON bỏ trường tồn kho/người tạo, và không ghi được vật tư")]
    public async Task WorkerAndMemberScope_EnforcesBrUsr14AndBrTsk07()
    {
        var ct = TestContext.Current.CancellationToken;
        var household = await factory.CreateHouseholdAsync(ct);
        var plot = await TestData.CreatePlotAsync(factory.Postgres, household, "TD001", ct);
        var season = await TestData.CreateSeasonAsync(factory.Postgres, household, plot, SeasonStatus.InProgress, ct);
        await TestData.AssignMemberAsync(factory.Postgres, household, season, household.Member, ct);
        await TestData.AssignMemberAsync(factory.Postgres, household, season, household.Worker, ct);

        var supply = await SupplyTestData.CreateSupplyAsync(factory.Postgres, household, "VT001", ct);

        var workerTask = await FarmTaskTestData.CreateTaskAsync(
            factory.Postgres, household, season, new DateOnly(2026, 10, 10), ct,
            assignee: household.Worker,
            createdBy: household.Owner,
            supplies: (supply, 5m, null, null));

        var ownerTask = await FarmTaskTestData.CreateTaskAsync(
            factory.Postgres, household, season, new DateOnly(2026, 10, 11), ct,
            assignee: household.Owner,
            createdBy: household.Owner);

        var workerClient = factory.CreateClientFor(household.Worker);

        // TC-20(b): Nhân công xem công việc giao cho người khác -> NOT_FOUND
        var otherTaskRes = await workerClient.GetAsync($"{TasksPath}/{ownerTask.Id}", ct);
        await otherTaskRes.AssertProblemAsync(HttpStatusCode.NotFound, ErrorCodes.NotFound, ct);

        // BR-USR-14: Nhân công xem việc của mình -> không chứa các trường tồn kho, người tạo, giao dịch kho
        var getRes = await workerClient.GetAsync($"{TasksPath}/{workerTask.Id}", ct);
        Assert.Equal(HttpStatusCode.OK, getRes.StatusCode);
        var detail = (await getRes.ReadJsonAsync(ct)).GetProperty("data");
        Assert.False(detail.TryGetProperty("createdById", out _));
        Assert.False(detail.TryGetProperty("createdByName", out _));
        var workerSupply = detail.GetProperty("supplyUsages")[0];
        Assert.False(workerSupply.TryGetProperty("currentStock", out _));
        Assert.False(workerSupply.TryGetProperty("actualQuantity", out _));
        Assert.False(workerSupply.TryGetProperty("stockTransactionId", out _));

        // Thành viên (không phải người tạo và không phải người được giao) ghi kết quả việc của Worker -> FORBIDDEN
        var memberClient = factory.CreateClientFor(household.Member);
        var memberForbiddenRes = await memberClient.PostAsJsonAsync(
            $"{TasksPath}/{workerTask.Id}/record-result",
            new RecordFarmTaskResultRequest(FarmTaskStatus.Done, new DateOnly(2026, 10, 10), null, null, null, null, null, workerTask.Version),
            FarmTaskTestData.JsonOptions,
            ct);
        await memberForbiddenRes.AssertProblemAsync(HttpStatusCode.Forbidden, ErrorCodes.Forbidden, ct);

        // BR-TSK-07: Nhân công cố gửi supplies -> FORBIDDEN
        var workerSupplyRes = await workerClient.PostAsJsonAsync(
            $"{TasksPath}/{workerTask.Id}/record-result",
            new RecordFarmTaskResultRequest(
                FarmTaskStatus.Done,
                new DateOnly(2026, 10, 10),
                null,
                null,
                null,
                [new FarmTaskActualSupplyInput(supply.Id, 2m)],
                null,
                workerTask.Version),
            FarmTaskTestData.JsonOptions,
            ct);
        await workerSupplyRes.AssertProblemAsync(HttpStatusCode.Forbidden, ErrorCodes.Forbidden, ct);

        // Nhân công ghi hoàn thành hợp lệ (không supplies) -> 200 OK và JSON vẫn bỏ trường tồn kho/người tạo
        var workerOkRes = await workerClient.PostAsJsonAsync(
            $"{TasksPath}/{workerTask.Id}/record-result",
            new RecordFarmTaskResultRequest(
                FarmTaskStatus.Done,
                new DateOnly(2026, 10, 10),
                50,
                "Cỏ mọc dày ở góc đông",
                "Đất đủ ẩm",
                null,
                null,
                workerTask.Version),
            FarmTaskTestData.JsonOptions,
            ct);
        Assert.Equal(HttpStatusCode.OK, workerOkRes.StatusCode);
        var workerOkData = (await workerOkRes.ReadJsonAsync(ct)).GetProperty("data");
        Assert.Equal("Done", workerOkData.GetProperty("status").GetString());
        Assert.False(workerOkData.TryGetProperty("createdById", out _));
    }
}
