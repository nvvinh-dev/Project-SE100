using HouseholdFarm.Api.Common.Responses;
using HouseholdFarm.Api.Tests.Infrastructure;

namespace HouseholdFarm.Api.Tests.Common.Responses;

public class PagedQueryableExtensionsTests(PostgresFixture postgres)
{
    [Fact(DisplayName = "ToPagedResultAsync đếm tổng số dòng và lấy đúng trang trên database thật")]
    public async Task ToPagedResult_ReturnsRequestedPageAndTotalCount()
    {
        var ct = TestContext.Current.CancellationToken;
        var household = await TestData.CreateHouseholdAsync(postgres, ct);
        await using var db = postgres.CreateDbContext();
        var query = db.Users
            .Where(u => u.HouseholdId == household.Household.Id)
            .OrderBy(u => u.PhoneNumber)
            .Select(u => u.PhoneNumber);

        var result = await query.ToPagedResultAsync(new PageQuery { Page = 2, PageSize = 3 }, ct);

        Assert.Equal(2, result.Page);
        Assert.Equal(3, result.PageSize);
        Assert.Equal(4, result.TotalCount);
        var phone = Assert.Single(result.Items);
        Assert.Equal(household.Worker.PhoneNumber, phone);
    }
}
