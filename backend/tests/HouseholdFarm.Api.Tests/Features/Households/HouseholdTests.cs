using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using HouseholdFarm.Api.Common.Auditing;
using HouseholdFarm.Api.Common.Errors;
using HouseholdFarm.Api.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace HouseholdFarm.Api.Tests.Features.Households;

/// <summary>Xem, cập nhật thông tin hộ (FR-HH-01) và cài đặt hộ (FR-HH-08).</summary>
public class HouseholdTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private const string HouseholdPath = "/api/household";
    private const string SettingsPath = "/api/household/settings";
    private const string ChangeSettingsPath = "/api/household/change-settings";

    [Fact(DisplayName = "FR-HH-01: Chủ hộ sửa tên, địa chỉ, liên hệ của hộ; mã hộ không đổi, version mới")]
    public async Task Update_Owner_SavesChanges()
    {
        var ct = TestContext.Current.CancellationToken;
        var household = await factory.CreateHouseholdAsync(ct);
        var client = factory.CreateClientFor(household.Owner);
        var current = (await (await client.GetAsync(HouseholdPath, ct)).ReadJsonAsync(ct)).GetProperty("data");

        var response = await client.PutAsJsonAsync(
            HouseholdPath,
            new
            {
                code = "HH999999",
                name = "  Hộ ông Tư  ",
                productionAddress = " Ấp 3, xã Mỹ An ",
                contactInfo = "  ",
                version = current.GetProperty("version").GetUInt32(),
            },
            ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var data = (await response.ReadJsonAsync(ct)).GetProperty("data");
        Assert.Equal(household.Household.Code, data.GetProperty("code").GetString());
        Assert.Equal("Hộ ông Tư", data.GetProperty("name").GetString());
        Assert.Equal("Ấp 3, xã Mỹ An", data.GetProperty("productionAddress").GetString());
        Assert.Equal(JsonValueKind.Null, data.GetProperty("contactInfo").ValueKind);
        Assert.NotEqual(current.GetProperty("version").GetUInt32(), data.GetProperty("version").GetUInt32());

        await using var db = factory.Postgres.CreateDbContext();
        var saved = await db.Households.SingleAsync(h => h.Id == household.Household.Id, ct);
        Assert.Equal(household.Household.Code, saved.Code);
        Assert.Equal(household.Owner.Id, saved.UpdatedById);
    }

    [Fact(DisplayName = "Ma trận 2.5: Thành viên hộ xem được thông tin hộ nhưng sửa trả FORBIDDEN")]
    public async Task Member_CanViewButNotUpdate()
    {
        var ct = TestContext.Current.CancellationToken;
        var household = await factory.CreateHouseholdAsync(ct);
        var client = factory.CreateClientFor(household.MemberWithAllPermissions);

        var view = await client.GetAsync(HouseholdPath, ct);
        Assert.Equal(HttpStatusCode.OK, view.StatusCode);
        var data = (await view.ReadJsonAsync(ct)).GetProperty("data");
        Assert.Equal(household.Household.Name, data.GetProperty("name").GetString());

        await (await client.PutAsJsonAsync(HouseholdPath, UpdateOf(data.GetProperty("version").GetUInt32()), ct))
            .AssertProblemAsync(HttpStatusCode.Forbidden, ErrorCodes.Forbidden, ct);
    }

    [Fact(DisplayName = "Ma trận 2.5: Nhân công, Quản trị hệ thống xem thông tin hộ trả FORBIDDEN")]
    public async Task WorkerAndAdmin_View_ReturnsForbidden()
    {
        var ct = TestContext.Current.CancellationToken;
        var household = await factory.CreateHouseholdAsync(ct);
        var admin = await factory.CreateSystemAdminAsync(ct);

        foreach (var user in new[] { household.Worker, admin })
        {
            await (await factory.CreateClientFor(user).GetAsync(HouseholdPath, ct))
                .AssertProblemAsync(HttpStatusCode.Forbidden, ErrorCodes.Forbidden, ct);
        }
    }

    [Fact(DisplayName = "BR-GEN-11: sửa thông tin hộ bằng version cũ trả CONCURRENCY_CONFLICT")]
    public async Task Update_WithStaleVersion_ReturnsConcurrencyConflict()
    {
        var ct = TestContext.Current.CancellationToken;
        var household = await factory.CreateHouseholdAsync(ct);
        var client = factory.CreateClientFor(household.Owner);
        var version = (await (await client.GetAsync(HouseholdPath, ct)).ReadJsonAsync(ct))
            .GetProperty("data").GetProperty("version").GetUInt32();
        Assert.Equal(HttpStatusCode.OK, (await client.PutAsJsonAsync(HouseholdPath, UpdateOf(version, "Hộ mới"), ct)).StatusCode);

        var response = await client.PutAsJsonAsync(HouseholdPath, UpdateOf(version, "Hộ khác"), ct);

        await response.AssertProblemAsync(HttpStatusCode.Conflict, ErrorCodes.ConcurrencyConflict, ct);
    }

    [Fact(DisplayName = "UC-HH-01 3a: thiếu tên hộ, địa chỉ khu sản xuất trả VALIDATION_ERROR theo từng trường")]
    public async Task Update_MissingFields_ReturnsValidationError()
    {
        var ct = TestContext.Current.CancellationToken;
        var household = await factory.CreateHouseholdAsync(ct);

        var response = await factory.CreateClientFor(household.Owner)
            .PutAsJsonAsync(HouseholdPath, new { name = " ", productionAddress = "", version = 1u }, ct);

        var errors = (await response.AssertProblemAsync(HttpStatusCode.BadRequest, ErrorCodes.ValidationError, ct))
            .GetProperty("errors");
        Assert.Equal("Vui lòng nhập tên hộ.", errors.GetProperty("name")[0].GetString());
        Assert.Equal("Vui lòng nhập địa chỉ khu sản xuất.", errors.GetProperty("productionAddress")[0].GetString());
    }

    [Fact(DisplayName = "FR-HH-08: hộ mới có cả hai công tắc tắt; đổi một công tắc ghi nhật ký đúng công tắc đó")]
    public async Task ChangeSettings_Owner_SavesAndLogsChangedSwitch()
    {
        var ct = TestContext.Current.CancellationToken;
        var household = await factory.CreateHouseholdAsync(ct);
        var client = factory.CreateClientFor(household.Owner);

        var settings = (await (await client.GetAsync(SettingsPath, ct)).ReadJsonAsync(ct)).GetProperty("data");
        Assert.False(settings.GetProperty("allowNegativeStock").GetBoolean());
        Assert.False(settings.GetProperty("allowOverlappingSeasons").GetBoolean());

        var response = await client.PostAsJsonAsync(
            ChangeSettingsPath,
            new { allowNegativeStock = true, allowOverlappingSeasons = false, version = settings.GetProperty("version").GetUInt32() },
            ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var data = (await response.ReadJsonAsync(ct)).GetProperty("data");
        Assert.True(data.GetProperty("allowNegativeStock").GetBoolean());
        Assert.False(data.GetProperty("allowOverlappingSeasons").GetBoolean());

        await using var db = factory.Postgres.CreateDbContext();
        Assert.True((await db.Households.SingleAsync(h => h.Id == household.Household.Id, ct)).Settings.AllowNegativeStock);
        var log = await db.AuditLogs.IgnoreQueryFilters().SingleAsync(l => l.EntityId == household.Household.Id, ct);
        Assert.Equal(AuditActions.HouseholdChangeSettings, log.Action);
        Assert.Equal(household.Owner.Id, log.ActorId);
        var oldValues = JsonDocument.Parse(log.OldValues!).RootElement;
        var newValues = JsonDocument.Parse(log.NewValues!).RootElement;
        Assert.Equal(["allowNegativeStock"], oldValues.EnumerateObject().Select(p => p.Name));
        Assert.False(oldValues.GetProperty("allowNegativeStock").GetBoolean());
        Assert.True(newValues.GetProperty("allowNegativeStock").GetBoolean());
    }

    [Fact(DisplayName = "FR-HH-08: gửi cài đặt không đổi gì thì không ghi nhật ký")]
    public async Task ChangeSettings_NoChange_DoesNotLog()
    {
        var ct = TestContext.Current.CancellationToken;
        var household = await factory.CreateHouseholdAsync(ct);
        var client = factory.CreateClientFor(household.Owner);
        var version = (await (await client.GetAsync(SettingsPath, ct)).ReadJsonAsync(ct))
            .GetProperty("data").GetProperty("version").GetUInt32();

        var response = await client.PostAsJsonAsync(
            ChangeSettingsPath,
            new { allowNegativeStock = false, allowOverlappingSeasons = false, version },
            ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        await using var db = factory.Postgres.CreateDbContext();
        Assert.False(await db.AuditLogs.IgnoreQueryFilters().AnyAsync(l => l.EntityId == household.Household.Id, ct));
    }

    [Fact(DisplayName = "BR-GEN-11: đổi cài đặt hộ bằng version cũ trả CONCURRENCY_CONFLICT, không ghi nhật ký")]
    public async Task ChangeSettings_WithStaleVersion_ReturnsConcurrencyConflict()
    {
        var ct = TestContext.Current.CancellationToken;
        var household = await factory.CreateHouseholdAsync(ct);
        var client = factory.CreateClientFor(household.Owner);
        var version = (await (await client.GetAsync(SettingsPath, ct)).ReadJsonAsync(ct))
            .GetProperty("data").GetProperty("version").GetUInt32();
        // Người khác sửa thông tin hộ trước: cài đặt và thông tin hộ cùng một dòng nên dùng chung version.
        Assert.Equal(HttpStatusCode.OK, (await client.PutAsJsonAsync(HouseholdPath, UpdateOf(version, "Hộ mới"), ct)).StatusCode);

        var response = await client.PostAsJsonAsync(
            ChangeSettingsPath,
            new { allowNegativeStock = false, allowOverlappingSeasons = true, version },
            ct);

        await response.AssertProblemAsync(HttpStatusCode.Conflict, ErrorCodes.ConcurrencyConflict, ct);
        await using var db = factory.Postgres.CreateDbContext();
        Assert.False((await db.Households.SingleAsync(h => h.Id == household.Household.Id, ct)).Settings.AllowOverlappingSeasons);
        Assert.False(await db.AuditLogs.IgnoreQueryFilters().AnyAsync(l => l.EntityId == household.Household.Id, ct));
    }

    [Fact(DisplayName = "Ma trận 2.5: Thành viên hộ, Nhân công xem, đổi cài đặt hộ trả FORBIDDEN")]
    public async Task Settings_NotOwner_ReturnsForbidden()
    {
        var ct = TestContext.Current.CancellationToken;
        var household = await factory.CreateHouseholdAsync(ct);

        foreach (var user in new[] { household.MemberWithAllPermissions, household.Worker })
        {
            var client = factory.CreateClientFor(user);
            await (await client.GetAsync(SettingsPath, ct))
                .AssertProblemAsync(HttpStatusCode.Forbidden, ErrorCodes.Forbidden, ct);
            await (await client.PostAsJsonAsync(
                    ChangeSettingsPath,
                    new { allowNegativeStock = true, allowOverlappingSeasons = true, version = 1u },
                    ct))
                .AssertProblemAsync(HttpStatusCode.Forbidden, ErrorCodes.Forbidden, ct);
        }
    }

    [Fact(DisplayName = "Đổi cài đặt hộ thiếu công tắc trả VALIDATION_ERROR, không hiểu là tắt")]
    public async Task ChangeSettings_MissingSwitch_ReturnsValidationError()
    {
        var ct = TestContext.Current.CancellationToken;
        var household = await factory.CreateHouseholdAsync(ct);

        var response = await factory.CreateClientFor(household.Owner)
            .PostAsJsonAsync(ChangeSettingsPath, new { allowNegativeStock = true, version = 1u }, ct);

        var problem = await response.AssertProblemAsync(HttpStatusCode.BadRequest, ErrorCodes.ValidationError, ct);
        Assert.True(problem.GetProperty("errors").TryGetProperty("allowOverlappingSeasons", out _));
    }

    private static object UpdateOf(uint version, string name = "Hộ test") => new
    {
        name,
        productionAddress = "Ấp 1, xã Tân Phú",
        contactInfo = (string?)null,
        version,
    };
}
