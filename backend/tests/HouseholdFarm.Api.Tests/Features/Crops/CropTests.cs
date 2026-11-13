using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using HouseholdFarm.Api.Common.Errors;
using HouseholdFarm.Api.Domain.Entities;
using HouseholdFarm.Api.Domain.Enums;
using HouseholdFarm.Api.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace HouseholdFarm.Api.Tests.Features.Crops;

/// <summary>
/// Tạo, xem, sửa cây trồng (FR-CROP-01), khóa đơn vị sản lượng (FR-CROP-02), ngừng sử dụng cây (FR-CROP-03).
/// </summary>
public class CropTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private const string CropsPath = "/api/crops";

    [Fact(DisplayName = "FR-CROP-01: tạo cây lưu đủ các trường, cây mới Active, chưa khóa đơn vị; ghi chú nhóm chỉ giữ khi chọn Khác")]
    public async Task Create_ValidInput_ReturnsCreatedCrop()
    {
        var ct = TestContext.Current.CancellationToken;
        var household = await factory.CreateHouseholdAsync(ct);
        var client = factory.CreateClientFor(household.Owner);

        var response = await client.PostAsJsonAsync(
            CropsPath,
            new
            {
                name = "  Lúa ST25  ",
                group = "Staple",
                groupOther = "Bị bỏ qua vì không chọn Khác",
                yieldUnit = "Kilogram",
                referenceInfo = " Gieo sạ 100 kg/ha ",
            },
            ct);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var data = (await response.ReadJsonAsync(ct)).GetProperty("data");
        Assert.Equal("Lúa ST25", data.GetProperty("name").GetString());
        Assert.Equal("Staple", data.GetProperty("group").GetString());
        Assert.Equal(JsonValueKind.Null, data.GetProperty("groupOther").ValueKind);
        Assert.Equal("Kilogram", data.GetProperty("yieldUnit").GetString());
        Assert.Equal("Gieo sạ 100 kg/ha", data.GetProperty("referenceInfo").GetString());
        Assert.Equal("Active", data.GetProperty("status").GetString());
        Assert.False(data.GetProperty("yieldUnitLocked").GetBoolean());
        Assert.True(data.GetProperty("version").GetUInt32() > 0);
        Assert.Equal($"{CropsPath}/{data.GetProperty("id").GetGuid()}", response.Headers.Location!.AbsolutePath);

        var other = await client.PostAsJsonAsync(
            CropsPath,
            new { name = "Sen", group = "Other", groupOther = "Cây thủy sinh", yieldUnit = "Bunch" },
            ct);
        Assert.Equal(
            "Cây thủy sinh",
            (await other.ReadJsonAsync(ct)).GetProperty("data").GetProperty("groupOther").GetString());
    }

    [Fact(DisplayName = "BR-CAT-03: tạo cây trùng tên trong hộ trả NAME_ALREADY_USED; hộ khác dùng cùng tên được")]
    public async Task Create_WithUsedName_ReturnsNameAlreadyUsed()
    {
        var ct = TestContext.Current.CancellationToken;
        var household = await factory.CreateHouseholdAsync(ct);
        var otherHousehold = await factory.CreateHouseholdAsync(ct);
        await TestData.CreateCropAsync(factory.Postgres, household, "Lúa ST25", ct);

        var duplicate = await factory.CreateClientFor(household.Owner).PostAsJsonAsync(CropsPath, NewCrop(" Lúa ST25 "), ct);
        var otherHouseholdSameName = await factory.CreateClientFor(otherHousehold.Owner)
            .PostAsJsonAsync(CropsPath, NewCrop("Lúa ST25"), ct);

        var problem = await duplicate.AssertProblemAsync(HttpStatusCode.Conflict, ErrorCodes.NameAlreadyUsed, ct);
        Assert.Contains("Lúa ST25", problem.GetProperty("detail").GetString());
        Assert.Equal(HttpStatusCode.Created, otherHouseholdSameName.StatusCode);
    }

    [Fact(DisplayName = "BR-CAT-03: nhiều yêu cầu tạo cùng một tên cây cùng lúc chỉ một yêu cầu thành công, còn lại NAME_ALREADY_USED")]
    public async Task Create_SameNameConcurrently_OnlyOneSucceeds()
    {
        var ct = TestContext.Current.CancellationToken;
        var household = await factory.CreateHouseholdAsync(ct);
        var client = factory.CreateClientFor(household.Owner);

        var responses = await Task.WhenAll(
            Enumerable.Range(0, 4).Select(_ => client.PostAsJsonAsync(CropsPath, NewCrop("Bắp nếp"), ct)));

        Assert.Single(responses, r => r.StatusCode == HttpStatusCode.Created);
        foreach (var failed in responses.Where(r => r.StatusCode != HttpStatusCode.Created))
        {
            await failed.AssertProblemAsync(HttpStatusCode.Conflict, ErrorCodes.NameAlreadyUsed, ct);
        }
    }

    [Fact(DisplayName = "Ma trận 2.5: Thành viên hộ, Nhân công tạo, sửa cây trả FORBIDDEN")]
    public async Task CreateOrUpdate_NotOwner_ReturnsForbidden()
    {
        var ct = TestContext.Current.CancellationToken;
        var household = await factory.CreateHouseholdAsync(ct);
        var crop = await TestData.CreateCropAsync(factory.Postgres, household, "Lúa ST25", ct);

        foreach (var user in new[] { household.MemberWithAllPermissions, household.Worker })
        {
            var client = factory.CreateClientFor(user);

            await (await client.PostAsJsonAsync(CropsPath, NewCrop("Ngô"), ct))
                .AssertProblemAsync(HttpStatusCode.Forbidden, ErrorCodes.Forbidden, ct);
            await (await client.PutAsJsonAsync($"{CropsPath}/{crop.Id}", UpdateOf("Lúa ST24", crop.Version), ct))
                .AssertProblemAsync(HttpStatusCode.Forbidden, ErrorCodes.Forbidden, ct);
        }
    }

    [Fact(DisplayName = "TC-10: (a) xem, sửa cây của hộ B bằng id trả NOT_FOUND; (b) gửi householdId của hộ B thì cây vẫn tạo trong hộ A")]
    public async Task TC10_AccessOtherHouseholdCrop_ReturnsNotFoundAndCreatesInOwnHousehold()
    {
        var ct = TestContext.Current.CancellationToken;
        var householdA = await factory.CreateHouseholdAsync(ct);
        var householdB = await factory.CreateHouseholdAsync(ct);
        var cropB = await TestData.CreateCropAsync(factory.Postgres, householdB, "Lúa ST25", ct);
        var client = factory.CreateClientFor(householdA.Owner);

        await (await client.GetAsync($"{CropsPath}/{cropB.Id}", ct))
            .AssertProblemAsync(HttpStatusCode.NotFound, ErrorCodes.NotFound, ct);
        await (await client.PutAsJsonAsync($"{CropsPath}/{cropB.Id}", UpdateOf("Lúa B", cropB.Version), ct))
            .AssertProblemAsync(HttpStatusCode.NotFound, ErrorCodes.NotFound, ct);

        var created = await client.PostAsJsonAsync(
            CropsPath,
            new { householdId = householdB.Household.Id, name = "Cà chua", group = "Vegetable", yieldUnit = "Kilogram" },
            ct);

        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var id = (await created.ReadJsonAsync(ct)).GetProperty("data").GetProperty("id").GetGuid();
        await using var db = factory.Postgres.CreateDbContext();
        var saved = await db.Crops.IgnoreQueryFilters().SingleAsync(c => c.Id == id, ct);
        Assert.Equal(householdA.Household.Id, saved.HouseholdId);
        Assert.Equal(householdA.Owner.Id, saved.CreatedById);
    }

    [Fact(DisplayName = "FR-CROP-01: sửa cây lưu đủ các trường, trả version mới")]
    public async Task Update_WithCurrentVersion_SavesChanges()
    {
        var ct = TestContext.Current.CancellationToken;
        var household = await factory.CreateHouseholdAsync(ct);
        var crop = await TestData.CreateCropAsync(factory.Postgres, household, "Lúa ST25", ct);

        var response = await factory.CreateClientFor(household.Owner).PutAsJsonAsync(
            $"{CropsPath}/{crop.Id}",
            new
            {
                name = " Xoài cát Hòa Lộc ",
                group = "FruitTree",
                yieldUnit = "Ton",
                referenceInfo = "Tỉa cành sau thu hoạch",
                status = "Active",
                version = crop.Version,
            },
            ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var data = (await response.ReadJsonAsync(ct)).GetProperty("data");
        Assert.Equal("Xoài cát Hòa Lộc", data.GetProperty("name").GetString());
        Assert.Equal("FruitTree", data.GetProperty("group").GetString());
        Assert.Equal("Ton", data.GetProperty("yieldUnit").GetString());
        Assert.Equal("Tỉa cành sau thu hoạch", data.GetProperty("referenceInfo").GetString());
        Assert.NotEqual(crop.Version, data.GetProperty("version").GetUInt32());

        await using var db = factory.Postgres.CreateDbContext();
        var saved = await db.Crops.IgnoreQueryFilters().SingleAsync(c => c.Id == crop.Id, ct);
        Assert.Equal(Unit.Ton, saved.YieldUnit);
        Assert.Equal(household.Owner.Id, saved.UpdatedById);
    }

    [Fact(DisplayName = "BR-CAT-03: sửa sang tên của cây khác trong hộ trả NAME_ALREADY_USED")]
    public async Task Update_ToUsedName_ReturnsNameAlreadyUsed()
    {
        var ct = TestContext.Current.CancellationToken;
        var household = await factory.CreateHouseholdAsync(ct);
        await TestData.CreateCropAsync(factory.Postgres, household, "Lúa ST25", ct);
        var crop = await TestData.CreateCropAsync(factory.Postgres, household, "Lúa OM18", ct);

        var response = await factory.CreateClientFor(household.Owner)
            .PutAsJsonAsync($"{CropsPath}/{crop.Id}", UpdateOf("Lúa ST25", crop.Version), ct);

        await response.AssertProblemAsync(HttpStatusCode.Conflict, ErrorCodes.NameAlreadyUsed, ct);
    }

    [Fact(DisplayName = "BR-GEN-11: sửa bằng version cũ (người khác đã sửa trước) trả CONCURRENCY_CONFLICT")]
    public async Task Update_WithStaleVersion_ReturnsConcurrencyConflict()
    {
        var ct = TestContext.Current.CancellationToken;
        var household = await factory.CreateHouseholdAsync(ct);
        var crop = await TestData.CreateCropAsync(factory.Postgres, household, "Lúa ST25", ct);
        var client = factory.CreateClientFor(household.Owner);
        // Lần sửa đầu phải đổi giá trị: gửi y nguyên thì EF không ghi gì và version không đổi.
        Assert.Equal(
            HttpStatusCode.OK,
            (await client.PutAsJsonAsync($"{CropsPath}/{crop.Id}", UpdateOf("Lúa ST24", crop.Version), ct)).StatusCode);

        var response = await client.PutAsJsonAsync($"{CropsPath}/{crop.Id}", UpdateOf("Lúa ST26", crop.Version), ct);

        await response.AssertProblemAsync(HttpStatusCode.Conflict, ErrorCodes.ConcurrencyConflict, ct);
    }

    [Fact(DisplayName = "FR-CROP-02: cây chưa có phiếu thu hoạch đổi được đơn vị sản lượng")]
    public async Task Update_ChangeUnitWithoutHarvest_Succeeds()
    {
        var ct = TestContext.Current.CancellationToken;
        var household = await factory.CreateHouseholdAsync(ct);
        var crop = await TestData.CreateCropAsync(factory.Postgres, household, "Lúa ST25", ct);
        var plot = await TestData.CreatePlotAsync(factory.Postgres, household, "TD001", ct);
        await TestData.CreateSeasonAsync(factory.Postgres, household, plot, SeasonStatus.InProgress, ct, crop: crop);

        var response = await factory.CreateClientFor(household.Owner)
            .PutAsJsonAsync($"{CropsPath}/{crop.Id}", UpdateOf("Lúa ST25", crop.Version, yieldUnit: "Quintal"), ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var data = (await response.ReadJsonAsync(ct)).GetProperty("data");
        Assert.Equal("Quintal", data.GetProperty("yieldUnit").GetString());
        Assert.False(data.GetProperty("yieldUnitLocked").GetBoolean());
    }

    [Theory(DisplayName = "BR-CAT-05: cây đã có phiếu thu hoạch (kể cả phiếu đã hủy) đổi đơn vị trả UNIT_LOCKED, yieldUnitLocked = true")]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Update_ChangeUnitWithHarvest_ReturnsUnitLocked(bool voided)
    {
        var ct = TestContext.Current.CancellationToken;
        var household = await factory.CreateHouseholdAsync(ct);
        var crop = await TestData.CreateCropAsync(factory.Postgres, household, "Lúa ST25", ct);
        await CreateHarvestRecordAsync(household, crop, voided, ct);
        var client = factory.CreateClientFor(household.Owner);

        var detail = (await (await client.GetAsync($"{CropsPath}/{crop.Id}", ct)).ReadJsonAsync(ct)).GetProperty("data");
        Assert.True(detail.GetProperty("yieldUnitLocked").GetBoolean());

        var response = await client.PutAsJsonAsync(
            $"{CropsPath}/{crop.Id}",
            UpdateOf("Lúa ST25", crop.Version, yieldUnit: "Ton"),
            ct);

        var problem = await response.AssertProblemAsync(HttpStatusCode.UnprocessableEntity, ErrorCodes.UnitLocked, ct);
        Assert.Contains("Lúa ST25", problem.GetProperty("detail").GetString());
        await using var db = factory.Postgres.CreateDbContext();
        Assert.Equal(Unit.Kilogram, (await db.Crops.IgnoreQueryFilters().SingleAsync(c => c.Id == crop.Id, ct)).YieldUnit);
    }

    [Fact(DisplayName = "BR-CAT-05: cây đã có phiếu thu hoạch vẫn sửa được các trường khác khi giữ nguyên đơn vị")]
    public async Task Update_KeepUnitWithHarvest_SucceedsAndStaysLocked()
    {
        var ct = TestContext.Current.CancellationToken;
        var household = await factory.CreateHouseholdAsync(ct);
        var crop = await TestData.CreateCropAsync(factory.Postgres, household, "Lúa ST25", ct);
        await CreateHarvestRecordAsync(household, crop, voided: false, ct);

        var response = await factory.CreateClientFor(household.Owner)
            .PutAsJsonAsync($"{CropsPath}/{crop.Id}", UpdateOf("Lúa ST25 vụ đông", crop.Version), ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var data = (await response.ReadJsonAsync(ct)).GetProperty("data");
        Assert.Equal("Lúa ST25 vụ đông", data.GetProperty("name").GetString());
        Assert.True(data.GetProperty("yieldUnitLocked").GetBoolean());
    }

    [Theory(DisplayName = "BR-CAT-04: ngừng sử dụng cây đang có mùa vụ Lên kế hoạch hoặc Đang canh tác trả CATALOG_IN_USE")]
    [InlineData(SeasonStatus.Planned)]
    [InlineData(SeasonStatus.InProgress)]
    public async Task Deactivate_WithRunningSeason_ReturnsCatalogInUse(SeasonStatus status)
    {
        var ct = TestContext.Current.CancellationToken;
        var household = await factory.CreateHouseholdAsync(ct);
        var crop = await TestData.CreateCropAsync(factory.Postgres, household, "Lúa ST25", ct);
        var plot = await TestData.CreatePlotAsync(factory.Postgres, household, "TD001", ct);
        await TestData.CreateSeasonAsync(factory.Postgres, household, plot, status, ct, crop: crop);

        var response = await factory.CreateClientFor(household.Owner)
            .PutAsJsonAsync($"{CropsPath}/{crop.Id}", UpdateOf("Lúa ST25", crop.Version, "Inactive"), ct);

        var problem = await response.AssertProblemAsync(HttpStatusCode.UnprocessableEntity, ErrorCodes.CatalogInUse, ct);
        Assert.Contains("Lúa ST25", problem.GetProperty("detail").GetString());
    }

    [Fact(DisplayName = "FR-CROP-03: cây chỉ có mùa vụ đã chốt, đã hủy thì ngừng sử dụng được, rồi dùng lại được")]
    public async Task Deactivate_WithoutRunningSeason_SucceedsAndCanReactivate()
    {
        var ct = TestContext.Current.CancellationToken;
        var household = await factory.CreateHouseholdAsync(ct);
        var crop = await TestData.CreateCropAsync(factory.Postgres, household, "Lúa ST25", ct);
        var plot = await TestData.CreatePlotAsync(factory.Postgres, household, "TD001", ct);
        await TestData.CreateSeasonAsync(factory.Postgres, household, plot, SeasonStatus.Closed, ct, crop: crop);
        await TestData.CreateSeasonAsync(factory.Postgres, household, plot, SeasonStatus.Cancelled, ct, crop: crop);
        var client = factory.CreateClientFor(household.Owner);

        var deactivated = await client.PutAsJsonAsync($"{CropsPath}/{crop.Id}", UpdateOf("Lúa ST25", crop.Version, "Inactive"), ct);
        Assert.Equal(HttpStatusCode.OK, deactivated.StatusCode);
        var data = (await deactivated.ReadJsonAsync(ct)).GetProperty("data");
        Assert.Equal("Inactive", data.GetProperty("status").GetString());

        var reactivated = await client.PutAsJsonAsync(
            $"{CropsPath}/{crop.Id}",
            UpdateOf("Lúa ST25", data.GetProperty("version").GetUInt32(), "Active"),
            ct);
        Assert.Equal("Active", (await reactivated.ReadJsonAsync(ct)).GetProperty("data").GetProperty("status").GetString());
    }

    [Fact(DisplayName = "FR-CROP-03: không có API xóa cây trồng")]
    public async Task Delete_IsNotSupported()
    {
        var ct = TestContext.Current.CancellationToken;
        var household = await factory.CreateHouseholdAsync(ct);
        var crop = await TestData.CreateCropAsync(factory.Postgres, household, "Lúa ST25", ct);

        var response = await factory.CreateClientFor(household.Owner).DeleteAsync($"{CropsPath}/{crop.Id}", ct);

        Assert.Equal(HttpStatusCode.MethodNotAllowed, response.StatusCode);
        await using var db = factory.Postgres.CreateDbContext();
        Assert.True(await db.Crops.IgnoreQueryFilters().AnyAsync(c => c.Id == crop.Id, ct));
    }

    [Fact(DisplayName = "Ma trận 2.5: Thành viên hộ xem được chi tiết mọi cây của hộ")]
    public async Task Get_Member_ReturnsFullCrop()
    {
        var ct = TestContext.Current.CancellationToken;
        var household = await factory.CreateHouseholdAsync(ct);
        var crop = await TestData.CreateCropAsync(factory.Postgres, household, "Lúa ST25", ct);

        var response = await factory.CreateClientFor(household.Member).GetAsync($"{CropsPath}/{crop.Id}", ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var data = (await response.ReadJsonAsync(ct)).GetProperty("data");
        Assert.Equal("Kilogram", data.GetProperty("yieldUnit").GetString());
        Assert.Equal(crop.Version, data.GetProperty("version").GetUInt32());
    }

    [Fact(DisplayName = "BR-USR-14: Nhân công chỉ thấy tên cây của mùa vụ có việc giao cho mình; cây khác trả NOT_FOUND")]
    public async Task Get_Worker_SeesOnlyNameOfAssignedCrop()
    {
        var ct = TestContext.Current.CancellationToken;
        var household = await factory.CreateHouseholdAsync(ct);
        var plot = await TestData.CreatePlotAsync(factory.Postgres, household, "TD001", ct);
        var assignedCrop = await TestData.CreateCropAsync(factory.Postgres, household, "Lúa ST25", ct);
        var otherCrop = await TestData.CreateCropAsync(factory.Postgres, household, "Ngô", ct);
        await TestData.CreateSeasonAsync(factory.Postgres, household, plot, SeasonStatus.InProgress, ct, household.Worker, assignedCrop);
        // Công việc giao cho người khác không mở cây cho Nhân công.
        await TestData.CreateSeasonAsync(factory.Postgres, household, plot, SeasonStatus.Planned, ct, household.Member, otherCrop);
        var client = factory.CreateClientFor(household.Worker);

        var assigned = await client.GetAsync($"{CropsPath}/{assignedCrop.Id}", ct);

        Assert.Equal(HttpStatusCode.OK, assigned.StatusCode);
        var data = (await assigned.ReadJsonAsync(ct)).GetProperty("data");
        Assert.Equal("Lúa ST25", data.GetProperty("name").GetString());
        foreach (var hidden in new[] { "group", "groupOther", "yieldUnit", "referenceInfo", "status", "yieldUnitLocked", "version" })
        {
            Assert.Equal(JsonValueKind.Null, data.GetProperty(hidden).ValueKind);
        }

        await (await client.GetAsync($"{CropsPath}/{otherCrop.Id}", ct))
            .AssertProblemAsync(HttpStatusCode.NotFound, ErrorCodes.NotFound, ct);
    }

    [Fact(DisplayName = "Ma trận 2.5: Quản trị hệ thống xem cây trồng trả FORBIDDEN")]
    public async Task Get_SystemAdmin_ReturnsForbidden()
    {
        var ct = TestContext.Current.CancellationToken;
        var admin = await factory.CreateSystemAdminAsync(ct);

        var response = await factory.CreateClientFor(admin).GetAsync(CropsPath, ct);

        await response.AssertProblemAsync(HttpStatusCode.Forbidden, ErrorCodes.Forbidden, ct);
    }

    [Fact(DisplayName = "Thiếu tên, nhóm cây, đơn vị sản lượng khi tạo trả VALIDATION_ERROR theo từng trường")]
    public async Task Create_MissingFields_ReturnsValidationError()
    {
        var ct = TestContext.Current.CancellationToken;
        var household = await factory.CreateHouseholdAsync(ct);
        var client = factory.CreateClientFor(household.Owner);

        var missing = await client.PostAsJsonAsync(CropsPath, new { name = "   " }, ct);
        var unknownUnit = await client.PostAsJsonAsync(CropsPath, new { name = "Ngô", group = "Staple", yieldUnit = "Sack" }, ct);

        var errors = (await missing.AssertProblemAsync(HttpStatusCode.BadRequest, ErrorCodes.ValidationError, ct))
            .GetProperty("errors");
        Assert.Equal("Vui lòng nhập tên cây trồng.", errors.GetProperty("name")[0].GetString());
        Assert.Equal("Vui lòng chọn nhóm cây.", errors.GetProperty("group")[0].GetString());
        Assert.Equal("Vui lòng chọn đơn vị sản lượng.", errors.GetProperty("yieldUnit")[0].GetString());
        await unknownUnit.AssertProblemAsync(HttpStatusCode.BadRequest, ErrorCodes.ValidationError, ct);
    }

    [Fact(DisplayName = "Sửa cây thiếu trạng thái trả VALIDATION_ERROR, cây ngừng sử dụng không bị bật lại")]
    public async Task Update_WithoutStatus_ReturnsValidationErrorAndKeepsStatus()
    {
        var ct = TestContext.Current.CancellationToken;
        var household = await factory.CreateHouseholdAsync(ct);
        var crop = await TestData.CreateCropAsync(factory.Postgres, household, "Lúa ST25", ct, status: RecordStatus.Inactive);

        var response = await factory.CreateClientFor(household.Owner).PutAsJsonAsync(
            $"{CropsPath}/{crop.Id}",
            new { name = "Lúa ST25", group = "Staple", yieldUnit = "Kilogram", version = crop.Version },
            ct);

        var problem = await response.AssertProblemAsync(HttpStatusCode.BadRequest, ErrorCodes.ValidationError, ct);
        Assert.Equal("Vui lòng chọn trạng thái.", problem.GetProperty("errors").GetProperty("status")[0].GetString());
        await using var db = factory.Postgres.CreateDbContext();
        Assert.Equal(RecordStatus.Inactive, (await db.Crops.IgnoreQueryFilters().SingleAsync(c => c.Id == crop.Id, ct)).Status);
    }

    // Phiếu thu hoạch ghi thẳng database: module thu hoạch không thuộc phạm vi của các test này.
    private async Task CreateHarvestRecordAsync(TestHousehold household, Crop crop, bool voided, CancellationToken ct)
    {
        var plot = await TestData.CreatePlotAsync(factory.Postgres, household, $"TD{Guid.CreateVersion7():N}"[..20], ct);
        var season = await TestData.CreateSeasonAsync(factory.Postgres, household, plot, SeasonStatus.Closed, ct, crop: crop);

        await using var db = factory.Postgres.CreateDbContext(TestCurrentUser.For(household.Owner));
        db.HarvestRecords.Add(new HarvestRecord
        {
            SeasonId = season.Id,
            QualityGrade = QualityGrade.Grade1,
            Quantity = 50m,
            HarvestDate = new DateOnly(2026, 11, 1),
            RequestId = Guid.CreateVersion7(),
            VoidedAt = voided ? new DateTime(2026, 11, 2, 0, 0, 0, DateTimeKind.Utc) : null,
            VoidedById = voided ? household.Owner.Id : null,
            VoidReason = voided ? "Ghi nhầm" : null,
        });
        await db.SaveChangesAsync(ct);
    }

    private static object NewCrop(string name) => new { name, group = "Staple", yieldUnit = "Kilogram" };

    private static object UpdateOf(string name, uint version, string status = "Active", string yieldUnit = "Kilogram") => new
    {
        name,
        group = "Staple",
        yieldUnit,
        status,
        version,
    };
}
