using System.Net.Http.Headers;
using HouseholdFarm.Api.Common.Auth;
using HouseholdFarm.Api.Common.Time;
using HouseholdFarm.Api.Domain.Entities;
using HouseholdFarm.Api.Domain.Enums;
using HouseholdFarm.Api.Features.Ai;
using HouseholdFarm.Api.Tests.Infrastructure;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace HouseholdFarm.Api.Tests.Features.Ai;

public static class AiTestHelpers
{
    public const string QuotaPath = "/api/ai/quota";

    public const string AssistantPath = "/api/ai/assistant";

    /// <summary>Ứng dụng dùng <paramref name="ai"/> thay cho Gemini; <paramref name="utcNow"/> để giả lập ngày giờ.</summary>
    public static WebApplicationFactory<Program> WithFakeAI(
        this ApiFactory factory,
        FakeAIClient ai,
        DateTimeOffset? utcNow = null) =>
        factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
        {
            services.AddSingleton<IAIClient>(ai);
            if (utcNow is { } now)
            {
                services.AddSingleton<TimeProvider>(new FixedTimeProvider(now));
            }
        }));

    public static HttpClient ClientFor(this WebApplicationFactory<Program> app, ApplicationUser user)
    {
        var client = app.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            app.Services.GetRequiredService<AccessTokenIssuer>().Create(user.Id));
        return client;
    }

    /// <summary>Ngày hôm nay theo giờ Việt Nam của ứng dụng.</summary>
    public static DateOnly Today(this WebApplicationFactory<Program> app) => app.Services.GetRequiredService<IAppClock>().Today;

    /// <summary>Ghi sẵn <paramref name="count"/> lượt AI của hộ vào ngày <paramref name="usageDate"/>.</summary>
    public static async Task SeedUsagesAsync(
        PostgresFixture postgres,
        TestHousehold household,
        DateOnly usageDate,
        int count,
        CancellationToken ct)
    {
        await using var db = postgres.CreateDbContext(TestCurrentUser.For(household.Owner));
        for (var i = 0; i < count; i++)
        {
            db.AIUsages.Add(new AIUsage { UserId = household.Owner.Id, Feature = AIFeature.Assistant, UsageDate = usageDate });
        }

        await db.SaveChangesAsync(ct);
    }

    public static async Task<List<AIUsage>> UsagesAsync(PostgresFixture postgres, TestHousehold household, CancellationToken ct)
    {
        await using var db = postgres.CreateDbContext(TestCurrentUser.For(household.Owner));
        return await db.AIUsages.AsNoTracking().ToListAsync(ct);
    }

    public static object Ask(string question, params (string Question, string Answer)[] history) => new
    {
        question,
        history = history.Select(h => new { question = h.Question, answer = h.Answer }).ToArray(),
    };
}
