using System.Net;
using System.Text.Json;
using HouseholdFarm.Api.Domain.Entities;
using HouseholdFarm.Api.Domain.Enums;
using HouseholdFarm.Api.Tests.Common.Probe;
using HouseholdFarm.Api.Tests.Infrastructure;

namespace HouseholdFarm.Api.Tests.Common.Auth;

public class CurrentUserMiddlewareTests(ProbeApiFactory factory) : IClassFixture<ProbeApiFactory>
{
    private const string Path = "/api/probe/current-user";

    [Fact(DisplayName = "D-11: người dùng hiện tại nạp từ database có hộ, vai trò và mùa vụ được phân công")]
    public async Task Load_Member_HasHouseholdRoleAndAssignedSeasons()
    {
        var ct = TestContext.Current.CancellationToken;
        var household = await factory.CreateHouseholdAsync(ct);
        var seasonId = await CreateSeasonAsync(household, assignTo: household.Member, ct);

        var member = await GetAsync(household.Member, ct);
        var owner = await GetAsync(household.Owner, ct);

        Assert.Equal(household.Member.Id, member.GetProperty("userId").GetGuid());
        Assert.Equal(household.Household.Id, member.GetProperty("householdId").GetGuid());
        Assert.Equal("Member", member.GetProperty("role").GetString());
        Assert.Equal([seasonId], Ids(member, "assignedSeasonIds"));
        // Chủ hộ thấy cả hộ nên không cần phân công.
        Assert.Equal("Owner", owner.GetProperty("role").GetString());
        Assert.Empty(Ids(owner, "assignedSeasonIds"));
    }

    [Fact(DisplayName = "D-11: phân công mới có hiệu lực ngay ở yêu cầu kế tiếp, không chờ access token hết hạn")]
    public async Task Load_AfterAssignment_SeesNewSeasonWithSameToken()
    {
        var ct = TestContext.Current.CancellationToken;
        var household = await factory.CreateHouseholdAsync(ct);
        var client = factory.CreateClientFor(household.Worker);
        Assert.Empty(Ids(await ReadDataAsync(client, ct), "assignedSeasonIds"));

        var seasonId = await CreateSeasonAsync(household, assignTo: household.Worker, ct);

        Assert.Equal([seasonId], Ids(await ReadDataAsync(client, ct), "assignedSeasonIds"));
    }

    [Fact(DisplayName = "BR-GEN-01: Global Query Filter lấy hộ từ người đăng nhập, không thấy thửa của hộ khác")]
    public async Task Load_GlobalQueryFilter_UsesHouseholdOfSignedInUser()
    {
        var ct = TestContext.Current.CancellationToken;
        var a = await factory.CreateHouseholdAsync(ct);
        var b = await factory.CreateHouseholdAsync(ct);
        var plotA = await CreatePlotAsync(a, ct);
        await CreatePlotAsync(b, ct);

        var owner = await GetAsync(a.Owner, ct);

        Assert.Equal([plotA.Id], Ids(owner, "visiblePlotIds"));
    }

    private async Task<JsonElement> GetAsync(ApplicationUser user, CancellationToken ct) =>
        await ReadDataAsync(factory.CreateClientFor(user), ct);

    private static async Task<JsonElement> ReadDataAsync(HttpClient client, CancellationToken ct)
    {
        var response = await client.GetAsync(Path, ct);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.ReadJsonAsync(ct)).GetProperty("data");
    }

    private static List<Guid> Ids(JsonElement data, string property) =>
        data.GetProperty(property).EnumerateArray().Select(e => e.GetGuid()).ToList();

    private async Task<Plot> CreatePlotAsync(TestHousehold household, CancellationToken ct)
    {
        await using var db = factory.Postgres.CreateDbContext(TestCurrentUser.For(household.Owner));
        var plot = new Plot
        {
            Code = "TD001",
            AreaM2 = 1500m,
            SoilType = SoilType.Alluvial,
            WaterSource = WaterSource.RiverCanal,
            Status = RecordStatus.Active,
        };
        db.Plots.Add(plot);
        await db.SaveChangesAsync(ct);
        return plot;
    }

    private async Task<Guid> CreateSeasonAsync(TestHousehold household, ApplicationUser assignTo, CancellationToken ct)
    {
        var plot = await CreatePlotAsync(household, ct);
        await using var db = factory.Postgres.CreateDbContext(TestCurrentUser.For(household.Owner));
        var season = new Season
        {
            PlotId = plot.Id,
            Crop = new Crop { Name = "Lúa ST25", Group = CropGroup.Staple, YieldUnit = Unit.Kilogram, Status = RecordStatus.Active },
            StartDate = new DateOnly(2026, 9, 1),
            ExpectedHarvestDate = new DateOnly(2026, 12, 1),
            TargetYield = 3000m,
            Status = SeasonStatus.InProgress,
        };
        db.Seasons.Add(season);
        db.SeasonAssignments.Add(new SeasonAssignment { Season = season, UserId = assignTo.Id });
        await db.SaveChangesAsync(ct);
        return season.Id;
    }
}
