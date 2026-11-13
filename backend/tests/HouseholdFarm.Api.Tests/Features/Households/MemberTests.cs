using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using HouseholdFarm.Api.Common.Errors;
using HouseholdFarm.Api.Domain.Enums;
using HouseholdFarm.Api.Tests.Features.Auth;
using HouseholdFarm.Api.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace HouseholdFarm.Api.Tests.Features.Households;

/// <summary>Tạo tài khoản thành viên (FR-HH-02), danh sách (FR-HH-03), sửa thông tin thành viên (FR-HH-04).</summary>
public class MemberTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private const string MembersPath = "/api/members";

    [Theory(DisplayName = "FR-HH-02: Chủ hộ tạo Thành viên hộ, Nhân công; quyền bổ sung tắt, đăng nhập được bằng mật khẩu ban đầu")]
    [InlineData("Member")]
    [InlineData("Worker")]
    public async Task Create_MemberOrWorker_CanLogIn(string role)
    {
        var ct = TestContext.Current.CancellationToken;
        var household = await factory.CreateHouseholdAsync(ct);
        var phone = TestData.NewPhone();

        var response = await factory.CreateClientFor(household.Owner).PostAsJsonAsync(
            MembersPath,
            new { fullName = "  Nguyễn Văn Ba ", phone = "+84 " + phone[1..], role, initialPassword = "BaMoiVao2026" },
            ct);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var data = (await response.ReadJsonAsync(ct)).GetProperty("data");
        var id = data.GetProperty("id").GetGuid();
        Assert.Equal("Nguyễn Văn Ba", data.GetProperty("fullName").GetString());
        Assert.Equal(phone, data.GetProperty("phone").GetString());
        Assert.Equal(role, data.GetProperty("role").GetString());
        Assert.False(data.GetProperty("canManageStock").GetBoolean());
        Assert.False(data.GetProperty("canManageFinance").GetBoolean());
        Assert.False(data.GetProperty("canCloseSeason").GetBoolean());
        Assert.False(data.GetProperty("isLocked").GetBoolean());
        Assert.Equal($"{MembersPath}/{id}", response.Headers.Location!.AbsolutePath);

        await using var db = factory.Postgres.CreateDbContext();
        Assert.Equal(household.Household.Id, (await db.Users.SingleAsync(u => u.Id == id, ct)).HouseholdId);
        await AuthApi.LoginAsync(factory, phone, "BaMoiVao2026", ct);
    }

    [Fact(DisplayName = "BR-USR-08: tạo tài khoản vai trò Chủ hộ, Quản trị hoặc thiếu vai trò trả VALIDATION_ERROR")]
    public async Task Create_InvalidRole_ReturnsValidationError()
    {
        var ct = TestContext.Current.CancellationToken;
        var household = await factory.CreateHouseholdAsync(ct);
        var client = factory.CreateClientFor(household.Owner);

        foreach (var role in new[] { "Owner", "SystemAdmin", null })
        {
            var response = await client.PostAsJsonAsync(MembersPath, NewMember(TestData.NewPhone(), role), ct);
            var problem = await response.AssertProblemAsync(HttpStatusCode.BadRequest, ErrorCodes.ValidationError, ct);
            Assert.True(problem.GetProperty("errors").TryGetProperty("role", out _));
        }
    }

    [Fact(DisplayName = "BR-USR-02: tạo tài khoản bằng số đã dùng ở hộ khác (viết khác dạng) trả PHONE_ALREADY_USED")]
    public async Task Create_UsedPhone_ReturnsPhoneAlreadyUsed()
    {
        var ct = TestContext.Current.CancellationToken;
        var household = await factory.CreateHouseholdAsync(ct);
        var otherHousehold = await factory.CreateHouseholdAsync(ct);
        var usedPhone = otherHousehold.Worker.PhoneNumber!;

        var response = await factory.CreateClientFor(household.Owner)
            .PostAsJsonAsync(MembersPath, NewMember($"{usedPhone[..4]}.{usedPhone[4..]}"), ct);

        await response.AssertProblemAsync(HttpStatusCode.Conflict, ErrorCodes.PhoneAlreadyUsed, ct);
    }

    [Fact(DisplayName = "Ma trận 2.5: Thành viên hộ, Nhân công tạo tài khoản trả FORBIDDEN; mật khẩu yếu trả VALIDATION_ERROR")]
    public async Task Create_NotOwnerOrWeakPassword_IsRejected()
    {
        var ct = TestContext.Current.CancellationToken;
        var household = await factory.CreateHouseholdAsync(ct);

        foreach (var user in new[] { household.MemberWithAllPermissions, household.Worker })
        {
            await (await factory.CreateClientFor(user).PostAsJsonAsync(MembersPath, NewMember(TestData.NewPhone()), ct))
                .AssertProblemAsync(HttpStatusCode.Forbidden, ErrorCodes.Forbidden, ct);
        }

        var weak = await factory.CreateClientFor(household.Owner).PostAsJsonAsync(
            MembersPath,
            new { fullName = "Ba", phone = TestData.NewPhone(), role = "Member", initialPassword = "abcdefgh" },
            ct);
        var problem = await weak.AssertProblemAsync(HttpStatusCode.BadRequest, ErrorCodes.ValidationError, ct);
        Assert.Equal("Mật khẩu phải có ít nhất 1 chữ số.", problem.GetProperty("errors").GetProperty("initialPassword")[0].GetString());
    }

    [Fact(DisplayName = "FR-HH-03: Chủ hộ thấy cả hộ kèm số điện thoại, trạng thái; lọc vai trò, trạng thái khóa; tìm tên, số")]
    public async Task List_Owner_SeesWholeHouseholdAndFilters()
    {
        var ct = TestContext.Current.CancellationToken;
        var household = await factory.CreateHouseholdAsync(ct);
        await SetLockedAsync(household.Worker.Id, ct);
        var client = factory.CreateClientFor(household.Owner);

        var all = await (await client.GetAsync(MembersPath, ct)).ReadJsonAsync(ct);
        Assert.Equal(4, all.GetProperty("meta").GetProperty("totalCount").GetInt32());
        var worker = all.GetProperty("data").EnumerateArray().Single(m => m.GetProperty("role").GetString() == "Worker");
        Assert.Equal(household.Worker.PhoneNumber, worker.GetProperty("phone").GetString());
        Assert.True(worker.GetProperty("isLocked").GetBoolean());

        Assert.Equal([household.Worker.Id], await IdsAsync(client, "?role=Worker", ct));
        Assert.Equal([household.Worker.Id], await IdsAsync(client, "?isLocked=true", ct));
        Assert.Equal(3, (await IdsAsync(client, "?isLocked=false", ct)).Count);
        Assert.Equal([household.Owner.Id], await IdsAsync(client, "?search=chủ%20hộ", ct));
        Assert.Equal([household.Member.Id], await IdsAsync(client, $"?search={household.Member.PhoneNumber}", ct));
    }

    [Fact(DisplayName = "TC-10: danh sách người trong hộ không có người của hộ khác; xem người của hộ khác bằng id trả NOT_FOUND")]
    public async Task TC10_List_ExcludesOtherHousehold()
    {
        var ct = TestContext.Current.CancellationToken;
        var householdA = await factory.CreateHouseholdAsync(ct);
        var householdB = await factory.CreateHouseholdAsync(ct);
        var client = factory.CreateClientFor(householdA.Owner);

        var ids = await IdsAsync(client, string.Empty, ct);

        Assert.Equal(4, ids.Count);
        Assert.DoesNotContain(householdB.Member.Id, ids);
        await (await client.GetAsync($"{MembersPath}/{householdB.Member.Id}", ct))
            .AssertProblemAsync(HttpStatusCode.NotFound, ErrorCodes.NotFound, ct);
        await (await client.PutAsJsonAsync($"{MembersPath}/{householdB.Member.Id}", UpdateOf("Tên mới", householdB.Member), ct))
            .AssertProblemAsync(HttpStatusCode.NotFound, ErrorCodes.NotFound, ct);
    }

    [Fact(DisplayName = "Ma trận 2.5 ghi chú 1: Thành viên hộ chỉ thấy Chủ hộ và người cùng mùa vụ, chỉ họ tên, vai trò")]
    public async Task List_Member_SeesOwnerAndSeasonMatesOnly()
    {
        var ct = TestContext.Current.CancellationToken;
        var household = await factory.CreateHouseholdAsync(ct);
        var plot = await TestData.CreatePlotAsync(factory.Postgres, household, "TD001", ct);
        var shared = await TestData.CreateSeasonAsync(factory.Postgres, household, plot, SeasonStatus.InProgress, ct);
        var other = await TestData.CreateSeasonAsync(factory.Postgres, household, plot, SeasonStatus.Planned, ct);
        await TestData.AssignMemberAsync(factory.Postgres, household, shared, household.Member, ct);
        await TestData.AssignMemberAsync(factory.Postgres, household, shared, household.Worker, ct);
        // Cùng hộ nhưng không cùng mùa vụ với Thành viên hộ đang xem.
        await TestData.AssignMemberAsync(factory.Postgres, household, other, household.MemberWithAllPermissions, ct);
        var client = factory.CreateClientFor(household.Member);

        var body = await (await client.GetAsync(MembersPath, ct)).ReadJsonAsync(ct);

        var items = body.GetProperty("data").EnumerateArray().ToList();
        Assert.Equal(
            new[] { household.Owner.Id, household.Member.Id, household.Worker.Id }.Order(),
            items.Select(m => m.GetProperty("id").GetGuid()).Order());
        foreach (var item in items)
        {
            Assert.False(string.IsNullOrEmpty(item.GetProperty("fullName").GetString()));
            foreach (var hidden in new[] { "phone", "isLocked", "canManageStock", "canManageFinance", "canCloseSeason" })
            {
                Assert.Equal(JsonValueKind.Null, item.GetProperty(hidden).ValueKind);
            }
        }

        // Không dò được số điện thoại bằng ô tìm kiếm.
        Assert.Empty(await IdsAsync(client, $"?search={household.Worker.PhoneNumber}", ct));
    }

    [Fact(DisplayName = "Ma trận 2.5: Nhân công, Quản trị xem danh sách người trong hộ trả FORBIDDEN; Thành viên hộ xem chi tiết trả FORBIDDEN")]
    public async Task List_WorkerOrAdmin_ReturnsForbidden()
    {
        var ct = TestContext.Current.CancellationToken;
        var household = await factory.CreateHouseholdAsync(ct);
        var admin = await factory.CreateSystemAdminAsync(ct);

        foreach (var user in new[] { household.Worker, admin })
        {
            await (await factory.CreateClientFor(user).GetAsync(MembersPath, ct))
                .AssertProblemAsync(HttpStatusCode.Forbidden, ErrorCodes.Forbidden, ct);
        }

        await (await factory.CreateClientFor(household.Member).GetAsync($"{MembersPath}/{household.Worker.Id}", ct))
            .AssertProblemAsync(HttpStatusCode.Forbidden, ErrorCodes.Forbidden, ct);
    }

    [Fact(DisplayName = "FR-HH-04: sửa họ tên, số điện thoại; đăng nhập bằng số mới được; không thu hồi phiên đang có")]
    public async Task Update_NameAndPhone_SavesWithoutRevokingSessions()
    {
        var ct = TestContext.Current.CancellationToken;
        var household = await factory.CreateHouseholdAsync(ct);
        var session = await AuthApi.LoginAsync(factory, household.Member.PhoneNumber!, TestData.Password, ct);
        var newPhone = TestData.NewPhone();

        var response = await factory.CreateClientFor(household.Owner).PutAsJsonAsync(
            $"{MembersPath}/{household.Member.Id}",
            new { fullName = "Trần Thị Tư", phone = newPhone, version = household.Member.Version },
            ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var data = (await response.ReadJsonAsync(ct)).GetProperty("data");
        Assert.Equal("Trần Thị Tư", data.GetProperty("fullName").GetString());
        Assert.Equal(newPhone, data.GetProperty("phone").GetString());
        Assert.NotEqual(household.Member.Version, data.GetProperty("version").GetUInt32());
        await AuthApi.LoginAsync(factory, newPhone, TestData.Password, ct);
        Assert.Equal(HttpStatusCode.OK, (await AuthApi.RefreshAsync(factory, session.RefreshToken, ct)).StatusCode);
    }

    [Fact(DisplayName = "BR-USR-02, BR-GEN-11: sửa sang số của người khác trả PHONE_ALREADY_USED; version cũ trả CONCURRENCY_CONFLICT")]
    public async Task Update_UsedPhoneOrStaleVersion_IsRejected()
    {
        var ct = TestContext.Current.CancellationToken;
        var household = await factory.CreateHouseholdAsync(ct);
        var client = factory.CreateClientFor(household.Owner);
        var path = $"{MembersPath}/{household.Member.Id}";

        await (await client.PutAsJsonAsync(
                path,
                new { fullName = "Tư", phone = household.Worker.PhoneNumber, version = household.Member.Version },
                ct))
            .AssertProblemAsync(HttpStatusCode.Conflict, ErrorCodes.PhoneAlreadyUsed, ct);

        Assert.Equal(HttpStatusCode.OK, (await client.PutAsJsonAsync(path, UpdateOf("Tên một", household.Member), ct)).StatusCode);
        await (await client.PutAsJsonAsync(path, UpdateOf("Tên hai", household.Member), ct))
            .AssertProblemAsync(HttpStatusCode.Conflict, ErrorCodes.ConcurrencyConflict, ct);
    }

    [Fact(DisplayName = "BR-USR-07: sửa thông tin tài khoản Chủ hộ qua chức năng thành viên trả FORBIDDEN")]
    public async Task Update_Owner_ReturnsForbidden()
    {
        var ct = TestContext.Current.CancellationToken;
        var household = await factory.CreateHouseholdAsync(ct);

        var response = await factory.CreateClientFor(household.Owner)
            .PutAsJsonAsync($"{MembersPath}/{household.Owner.Id}", UpdateOf("Chủ hộ mới", household.Owner), ct);

        await response.AssertProblemAsync(HttpStatusCode.Forbidden, ErrorCodes.Forbidden, ct);
    }

    [Fact(DisplayName = "FR-HH-04: Chủ hộ xem chi tiết một thành viên; không có API xóa tài khoản")]
    public async Task Get_Owner_ReturnsMemberAndDeleteIsNotSupported()
    {
        var ct = TestContext.Current.CancellationToken;
        var household = await factory.CreateHouseholdAsync(ct);
        var client = factory.CreateClientFor(household.Owner);

        var response = await client.GetAsync($"{MembersPath}/{household.MemberWithAllPermissions.Id}", ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var data = (await response.ReadJsonAsync(ct)).GetProperty("data");
        Assert.Equal("Member", data.GetProperty("role").GetString());
        Assert.True(data.GetProperty("canManageFinance").GetBoolean());
        Assert.Equal(household.MemberWithAllPermissions.Version, data.GetProperty("version").GetUInt32());
        Assert.Equal(
            HttpStatusCode.MethodNotAllowed,
            (await client.DeleteAsync($"{MembersPath}/{household.Member.Id}", ct)).StatusCode);
    }

    private async Task<List<Guid>> IdsAsync(HttpClient client, string queryString, CancellationToken ct)
    {
        var response = await client.GetAsync(MembersPath + queryString, ct);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.ReadJsonAsync(ct)).GetProperty("data").EnumerateArray()
            .Select(m => m.GetProperty("id").GetGuid())
            .ToList();
    }

    private async Task SetLockedAsync(Guid userId, CancellationToken ct)
    {
        await using var db = factory.Postgres.CreateDbContext();
        await db.Users.Where(u => u.Id == userId).ExecuteUpdateAsync(s => s.SetProperty(u => u.IsLocked, true), ct);
    }

    private static object NewMember(string phone, string? role = "Member") =>
        new { fullName = "Lê Văn Năm", phone, role, initialPassword = "NamMoiVao2026" };

    private static object UpdateOf(string fullName, Domain.Entities.ApplicationUser user) =>
        new { fullName, phone = user.PhoneNumber, version = user.Version };
}
