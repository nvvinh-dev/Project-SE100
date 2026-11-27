using HouseholdFarm.Api.Common.Auth;
using HouseholdFarm.Api.Common.Errors;
using HouseholdFarm.Api.Common.Time;
using HouseholdFarm.Api.Data;
using HouseholdFarm.Api.Domain.Entities;
using HouseholdFarm.Api.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace HouseholdFarm.Api.Features.Ai;

public sealed class AIUsageService(
    AppDbContext db,
    ICurrentUser currentUser,
    IAppClock clock,
    IOptions<AIOptions> options) : IAIUsageService
{
    public async Task<AIQuotaResponse> GetQuotaAsync(CancellationToken ct)
    {
        // BR-AI-01: Nhân công không dùng tính năng AI.
        currentUser.EnsureNotWorker();
        return await CountAsync(clock.Today, ct);
    }

    public async Task EnsureAvailableAsync(CancellationToken ct)
    {
        currentUser.EnsureNotWorker();

        // BR-AI-04: đếm không khóa dòng, vì khóa dòng của hộ sẽ làm chậm mọi thao tác khác của hộ.
        var quota = await CountAsync(clock.Today, ct);
        if (quota.Remaining <= 0)
        {
            throw new AppException(
                ErrorCodes.AIQuotaExceeded,
                $"Hộ đã dùng hết {quota.Limit} lượt AI hôm nay. Tính năng AI mở lại vào ngày mai; các chức năng khác vẫn dùng bình thường.",
                quota);
        }
    }

    public async Task<AIQuotaResponse> RecordAsync(AIFeature feature, CancellationToken ct)
    {
        currentUser.EnsureNotWorker();

        // Ngày theo giờ Việt Nam, để lượt được làm mới đúng nửa đêm ở Việt Nam. household_id, created_at do interceptor điền.
        var today = clock.Today;
        db.AIUsages.Add(new AIUsage { UserId = currentUser.UserId, Feature = feature, UsageDate = today });
        await db.SaveChangesAsync(ct);
        return await CountAsync(today, ct);
    }

    private async Task<AIQuotaResponse> CountAsync(DateOnly today, CancellationToken ct)
    {
        // Global Query Filter đã lọc theo hộ.
        var used = await db.AIUsages.CountAsync(u => u.UsageDate == today, ct);
        var limit = options.Value.DailyQuotaPerHousehold;
        return new AIQuotaResponse(today, used, limit, Math.Max(0, limit - used));
    }
}
