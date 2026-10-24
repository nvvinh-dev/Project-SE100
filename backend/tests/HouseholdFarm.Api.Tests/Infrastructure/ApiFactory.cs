using HouseholdFarm.Api.Domain.Entities;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;

namespace HouseholdFarm.Api.Tests.Infrastructure;

/// <summary>
/// Ứng dụng thật chạy trên database của <see cref="PostgresFixture"/>. Test tích hợp gọi API qua <c>CreateClient()</c>.
/// </summary>
/// <remarks>
/// Môi trường <c>Testing</c> để không đọc user-secrets trên máy người chạy test (khóa API thật) và không bật Swagger UI.
/// </remarks>
public class ApiFactory(PostgresFixture postgres) : WebApplicationFactory<Program>
{
    public PostgresFixture Postgres => postgres;

    /// <inheritdoc cref="TestData.CreateHouseholdAsync"/>
    public Task<TestHousehold> CreateHouseholdAsync(CancellationToken ct) => TestData.CreateHouseholdAsync(postgres, ct);

    /// <inheritdoc cref="TestData.CreateSystemAdminAsync"/>
    public Task<ApplicationUser> CreateSystemAdminAsync(CancellationToken ct) => TestData.CreateSystemAdminAsync(postgres, ct);

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureAppConfiguration(config => config.AddInMemoryCollection(
            new Dictionary<string, string?> { ["ConnectionStrings:Default"] = postgres.ConnectionString }));
    }
}
