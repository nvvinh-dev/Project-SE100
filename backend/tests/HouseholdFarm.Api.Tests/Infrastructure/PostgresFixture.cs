using HouseholdFarm.Api.Common.Auth;
using HouseholdFarm.Api.Common.Persistence;
using HouseholdFarm.Api.Data;
using HouseholdFarm.Api.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Testcontainers.PostgreSql;

[assembly: AssemblyFixture(typeof(PostgresFixture))]

namespace HouseholdFarm.Api.Tests.Infrastructure;

/// <summary>
/// Một container PostgreSQL cho cả lượt chạy test, cùng phiên bản với database phát triển và Supabase.
/// Mỗi test tự tạo hộ riêng nên các test không ảnh hưởng nhau, không cần xóa dữ liệu giữa các test.
/// </summary>
public sealed class PostgresFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder("postgres:17").Build();

    public string ConnectionString => _container.GetConnectionString();

    public async ValueTask InitializeAsync()
    {
        await _container.StartAsync();

        await using var db = CreateDbContext();
        await db.Database.MigrateAsync();
    }

    public ValueTask DisposeAsync() => _container.DisposeAsync();

    /// <summary>
    /// AppDbContext nối thẳng vào database test, không qua ứng dụng; dùng để tạo dữ liệu test và đọc lại dữ liệu đã lưu.
    /// </summary>
    /// <param name="currentUser">Người dùng cho Global Query Filter và cột chung; để trống là chưa đăng nhập.</param>
    public AppDbContext CreateDbContext(ICurrentUser? currentUser = null)
    {
        currentUser ??= TestCurrentUser.Anonymous;
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(ConnectionString)
            .UseSnakeCaseNamingConvention()
            .AddInterceptors(new CommonColumnsInterceptor(currentUser, TestClock.At(TimeProvider.System.GetUtcNow())))
            .Options;
        return new AppDbContext(options, currentUser);
    }
}
