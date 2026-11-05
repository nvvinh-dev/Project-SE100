using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using HouseholdFarm.Api.Common.Errors;
using HouseholdFarm.Api.Domain.Enums;
using HouseholdFarm.Api.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace HouseholdFarm.Api.Tests.Features.Auth;

public class RegisterTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private const string Password = "MatKhau2026";

    [Fact(DisplayName = "FR-AUTH-01: đăng ký tạo hộ mới và Chủ hộ, chưa bắt đầu phiên đăng nhập")]
    public async Task Register_WithValidInput_CreatesHouseholdAndOwnerWithoutSession()
    {
        var ct = TestContext.Current.CancellationToken;
        var phone = TestData.NewPhone();

        var response = await RegisterAsync(phone, ct, contactInfo: "  Zalo cùng số  ");

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        // UC-AUTH-01: không trả access token, không đặt cookie refresh token.
        Assert.Null(AuthApi.ReadRefreshCookie(response));
        var data = (await response.ReadJsonAsync(ct)).GetProperty("data");
        Assert.False(data.TryGetProperty("accessToken", out _));
        Assert.Equal("Owner", data.GetProperty("role").GetString());
        Assert.Equal(phone, data.GetProperty("phone").GetString());

        await using var db = factory.Postgres.CreateDbContext();
        var owner = await db.Users.Include(u => u.Household).SingleAsync(u => u.UserName == phone, ct);
        Assert.Equal(data.GetProperty("id").GetGuid(), owner.Id);
        Assert.Equal(UserRole.Owner, owner.Role);
        Assert.Equal("Nguyễn Văn An", owner.FullName);
        Assert.Equal(phone, owner.PhoneNumber);
        Assert.True(owner.LockoutEnabled);
        Assert.False(owner.IsLocked);

        // BR-CAT-02 và hai công tắc cài đặt hộ mặc định tắt.
        var household = owner.Household!;
        Assert.Matches(@"^HH\d{6}$", household.Code);
        Assert.Equal(household.Code, data.GetProperty("household").GetProperty("code").GetString());
        Assert.Equal("Hộ ông An", household.Name);
        Assert.Equal("Ấp 2, xã Tân Phú", household.ProductionAddress);
        Assert.Equal("Zalo cùng số", household.ContactInfo);
        Assert.False(household.Settings.AllowNegativeStock);
        Assert.False(household.Settings.AllowOverlappingSeasons);

        Assert.False(await db.RefreshTokens.AnyAsync(t => t.UserId == owner.Id, ct));

        // Đăng nhập được bằng số và mật khẩu vừa đăng ký (FR-AUTH-02).
        await AuthApi.LoginAsync(factory, phone, Password, ct);
    }

    [Fact(DisplayName = "BR-USR-02: +84 912 345 678 và 0912345678 là một số, đăng ký lần hai trả PHONE_ALREADY_USED")]
    public async Task Register_SamePhoneInOtherFormat_ReturnsPhoneAlreadyUsed()
    {
        var ct = TestContext.Current.CancellationToken;
        var phone = TestData.NewPhone();
        Assert.Equal(HttpStatusCode.Created, (await RegisterAsync(phone, ct)).StatusCode);

        var response = await RegisterAsync($"+84 {phone[1..4]} {phone[4..7]} {phone[7..]}", ct);

        await response.AssertProblemAsync(HttpStatusCode.Conflict, ErrorCodes.PhoneAlreadyUsed, ct);
        await using var db = factory.Postgres.CreateDbContext();
        Assert.Equal(1, await db.Users.CountAsync(u => u.UserName == phone, ct));
    }

    [Fact(DisplayName = "BR-USR-02: số của tài khoản đã bị khóa vẫn không đăng ký lại được")]
    public async Task Register_PhoneOfLockedAccount_ReturnsPhoneAlreadyUsed()
    {
        var ct = TestContext.Current.CancellationToken;
        var household = await factory.CreateHouseholdAsync(ct);
        await using (var db = factory.Postgres.CreateDbContext())
        {
            await db.Users.Where(u => u.Id == household.Member.Id)
                .ExecuteUpdateAsync(s => s.SetProperty(u => u.IsLocked, true), ct);
        }

        var response = await RegisterAsync(household.Member.PhoneNumber!, ct);

        await response.AssertProblemAsync(HttpStatusCode.Conflict, ErrorCodes.PhoneAlreadyUsed, ct);
    }

    [Fact(DisplayName = "D-22: mật khẩu thiếu chữ số, thiếu họ tên, tên hộ, địa chỉ khu sản xuất trả VALIDATION_ERROR")]
    public async Task Register_InvalidInput_ReturnsValidationError()
    {
        var ct = TestContext.Current.CancellationToken;
        var phone = TestData.NewPhone();

        var response = await AuthApi.CreateClient(factory).PostAsJsonAsync(
            AuthApi.RegisterPath,
            new { fullName = " ", phone, password = "MatKhauKhongSo", householdName = "", productionAddress = "" },
            ct);

        var problem = await response.AssertProblemAsync(HttpStatusCode.BadRequest, ErrorCodes.ValidationError, ct);
        Assert.Equal(["Mật khẩu phải có ít nhất 1 chữ số."], Messages(problem, "password"));
        Assert.Equal(["Vui lòng nhập họ tên."], Messages(problem, "fullName"));
        Assert.Equal(["Vui lòng nhập tên hộ."], Messages(problem, "householdName"));
        Assert.Equal(["Vui lòng nhập địa chỉ khu sản xuất."], Messages(problem, "productionAddress"));

        await using var db = factory.Postgres.CreateDbContext();
        Assert.False(await db.Users.AnyAsync(u => u.UserName == phone, ct));
    }

    [Theory(DisplayName = "D-22: mật khẩu ngắn hơn 8 ký tự hoặc không có chữ cái trả VALIDATION_ERROR")]
    [InlineData("Mk12345", "Mật khẩu phải có ít nhất 8 ký tự.")]
    [InlineData("12345678", "Mật khẩu phải có ít nhất 1 chữ cái.")]
    public async Task Register_WeakPassword_ReturnsValidationError(string password, string message)
    {
        var ct = TestContext.Current.CancellationToken;

        var response = await RegisterAsync(TestData.NewPhone(), ct, password: password);

        var problem = await response.AssertProblemAsync(HttpStatusCode.BadRequest, ErrorCodes.ValidationError, ct);
        Assert.Equal([message], Messages(problem, "password"));
    }

    private Task<HttpResponseMessage> RegisterAsync(
        string phone,
        CancellationToken ct,
        string password = Password,
        string? contactInfo = null) =>
        AuthApi.CreateClient(factory).PostAsJsonAsync(
            AuthApi.RegisterPath,
            new
            {
                fullName = " Nguyễn Văn An ",
                phone,
                password,
                householdName = "Hộ ông An",
                productionAddress = "Ấp 2, xã Tân Phú",
                contactInfo,
            },
            ct);

    private static List<string?> Messages(JsonElement problem, string field) =>
        problem.GetProperty("errors").GetProperty(field).EnumerateArray().Select(e => e.GetString()).ToList();
}
