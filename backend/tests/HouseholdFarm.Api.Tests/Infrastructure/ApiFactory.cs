using System.Net.Http.Headers;
using System.Security.Cryptography;
using HouseholdFarm.Api.Common.Auth;
using HouseholdFarm.Api.Domain.Entities;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace HouseholdFarm.Api.Tests.Infrastructure;

/// <summary>
/// Ứng dụng thật chạy trên database của <see cref="PostgresFixture"/>. Test tích hợp gọi API qua <c>CreateClient()</c>,
/// hoặc <see cref="CreateClientFor"/> khi cần đăng nhập.
/// </summary>
/// <remarks>
/// Môi trường <c>Testing</c> để không đọc user-secrets trên máy người chạy test (khóa API thật) và không bật Swagger UI.
/// </remarks>
public class ApiFactory(PostgresFixture postgres) : WebApplicationFactory<Program>
{
    // Khóa ký sinh ngẫu nhiên mỗi lượt chạy: test không cần user-secrets, repo không chứa khóa nào.
    private static readonly string _signingKey = Convert.ToBase64String(RandomNumberGenerator.GetBytes(48));

    public PostgresFixture Postgres => postgres;

    /// <inheritdoc cref="TestData.CreateHouseholdAsync"/>
    public Task<TestHousehold> CreateHouseholdAsync(CancellationToken ct) => TestData.CreateHouseholdAsync(postgres, ct);

    /// <inheritdoc cref="TestData.CreateSystemAdminAsync"/>
    public Task<ApplicationUser> CreateSystemAdminAsync(CancellationToken ct) => TestData.CreateSystemAdminAsync(postgres, ct);

    /// <summary>
    /// HttpClient đã đăng nhập bằng <paramref name="user"/>, vai trò và quyền theo đúng tài khoản đó,
    /// ví dụ <c>CreateClientFor(household.Worker)</c>. Access token cấp thẳng, không qua API đăng nhập.
    /// </summary>
    public HttpClient CreateClientFor(ApplicationUser user)
    {
        var client = CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            Services.GetRequiredService<AccessTokenIssuer>().Create(user.Id));
        return client;
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureAppConfiguration(config => config.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:Default"] = postgres.ConnectionString,
            ["Jwt:SigningKey"] = _signingKey,
        }));
    }
}
