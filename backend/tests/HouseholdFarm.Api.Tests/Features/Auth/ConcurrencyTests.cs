using System.Net;
using System.Net.Http.Json;
using HouseholdFarm.Api.Common.Errors;
using HouseholdFarm.Api.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace HouseholdFarm.Api.Tests.Features.Auth;

/// <summary>
/// Thao tác trên cùng một tài khoản chạy cùng lúc. Lỗi tranh chấp chỉ xảy ra ở một số lượt nên mỗi test chạy nhiều lượt.
/// </summary>
public class ConcurrencyTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private const int Rounds = 15;
    private const string NewPassword = "MatKhauMoi2026";

    [Fact(DisplayName = "BR-USR-04: đổi mật khẩu cùng lúc với làm mới phiên thì không còn refresh token nào dùng được")]
    public async Task ChangePassword_ConcurrentWithRefresh_RevokesTokenIssuedByRefresh()
    {
        var ct = TestContext.Current.CancellationToken;

        for (var round = 0; round < Rounds; round++)
        {
            var household = await factory.CreateHouseholdAsync(ct);
            var session = await AuthApi.LoginAsync(factory, household.Member.PhoneNumber!, TestData.Password, ct);
            var client = factory.CreateClientFor(household.Member);
            using var stop = CancellationTokenSource.CreateLinkedTokenSource(ct);

            // Một thiết bị làm mới phiên liên tục, mỗi lần dùng token vừa nhận, cho tới khi bị từ chối hoặc đổi mật khẩu xong.
            var refreshing = Task.Run(
                async () =>
                {
                    var token = session.RefreshToken;
                    while (!stop.IsCancellationRequested)
                    {
                        var response = await AuthApi.RefreshAsync(factory, token, ct);
                        if (response.StatusCode != HttpStatusCode.OK)
                        {
                            return;
                        }

                        token = (await AuthApi.ReadSessionAsync(response, ct)).RefreshToken;
                    }
                },
                ct);

            var changePassword = await client.PostAsJsonAsync(
                AuthApi.ChangePasswordPath,
                new { currentPassword = TestData.Password, newPassword = NewPassword },
                ct);
            await stop.CancelAsync();
            await refreshing;

            Assert.Equal(HttpStatusCode.NoContent, changePassword.StatusCode);
            await using var db = factory.Postgres.CreateDbContext();
            Assert.All(
                await db.RefreshTokens.Where(t => t.UserId == household.Member.Id).ToListAsync(ct),
                t => Assert.NotNull(t.RevokedAt));
        }
    }

    [Fact(DisplayName = "BR-USR-17: hai lần đổi mật khẩu cùng lúc thì một lần thành công, lần sau INVALID_CREDENTIALS, không lỗi 500")]
    public async Task ChangePassword_TwiceConcurrently_OneSucceedsOtherInvalidCredentials()
    {
        var ct = TestContext.Current.CancellationToken;

        for (var round = 0; round < Rounds; round++)
        {
            var household = await factory.CreateHouseholdAsync(ct);
            var client = factory.CreateClientFor(household.Owner);

            var responses = await Task.WhenAll(Enumerable.Range(0, 2).Select(_ => client.PostAsJsonAsync(
                AuthApi.ChangePasswordPath,
                new { currentPassword = TestData.Password, newPassword = NewPassword },
                ct)));

            Assert.Equal(
                [HttpStatusCode.NoContent, HttpStatusCode.Unauthorized],
                responses.Select(r => r.StatusCode).Order());
            await responses.Single(r => r.StatusCode == HttpStatusCode.Unauthorized)
                .AssertProblemAsync(HttpStatusCode.Unauthorized, ErrorCodes.InvalidCredentials, ct);
        }
    }

    [Fact(DisplayName = "FR-AUTH-07: nhiều lần sửa thông tin cá nhân cùng lúc đều thành công, không lỗi 500")]
    public async Task UpdateProfile_Concurrently_AllSucceed()
    {
        var ct = TestContext.Current.CancellationToken;
        var household = await factory.CreateHouseholdAsync(ct);
        var client = factory.CreateClientFor(household.Worker);

        var responses = await Task.WhenAll(Enumerable.Range(0, 5).Select(i => client.PutAsJsonAsync(
            AuthApi.MePath,
            new { fullName = $"Tên {i}", phone = household.Worker.PhoneNumber },
            ct)));

        Assert.All(responses, r => Assert.Equal(HttpStatusCode.OK, r.StatusCode));
    }
}
