using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using HouseholdFarm.Api.Common.Errors;
using HouseholdFarm.Api.Domain.Enums;
using HouseholdFarm.Api.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace HouseholdFarm.Api.Tests.Features.Plots;

/// <summary>Tạo, xem, sửa thửa đất (FR-PLOT-01) và ngừng sử dụng thửa (FR-PLOT-02).</summary>
public class PlotTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private const string PlotsPath = "/api/plots";

    [Fact(DisplayName = "BR-CAT-01: tạo thửa để trống mã thì tự sinh TD001, TD002; thửa mới ở trạng thái Active")]
    public async Task Create_WithoutCode_GeneratesSequentialCodes()
    {
        var ct = TestContext.Current.CancellationToken;
        var household = await factory.CreateHouseholdAsync(ct);
        var client = factory.CreateClientFor(household.Owner);

        var first = await client.PostAsJsonAsync(PlotsPath, NewPlot(code: null), ct);
        var second = await client.PostAsJsonAsync(PlotsPath, NewPlot(code: "  "), ct);

        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        var data = (await first.ReadJsonAsync(ct)).GetProperty("data");
        Assert.Equal("TD001", data.GetProperty("code").GetString());
        Assert.Equal(1500.5m, data.GetProperty("areaM2").GetDecimal());
        Assert.Equal("Sau nhà", data.GetProperty("locationDescription").GetString());
        Assert.Equal("Loam", data.GetProperty("soilType").GetString());
        Assert.Equal("Well", data.GetProperty("waterSource").GetString());
        Assert.Equal("Active", data.GetProperty("status").GetString());
        Assert.True(data.GetProperty("version").GetUInt32() > 0);
        Assert.Equal($"{PlotsPath}/{data.GetProperty("id").GetGuid()}", first.Headers.Location!.AbsolutePath);

        Assert.Equal("TD002", (await second.ReadJsonAsync(ct)).GetProperty("data").GetProperty("code").GetString());
    }

    [Fact(DisplayName = "BR-CAT-01: mã tự sinh lấy số lớn nhất trong các mã đúng mẫu + 1, bỏ qua mã người dùng tự đặt")]
    public async Task Create_WithoutCode_ContinuesFromLargestMatchingCode()
    {
        var ct = TestContext.Current.CancellationToken;
        var household = await factory.CreateHouseholdAsync(ct);
        await TestData.CreatePlotAsync(factory.Postgres, household, "TD007", ct);
        await TestData.CreatePlotAsync(factory.Postgres, household, "TD-SAU-NHA", ct);

        var response = await factory.CreateClientFor(household.Owner).PostAsJsonAsync(PlotsPath, NewPlot(code: null), ct);

        Assert.Equal("TD008", (await response.ReadJsonAsync(ct)).GetProperty("data").GetProperty("code").GetString());
    }

    [Fact(DisplayName = "BR-CAT-01: tạo thửa với mã đã có trong hộ trả CODE_ALREADY_USED; hộ khác dùng cùng mã được")]
    public async Task Create_WithUsedCode_ReturnsCodeAlreadyUsed()
    {
        var ct = TestContext.Current.CancellationToken;
        var household = await factory.CreateHouseholdAsync(ct);
        var otherHousehold = await factory.CreateHouseholdAsync(ct);
        await TestData.CreatePlotAsync(factory.Postgres, household, "TD001", ct);

        var duplicate = await factory.CreateClientFor(household.Owner).PostAsJsonAsync(PlotsPath, NewPlot("TD001"), ct);
        var otherHouseholdSameCode = await factory.CreateClientFor(otherHousehold.Owner)
            .PostAsJsonAsync(PlotsPath, NewPlot("TD001"), ct);

        await duplicate.AssertProblemAsync(HttpStatusCode.Conflict, ErrorCodes.CodeAlreadyUsed, ct);
        Assert.Equal(HttpStatusCode.Created, otherHouseholdSameCode.StatusCode);
    }

    [Fact(DisplayName = "Ma trận 2.5: Thành viên hộ, Nhân công tạo, sửa thửa trả FORBIDDEN")]
    public async Task CreateOrUpdate_NotOwner_ReturnsForbidden()
    {
        var ct = TestContext.Current.CancellationToken;
        var household = await factory.CreateHouseholdAsync(ct);
        var plot = await TestData.CreatePlotAsync(factory.Postgres, household, "TD001", ct);

        foreach (var user in new[] { household.MemberWithAllPermissions, household.Worker })
        {
            var client = factory.CreateClientFor(user);

            await (await client.PostAsJsonAsync(PlotsPath, NewPlot(code: null), ct))
                .AssertProblemAsync(HttpStatusCode.Forbidden, ErrorCodes.Forbidden, ct);
            await (await client.PutAsJsonAsync($"{PlotsPath}/{plot.Id}", UpdateOf("TD001", plot.Version), ct))
                .AssertProblemAsync(HttpStatusCode.Forbidden, ErrorCodes.Forbidden, ct);
        }
    }

    [Fact(DisplayName = "TC-10: (a) xem, sửa thửa của hộ B bằng id trả NOT_FOUND; (b) gửi householdId của hộ B thì thửa vẫn tạo trong hộ A")]
    public async Task TC10_AccessOtherHouseholdPlot_ReturnsNotFoundAndCreatesInOwnHousehold()
    {
        var ct = TestContext.Current.CancellationToken;
        var householdA = await factory.CreateHouseholdAsync(ct);
        var householdB = await factory.CreateHouseholdAsync(ct);
        var plotB = await TestData.CreatePlotAsync(factory.Postgres, householdB, "TD001", ct);
        var client = factory.CreateClientFor(householdA.Owner);

        await (await client.GetAsync($"{PlotsPath}/{plotB.Id}", ct))
            .AssertProblemAsync(HttpStatusCode.NotFound, ErrorCodes.NotFound, ct);
        await (await client.PutAsJsonAsync($"{PlotsPath}/{plotB.Id}", UpdateOf("TD009", plotB.Version), ct))
            .AssertProblemAsync(HttpStatusCode.NotFound, ErrorCodes.NotFound, ct);

        var created = await client.PostAsJsonAsync(
            PlotsPath,
            new { householdId = householdB.Household.Id, areaM2 = 200m, soilType = "Clay", waterSource = "Rainwater" },
            ct);

        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var id = (await created.ReadJsonAsync(ct)).GetProperty("data").GetProperty("id").GetGuid();
        await using var db = factory.Postgres.CreateDbContext();
        var saved = await db.Plots.IgnoreQueryFilters().SingleAsync(p => p.Id == id, ct);
        Assert.Equal(householdA.Household.Id, saved.HouseholdId);
        Assert.Equal(householdA.Owner.Id, saved.CreatedById);
    }

    [Fact(DisplayName = "FR-PLOT-01: sửa thửa lưu đủ các trường, trả version mới; ghi chú loại đất chỉ giữ khi chọn Khác")]
    public async Task Update_WithCurrentVersion_SavesChanges()
    {
        var ct = TestContext.Current.CancellationToken;
        var household = await factory.CreateHouseholdAsync(ct);
        var plot = await TestData.CreatePlotAsync(factory.Postgres, household, "TD001", ct);

        var response = await factory.CreateClientFor(household.Owner).PutAsJsonAsync(
            $"{PlotsPath}/{plot.Id}",
            new
            {
                code = " TD-RUONG-1 ",
                areaM2 = 2500m,
                locationDescription = "Gần kênh",
                soilType = "Other",
                soilTypeOther = "Đất pha cát",
                waterSource = "Well",
                waterSourceOther = "Bị bỏ qua vì không chọn Khác",
                status = "Active",
                note = "Ruộng chính",
                version = plot.Version,
            },
            ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var data = (await response.ReadJsonAsync(ct)).GetProperty("data");
        Assert.Equal("TD-RUONG-1", data.GetProperty("code").GetString());
        Assert.NotEqual(plot.Version, data.GetProperty("version").GetUInt32());

        await using var db = factory.Postgres.CreateDbContext();
        var saved = await db.Plots.IgnoreQueryFilters().SingleAsync(p => p.Id == plot.Id, ct);
        Assert.Equal(2500m, saved.AreaM2);
        Assert.Equal(SoilType.Other, saved.SoilType);
        Assert.Equal("Đất pha cát", saved.SoilTypeOther);
        Assert.Null(saved.WaterSourceOther);
        Assert.Equal("Ruộng chính", saved.Note);
        Assert.Equal(household.Owner.Id, saved.UpdatedById);
    }

    [Fact(DisplayName = "BR-CAT-01: sửa sang mã của thửa khác trong hộ trả CODE_ALREADY_USED")]
    public async Task Update_ToUsedCode_ReturnsCodeAlreadyUsed()
    {
        var ct = TestContext.Current.CancellationToken;
        var household = await factory.CreateHouseholdAsync(ct);
        await TestData.CreatePlotAsync(factory.Postgres, household, "TD001", ct);
        var plot = await TestData.CreatePlotAsync(factory.Postgres, household, "TD002", ct);

        var response = await factory.CreateClientFor(household.Owner)
            .PutAsJsonAsync($"{PlotsPath}/{plot.Id}", UpdateOf("TD001", plot.Version), ct);

        await response.AssertProblemAsync(HttpStatusCode.Conflict, ErrorCodes.CodeAlreadyUsed, ct);
    }

    [Fact(DisplayName = "BR-GEN-11: sửa bằng version cũ (người khác đã sửa trước) trả CONCURRENCY_CONFLICT")]
    public async Task Update_WithStaleVersion_ReturnsConcurrencyConflict()
    {
        var ct = TestContext.Current.CancellationToken;
        var household = await factory.CreateHouseholdAsync(ct);
        var plot = await TestData.CreatePlotAsync(factory.Postgres, household, "TD001", ct);
        var client = factory.CreateClientFor(household.Owner);
        // Lần sửa đầu phải đổi giá trị: gửi y nguyên thì EF không ghi gì và version không đổi.
        Assert.Equal(
            HttpStatusCode.OK,
            (await client.PutAsJsonAsync($"{PlotsPath}/{plot.Id}", UpdateOf("TD002", plot.Version), ct)).StatusCode);

        var response = await client.PutAsJsonAsync($"{PlotsPath}/{plot.Id}", UpdateOf("TD005", plot.Version), ct);

        await response.AssertProblemAsync(HttpStatusCode.Conflict, ErrorCodes.ConcurrencyConflict, ct);
    }

    [Theory(DisplayName = "BR-CAT-04: ngừng sử dụng thửa đang có mùa vụ Lên kế hoạch hoặc Đang canh tác trả CATALOG_IN_USE")]
    [InlineData(SeasonStatus.Planned)]
    [InlineData(SeasonStatus.InProgress)]
    public async Task Deactivate_WithRunningSeason_ReturnsCatalogInUse(SeasonStatus status)
    {
        var ct = TestContext.Current.CancellationToken;
        var household = await factory.CreateHouseholdAsync(ct);
        var plot = await TestData.CreatePlotAsync(factory.Postgres, household, "TD001", ct);
        await TestData.CreateSeasonAsync(factory.Postgres, household, plot, status, ct);

        var response = await factory.CreateClientFor(household.Owner)
            .PutAsJsonAsync($"{PlotsPath}/{plot.Id}", UpdateOf("TD001", plot.Version, "Inactive"), ct);

        var problem = await response.AssertProblemAsync(HttpStatusCode.UnprocessableEntity, ErrorCodes.CatalogInUse, ct);
        Assert.Contains("TD001", problem.GetProperty("detail").GetString());
    }

    [Fact(DisplayName = "FR-PLOT-02: thửa chỉ có mùa vụ đã chốt, đã hủy thì ngừng sử dụng được, rồi dùng lại được")]
    public async Task Deactivate_WithoutRunningSeason_SucceedsAndCanReactivate()
    {
        var ct = TestContext.Current.CancellationToken;
        var household = await factory.CreateHouseholdAsync(ct);
        var plot = await TestData.CreatePlotAsync(factory.Postgres, household, "TD001", ct);
        await TestData.CreateSeasonAsync(factory.Postgres, household, plot, SeasonStatus.Closed, ct);
        await TestData.CreateSeasonAsync(factory.Postgres, household, plot, SeasonStatus.Cancelled, ct);
        var client = factory.CreateClientFor(household.Owner);

        var deactivated = await client.PutAsJsonAsync($"{PlotsPath}/{plot.Id}", UpdateOf("TD001", plot.Version, "Inactive"), ct);
        Assert.Equal(HttpStatusCode.OK, deactivated.StatusCode);
        var data = (await deactivated.ReadJsonAsync(ct)).GetProperty("data");
        Assert.Equal("Inactive", data.GetProperty("status").GetString());

        var reactivated = await client.PutAsJsonAsync(
            $"{PlotsPath}/{plot.Id}",
            UpdateOf("TD001", data.GetProperty("version").GetUInt32(), "Active"),
            ct);
        Assert.Equal("Active", (await reactivated.ReadJsonAsync(ct)).GetProperty("data").GetProperty("status").GetString());
    }

    [Fact(DisplayName = "FR-PLOT-02: không có API xóa thửa đất")]
    public async Task Delete_IsNotSupported()
    {
        var ct = TestContext.Current.CancellationToken;
        var household = await factory.CreateHouseholdAsync(ct);
        var plot = await TestData.CreatePlotAsync(factory.Postgres, household, "TD001", ct);

        var response = await factory.CreateClientFor(household.Owner).DeleteAsync($"{PlotsPath}/{plot.Id}", ct);

        Assert.Equal(HttpStatusCode.MethodNotAllowed, response.StatusCode);
        await using var db = factory.Postgres.CreateDbContext();
        Assert.True(await db.Plots.IgnoreQueryFilters().AnyAsync(p => p.Id == plot.Id, ct));
    }

    [Fact(DisplayName = "Ma trận 2.5: Thành viên hộ xem được chi tiết mọi thửa của hộ")]
    public async Task Get_Member_ReturnsFullPlot()
    {
        var ct = TestContext.Current.CancellationToken;
        var household = await factory.CreateHouseholdAsync(ct);
        var plot = await TestData.CreatePlotAsync(factory.Postgres, household, "TD001", ct, locationDescription: "Đầu xóm");

        var response = await factory.CreateClientFor(household.Member).GetAsync($"{PlotsPath}/{plot.Id}", ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var data = (await response.ReadJsonAsync(ct)).GetProperty("data");
        Assert.Equal(1000m, data.GetProperty("areaM2").GetDecimal());
        Assert.Equal(plot.Version, data.GetProperty("version").GetUInt32());
    }

    [Fact(DisplayName = "BR-USR-14: Nhân công chỉ thấy mã, vị trí của thửa có việc giao cho mình; thửa khác trả NOT_FOUND")]
    public async Task Get_Worker_SeesOnlyCodeAndLocationOfAssignedPlot()
    {
        var ct = TestContext.Current.CancellationToken;
        var household = await factory.CreateHouseholdAsync(ct);
        var assignedPlot = await TestData.CreatePlotAsync(factory.Postgres, household, "TD001", ct, locationDescription: "Bờ kênh");
        var otherPlot = await TestData.CreatePlotAsync(factory.Postgres, household, "TD002", ct);
        await TestData.CreateSeasonAsync(factory.Postgres, household, assignedPlot, SeasonStatus.InProgress, ct, household.Worker);
        // Công việc giao cho người khác không mở thửa cho Nhân công.
        await TestData.CreateSeasonAsync(factory.Postgres, household, otherPlot, SeasonStatus.InProgress, ct, household.Member);
        var client = factory.CreateClientFor(household.Worker);

        var assigned = await client.GetAsync($"{PlotsPath}/{assignedPlot.Id}", ct);

        Assert.Equal(HttpStatusCode.OK, assigned.StatusCode);
        var data = (await assigned.ReadJsonAsync(ct)).GetProperty("data");
        Assert.Equal("TD001", data.GetProperty("code").GetString());
        Assert.Equal("Bờ kênh", data.GetProperty("locationDescription").GetString());
        foreach (var hidden in new[] { "areaM2", "soilType", "soilTypeOther", "waterSource", "waterSourceOther", "status", "note", "version" })
        {
            Assert.Equal(JsonValueKind.Null, data.GetProperty(hidden).ValueKind);
        }

        await (await client.GetAsync($"{PlotsPath}/{otherPlot.Id}", ct))
            .AssertProblemAsync(HttpStatusCode.NotFound, ErrorCodes.NotFound, ct);
    }

    [Fact(DisplayName = "Ma trận 2.5: Quản trị hệ thống xem thửa trả FORBIDDEN")]
    public async Task Get_SystemAdmin_ReturnsForbidden()
    {
        var ct = TestContext.Current.CancellationToken;
        var admin = await factory.CreateSystemAdminAsync(ct);

        var response = await factory.CreateClientFor(admin).GetAsync(PlotsPath, ct);

        await response.AssertProblemAsync(HttpStatusCode.Forbidden, ErrorCodes.Forbidden, ct);
    }

    [Fact(DisplayName = "Diện tích không lớn hơn 0, loại đất không hợp lệ trả VALIDATION_ERROR theo từng trường")]
    public async Task Create_InvalidInput_ReturnsValidationError()
    {
        var ct = TestContext.Current.CancellationToken;
        var household = await factory.CreateHouseholdAsync(ct);
        var client = factory.CreateClientFor(household.Owner);

        var zeroArea = await client.PostAsJsonAsync(PlotsPath, NewPlot(code: null, areaM2: 0m), ct);
        var unknownSoil = await client.PostAsJsonAsync(
            PlotsPath,
            new { areaM2 = 100m, soilType = "Moon", waterSource = "Well" },
            ct);

        var zeroAreaProblem = await zeroArea.AssertProblemAsync(HttpStatusCode.BadRequest, ErrorCodes.ValidationError, ct);
        Assert.Equal(
            "Diện tích phải lớn hơn 0.",
            zeroAreaProblem.GetProperty("errors").GetProperty("areaM2")[0].GetString());
        await unknownSoil.AssertProblemAsync(HttpStatusCode.BadRequest, ErrorCodes.ValidationError, ct);
    }

    private static object NewPlot(string? code, decimal areaM2 = 1500.5m) => new
    {
        code,
        areaM2,
        locationDescription = "Sau nhà",
        soilType = "Loam",
        waterSource = "Well",
        note = (string?)null,
    };

    private static object UpdateOf(string code, uint version, string status = "Active") => new
    {
        code,
        areaM2 = 1000m,
        soilType = "Alluvial",
        waterSource = "RiverCanal",
        status,
        version,
    };
}
