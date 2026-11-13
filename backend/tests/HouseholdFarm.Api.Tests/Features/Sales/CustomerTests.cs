using System.Net;
using System.Net.Http.Json;
using HouseholdFarm.Api.Common.Errors;
using HouseholdFarm.Api.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace HouseholdFarm.Api.Tests.Features.Sales;

/// <summary>Danh mục khách hàng (FR-SAL-08, BR-GEN-13).</summary>
public class CustomerTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private const string CustomersPath = "/api/customers";

    [Fact(DisplayName = "FR-SAL-08: tạo khách hàng thành công, mặc định Active, số điện thoại được chuẩn hóa")]
    public async Task Create_ValidCustomer_Succeeds()
    {
        var ct = TestContext.Current.CancellationToken;
        var household = await factory.CreateHouseholdAsync(ct);

        var response = await factory.CreateClientFor(household.Owner).PostAsJsonAsync(
            CustomersPath,
            new { name = "  Chị Lan chợ Bình Điền ", phone = "+84 912 345 678", note = "Lấy hàng thứ Bảy" },
            ct);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var data = (await response.ReadJsonAsync(ct)).GetProperty("data");
        Assert.Equal("Chị Lan chợ Bình Điền", data.GetProperty("name").GetString());
        Assert.Equal("0912345678", data.GetProperty("phone").GetString());
        Assert.Equal("Active", data.GetProperty("status").GetString());
        var id = data.GetProperty("id").GetGuid();
        Assert.Equal($"{CustomersPath}/{id}", response.Headers.Location?.AbsolutePath);

        await using var db = factory.Postgres.CreateDbContext();
        var saved = await db.Customers.IgnoreQueryFilters().SingleAsync(c => c.Id == id, ct);
        Assert.Equal(household.Household.Id, saved.HouseholdId);
    }

    [Theory(DisplayName = "FR-SAL-08: thiếu tên hoặc số điện thoại sai định dạng trả VALIDATION_ERROR")]
    [InlineData("", null)]
    [InlineData("Anh Tư", "12345")]
    public async Task Create_InvalidData_ReturnsValidationError(string name, string? phone)
    {
        var ct = TestContext.Current.CancellationToken;
        var household = await factory.CreateHouseholdAsync(ct);

        var response = await factory.CreateClientFor(household.Owner).PostAsJsonAsync(CustomersPath, new { name, phone }, ct);

        await response.AssertProblemAsync(HttpStatusCode.BadRequest, ErrorCodes.ValidationError, ct);
    }

    [Fact(DisplayName = "FR-SAL-08: Thành viên hộ không có quyền TC, Nhân công quản lý khách hàng trả FORBIDDEN")]
    public async Task ManageCustomers_WithoutFinancePermission_ReturnsForbidden()
    {
        var ct = TestContext.Current.CancellationToken;
        var household = await factory.CreateHouseholdAsync(ct);

        foreach (var user in new[] { household.Member, household.Worker })
        {
            var client = factory.CreateClientFor(user);
            var create = await client.PostAsJsonAsync(CustomersPath, new { name = "Anh Tư" }, ct);
            await create.AssertProblemAsync(HttpStatusCode.Forbidden, ErrorCodes.Forbidden, ct);

            var list = await client.GetAsync(CustomersPath, ct);
            await list.AssertProblemAsync(HttpStatusCode.Forbidden, ErrorCodes.Forbidden, ct);
        }
    }

    [Fact(DisplayName = "FR-SAL-08: Thành viên hộ có quyền TC tạo, sửa được khách hàng")]
    public async Task ManageCustomers_MemberWithFinance_Succeeds()
    {
        var ct = TestContext.Current.CancellationToken;
        var household = await factory.CreateHouseholdAsync(ct);
        var client = factory.CreateClientFor(household.MemberWithAllPermissions);

        var created = await (await client.PostAsJsonAsync(CustomersPath, new { name = "Anh Tư" }, ct)).ReadJsonAsync(ct);
        var data = created.GetProperty("data");

        var response = await client.PutAsJsonAsync(
            $"{CustomersPath}/{data.GetProperty("id").GetGuid()}",
            new { name = "Anh Tư thương lái", phone = (string?)null, note = (string?)null, status = "Active", version = data.GetProperty("version").GetUInt32() },
            ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Anh Tư thương lái", (await response.ReadJsonAsync(ct)).GetProperty("data").GetProperty("name").GetString());
    }

    [Fact(DisplayName = "BR-GEN-13: chuyển khách hàng sang Inactive được; gửi version cũ trả CONCURRENCY_CONFLICT")]
    public async Task Update_ToInactive_SucceedsAndStaleVersionConflicts()
    {
        var ct = TestContext.Current.CancellationToken;
        var household = await factory.CreateHouseholdAsync(ct);
        var client = factory.CreateClientFor(household.Owner);
        var created = (await (await client.PostAsJsonAsync(CustomersPath, new { name = "Hợp tác xã Tân Phú" }, ct)).ReadJsonAsync(ct))
            .GetProperty("data");
        var path = $"{CustomersPath}/{created.GetProperty("id").GetGuid()}";
        var version = created.GetProperty("version").GetUInt32();

        var inactive = await client.PutAsJsonAsync(
            path, new { name = "Hợp tác xã Tân Phú", status = "Inactive", version }, ct);
        Assert.Equal(HttpStatusCode.OK, inactive.StatusCode);
        Assert.Equal("Inactive", (await inactive.ReadJsonAsync(ct)).GetProperty("data").GetProperty("status").GetString());

        var stale = await client.PutAsJsonAsync(path, new { name = "Tên khác", status = "Active", version }, ct);
        await stale.AssertProblemAsync(HttpStatusCode.Conflict, ErrorCodes.ConcurrencyConflict, ct);
    }

    [Fact(DisplayName = "FR-SAL-08: tìm theo tên và số điện thoại, lọc trạng thái; không thấy khách hàng của hộ khác")]
    public async Task GetList_SearchAndFilter_ReturnsMatches()
    {
        var ct = TestContext.Current.CancellationToken;
        var household = await factory.CreateHouseholdAsync(ct);
        var otherHousehold = await factory.CreateHouseholdAsync(ct);
        var client = factory.CreateClientFor(household.Owner);
        await client.PostAsJsonAsync(CustomersPath, new { name = "Chị Lan", phone = "0912000111" }, ct);
        await client.PostAsJsonAsync(CustomersPath, new { name = "Anh Ba vựa trái cây", status = "Inactive" }, ct);
        await factory.CreateClientFor(otherHousehold.Owner).PostAsJsonAsync(CustomersPath, new { name = "Chị Lan hộ B" }, ct);

        var byName = await (await client.GetAsync($"{CustomersPath}?search=lan", ct)).ReadJsonAsync(ct);
        var byPhone = await (await client.GetAsync($"{CustomersPath}?search=%2B84912000", ct)).ReadJsonAsync(ct);
        var inactive = await (await client.GetAsync($"{CustomersPath}?status=Inactive", ct)).ReadJsonAsync(ct);

        Assert.Equal(["Chị Lan"], byName.GetProperty("data").EnumerateArray()
            .Select(i => i.GetProperty("name").GetString()));
        Assert.Equal(["Chị Lan"], byPhone.GetProperty("data").EnumerateArray()
            .Select(i => i.GetProperty("name").GetString()));
        Assert.Equal(["Anh Ba vựa trái cây"], inactive.GetProperty("data").EnumerateArray()
            .Select(i => i.GetProperty("name").GetString()));
    }

    [Fact(DisplayName = "BR-GEN-02: khách hàng của hộ khác trả NOT_FOUND")]
    public async Task Get_OtherHouseholdCustomer_ReturnsNotFound()
    {
        var ct = TestContext.Current.CancellationToken;
        var household = await factory.CreateHouseholdAsync(ct);
        var otherHousehold = await factory.CreateHouseholdAsync(ct);
        var created = (await (await factory.CreateClientFor(otherHousehold.Owner)
            .PostAsJsonAsync(CustomersPath, new { name = "Khách hộ B" }, ct)).ReadJsonAsync(ct)).GetProperty("data");

        var response = await factory.CreateClientFor(household.Owner)
            .GetAsync($"{CustomersPath}/{created.GetProperty("id").GetGuid()}", ct);

        await response.AssertProblemAsync(HttpStatusCode.NotFound, ErrorCodes.NotFound, ct);
    }
}
