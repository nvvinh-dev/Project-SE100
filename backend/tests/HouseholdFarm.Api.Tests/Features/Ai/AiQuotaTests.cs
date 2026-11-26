using System.Net;
using System.Net.Http.Json;
using HouseholdFarm.Api.Common.Errors;
using HouseholdFarm.Api.Features.Ai;
using HouseholdFarm.Api.Tests.Infrastructure;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using static HouseholdFarm.Api.Tests.Features.Ai.AiTestHelpers;

namespace HouseholdFarm.Api.Tests.Features.Ai;

/// <summary>Nền tảng AI: giới hạn lượt theo hộ, theo ngày (BR-AI-04), AI lỗi (BR-AI-08), quyền (BR-AI-01) (FR-AI-05).</summary>
public class AiQuotaTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    [Fact(DisplayName = "BR-AI-04: GET /api/ai/quota trả số lượt đã dùng hôm nay của hộ, không tính lượt ngày khác và hộ khác")]
    public async Task GetQuota_ReturnsTodayUsageOfHousehold()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var app = factory.WithFakeAI(new FakeAIClient());
        var household = await factory.CreateHouseholdAsync(ct);
        var otherHousehold = await factory.CreateHouseholdAsync(ct);
        var today = app.Today();
        await SeedUsagesAsync(factory.Postgres, household, today, 3, ct);
        await SeedUsagesAsync(factory.Postgres, household, today.AddDays(-1), 5, ct);
        await SeedUsagesAsync(factory.Postgres, otherHousehold, today, 7, ct);

        var response = await app.ClientFor(household.Member).GetAsync(QuotaPath, ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var data = (await response.ReadJsonAsync(ct)).GetProperty("data");
        Assert.Equal(today.ToString("yyyy-MM-dd"), data.GetProperty("usageDate").GetString());
        Assert.Equal(3, data.GetProperty("used").GetInt32());
        Assert.Equal(50, data.GetProperty("limit").GetInt32());
        Assert.Equal(47, data.GetProperty("remaining").GetInt32());
    }

    [Fact(DisplayName = "TC-26: hộ đã dùng hết lượt AI trong ngày thì lượt mới bị từ chối AI_QUOTA_EXCEEDED; tạo phiếu thu vẫn thành công")]
    public async Task TC26_Ask_WhenQuotaUsedUp_ReturnsAIQuotaExceeded_OtherFeaturesStillWork()
    {
        var ct = TestContext.Current.CancellationToken;
        var ai = new FakeAIClient().ThenText("Không được gọi tới.");
        await using var app = factory.WithFakeAI(ai);
        var household = await factory.CreateHouseholdAsync(ct);
        var plot = await TestData.CreatePlotAsync(factory.Postgres, household, "TD001", ct);
        var season = await TestData.CreateSeasonAsync(factory.Postgres, household, plot, Domain.Enums.SeasonStatus.InProgress, ct);
        await SeedUsagesAsync(factory.Postgres, household, app.Today(), 50, ct);
        var client = app.ClientFor(household.Owner);

        var askResponse = await client.PostAsJsonAsync(AssistantPath, Ask("Mùa lúa thu được bao nhiêu?"), ct);
        var harvestResponse = await client.PostAsJsonAsync(
            "/api/harvest-records",
            new
            {
                requestId = Guid.CreateVersion7(),
                seasonId = season.Id,
                harvestDate = "2026-10-20",
                qualityGrade = "Grade1",
                quantity = 100m,
            },
            ct);

        await askResponse.AssertProblemAsync(HttpStatusCode.TooManyRequests, ErrorCodes.AIQuotaExceeded, ct);
        Assert.Empty(ai.Requests);
        Assert.Equal(HttpStatusCode.Created, harvestResponse.StatusCode);
        Assert.Equal(50, (await UsagesAsync(factory.Postgres, household, ct)).Count);
    }

    [Fact(DisplayName = "BR-AI-08: dịch vụ AI lỗi thì trả AI_UNAVAILABLE và không trừ lượt")]
    public async Task Ask_WhenAIFails_ReturnsAIUnavailable_AndDoesNotCountUsage()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var app = factory.WithFakeAI(new FakeAIClient().ThenUnavailable());
        var household = await factory.CreateHouseholdAsync(ct);
        var client = app.ClientFor(household.Owner);

        var response = await client.PostAsJsonAsync(AssistantPath, Ask("Hôm nay có việc gì?"), ct);
        var quota = (await (await client.GetAsync(QuotaPath, ct)).ReadJsonAsync(ct)).GetProperty("data");

        await response.AssertProblemAsync(HttpStatusCode.ServiceUnavailable, ErrorCodes.AIUnavailable, ct);
        Assert.Empty(await UsagesAsync(factory.Postgres, household, ct));
        Assert.Equal(0, quota.GetProperty("used").GetInt32());
    }

    [Fact(DisplayName = "BR-AI-08: chưa cấu hình khóa API thì bản hiện thực Gemini báo AI_UNAVAILABLE, không gọi ra ngoài")]
    public async Task GeminiClient_WithoutApiKey_ThrowsAIUnavailable()
    {
        var client = new GeminiAIClient(
            Options.Create(new AIOptions { Model = "gemini-3.5-flash-lite" }),
            NullLogger<GeminiAIClient>.Instance);

        var ex = await Assert.ThrowsAsync<AppException>(() => client.GenerateAsync(
            new AIRequest("Chỉ dẫn", [new AIUserTurn("Câu hỏi")], []),
            TestContext.Current.CancellationToken));

        Assert.Equal(ErrorCodes.AIUnavailable, ex.Code);
    }

    [Fact(DisplayName = "BR-AI-04: sang ngày mới theo giờ Việt Nam thì lượt được đặt lại")]
    public async Task Ask_OnNextDay_QuotaIsReset()
    {
        var ct = TestContext.Current.CancellationToken;
        var now = TimeProvider.System.GetUtcNow();
        await using var today = factory.WithFakeAI(new FakeAIClient(), now);
        var ai = new FakeAIClient().ThenText("Chưa có công việc nào.");
        await using var tomorrow = factory.WithFakeAI(ai, now.AddDays(1));
        var household = await factory.CreateHouseholdAsync(ct);
        await SeedUsagesAsync(factory.Postgres, household, today.Today(), 50, ct);

        var todayResponse = await today.ClientFor(household.Owner).PostAsJsonAsync(AssistantPath, Ask("Có việc gì?"), ct);
        var tomorrowResponse = await tomorrow.ClientFor(household.Owner).PostAsJsonAsync(AssistantPath, Ask("Có việc gì?"), ct);

        await todayResponse.AssertProblemAsync(HttpStatusCode.TooManyRequests, ErrorCodes.AIQuotaExceeded, ct);
        Assert.Equal(HttpStatusCode.OK, tomorrowResponse.StatusCode);
        var quota = (await tomorrowResponse.ReadJsonAsync(ct)).GetProperty("data").GetProperty("quota");
        Assert.Equal(tomorrow.Today().ToString("yyyy-MM-dd"), quota.GetProperty("usageDate").GetString());
        Assert.Equal(1, quota.GetProperty("used").GetInt32());
        Assert.Equal(49, quota.GetProperty("remaining").GetInt32());
    }

    [Fact(DisplayName = "BR-AI-01: Nhân công xem lượt hoặc hỏi trợ lý đều bị từ chối FORBIDDEN, không gọi dịch vụ AI")]
    public async Task Worker_QuotaAndAsk_ReturnForbidden()
    {
        var ct = TestContext.Current.CancellationToken;
        var ai = new FakeAIClient().ThenText("Không được gọi tới.");
        await using var app = factory.WithFakeAI(ai);
        var household = await factory.CreateHouseholdAsync(ct);
        var client = app.ClientFor(household.Worker);

        var quota = await client.GetAsync(QuotaPath, ct);
        var ask = await client.PostAsJsonAsync(AssistantPath, Ask("Có việc gì?"), ct);

        await quota.AssertProblemAsync(HttpStatusCode.Forbidden, ErrorCodes.Forbidden, ct);
        await ask.AssertProblemAsync(HttpStatusCode.Forbidden, ErrorCodes.Forbidden, ct);
        Assert.Empty(ai.Requests);
    }
}
