using HouseholdFarm.Api.Common.Auditing;
using HouseholdFarm.Api.Common.Auth;
using HouseholdFarm.Api.Common.Errors;
using HouseholdFarm.Api.Data;
using HouseholdFarm.Api.Domain.Entities;
using HouseholdFarm.Api.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace HouseholdFarm.Api.Features.Seasons;

public sealed class SeasonAssignmentService(
    AppDbContext db,
    ICurrentUser currentUser,
    IAuditLogger audit) : ISeasonAssignmentService
{
    public async Task<IReadOnlyList<SeasonAssignmentResponse>> GetListAsync(Guid seasonId, CancellationToken ct)
    {
        // Ma trận phân quyền: Nhân công không xem danh sách phân công
        currentUser.EnsureNotWorker();

        // Kiểm tra quyền truy cập mùa vụ
        var isOwner = currentUser.Role == UserRole.Owner;
        if (!isOwner && !currentUser.AssignedSeasonIds.Contains(seasonId))
        {
            throw AppException.NotFound("mùa vụ");
        }

        return await db.SeasonAssignments
            .AsNoTracking()
            .Where(a => a.SeasonId == seasonId)
            .OrderBy(a => a.CreatedAt)
            .Select(a => new SeasonAssignmentResponse(
                a.Id,
                a.SeasonId,
                a.UserId,
                a.User.FullName,
                a.User.Role,
                a.User.PhoneNumber ?? string.Empty,
                a.CreatedAt))
            .ToListAsync(ct);
    }

    public async Task<SeasonAssignmentResponse> AssignAsync(CreateSeasonAssignmentRequest request, CancellationToken ct)
    {
        // Ma trận phân quyền: chỉ Chủ hộ được phân công (FR-SEA-06)
        currentUser.EnsureOwner();

        var season = await db.Seasons
            .FirstOrDefaultAsync(s => s.Id == request.SeasonId, ct)
            ?? throw AppException.NotFound("mùa vụ");

        // BR-USR-11: Phân công chỉ khi mùa vụ Planned hoặc InProgress
        if (season.Status is not (SeasonStatus.Planned or SeasonStatus.InProgress))
        {
            throw new AppException(
                ErrorCodes.SeasonInvalidState,
                "Chỉ có thể phân công khi mùa vụ đang ở trạng thái 'Lên kế hoạch' hoặc 'Đang canh tác'.");
        }

        var user = await db.Users
            .FirstOrDefaultAsync(u => u.Id == request.UserId && u.HouseholdId == currentUser.HouseholdId, ct)
            ?? throw AppException.NotFound("thành viên");

        // BR-USR-10: Chủ hộ không cần phân công vì thấy toàn bộ dữ liệu hộ
        if (user.Role == UserRole.Owner)
        {
            throw new AppException(
                ErrorCodes.ValidationError,
                "Chủ hộ không cần phân công vì có quyền xem toàn bộ dữ liệu của hộ.");
        }

        // BR-USR-10: Chỉ phân công Member và Worker
        if (user.Role is not (UserRole.Member or UserRole.Worker))
        {
            throw new AppException(
                ErrorCodes.ValidationError,
                "Chỉ có thể phân công Thành viên hộ hoặc Nhân công.");
        }

        // BR-USR-10: Tài khoản chưa bị khóa
        if (user.IsLocked)
        {
            throw new AppException(
                ErrorCodes.ValidationError,
                "Không thể phân công tài khoản đang bị khóa.");
        }

        var existing = await db.SeasonAssignments
            .FirstOrDefaultAsync(a => a.SeasonId == request.SeasonId && a.UserId == request.UserId, ct);

        if (existing is not null)
        {
            throw new AppException(
                ErrorCodes.ValidationError,
                "Người này đã được phân công vào mùa vụ.");
        }

        var assignment = new SeasonAssignment
        {
            SeasonId = request.SeasonId,
            UserId = request.UserId,
        };

        db.SeasonAssignments.Add(assignment);

        audit.Log(
            AuditActions.SeasonAssignmentAssign,
            assignment.Id,
            oldValues: null,
            newValues: new { seasonId = assignment.SeasonId, userId = assignment.UserId, user.FullName },
            reason: null);

        await db.SaveChangesAsync(ct);

        return new SeasonAssignmentResponse(
            assignment.Id,
            assignment.SeasonId,
            user.Id,
            user.FullName,
            user.Role,
            user.PhoneNumber ?? string.Empty,
            assignment.CreatedAt);
    }

    public async Task UnassignAsync(Guid id, UnassignSeasonAssignmentRequest request, CancellationToken ct)
    {
        // Ma trận phân quyền: chỉ Chủ hộ được thu hồi phân công (FR-SEA-06)
        currentUser.EnsureOwner();

        var assignment = await db.SeasonAssignments
            .Include(a => a.Season)
            .Include(a => a.User)
            .FirstOrDefaultAsync(a => a.Id == id, ct)
            ?? throw AppException.NotFound("phân công");

        // BR-USR-11: Thu hồi phân công chỉ khi mùa vụ Planned hoặc InProgress
        if (assignment.Season.Status is not (SeasonStatus.Planned or SeasonStatus.InProgress))
        {
            throw new AppException(
                ErrorCodes.SeasonInvalidState,
                "Chỉ có thể thu hồi phân công khi mùa vụ đang ở trạng thái 'Lên kế hoạch' hoặc 'Đang canh tác'.");
        }

        // BR-USR-12: Kiểm tra người này còn công việc Pending hoặc WaitingForSupply trong mùa vụ
        var pendingTasks = await db.FarmTasks
            .Where(t => t.SeasonId == assignment.SeasonId
                        && t.AssigneeId == assignment.UserId
                        && (t.Status == FarmTaskStatus.Pending || t.Status == FarmTaskStatus.WaitingForSupply))
            .OrderBy(t => t.ScheduledDate)
            .Select(t => new
            {
                t.Id,
                t.Type,
                t.TypeOther,
                t.ScheduledDate,
            })
            .ToListAsync(ct);

        if (pendingTasks.Count > 0 && !request.Confirmed)
        {
            // BR-GEN-14: Yêu cầu xác nhận kèm danh sách ảnh hưởng
            var items = pendingTasks.Select(t => new
            {
                id = t.Id,
                label = $"{FormatTaskType(t.Type, t.TypeOther)} · {t.ScheduledDate:dd/MM/yyyy}",
            }).ToList();

            throw new AppException(
                ErrorCodes.ConfirmationRequired,
                $"Người này còn {pendingTasks.Count} công việc chưa hoàn thành trong mùa vụ. Bạn có chắc chắn muốn thu hồi phân công?",
                new { items });
        }

        // BR-USR-12: Khi đã xác nhận, chuyển các công việc chưa làm về chưa giao người (assignee_id = null)
        if (pendingTasks.Count > 0)
        {
            var taskIds = pendingTasks.Select(t => t.Id).ToList();
            var tasksToUpdate = await db.FarmTasks
                .Where(t => taskIds.Contains(t.Id))
                .ToListAsync(ct);

            foreach (var task in tasksToUpdate)
            {
                task.AssigneeId = null;
            }
        }

        db.SeasonAssignments.Remove(assignment);

        audit.Log(
            AuditActions.SeasonAssignmentUnassign,
            assignment.Id,
            oldValues: new { seasonId = assignment.SeasonId, userId = assignment.UserId, assignment.User.FullName },
            newValues: null,
            reason: null);

        await db.SaveChangesAsync(ct);
    }

    private static string FormatTaskType(FarmTaskType type, string? typeOther) => type switch
    {
        FarmTaskType.LandPreparation => "Làm đất",
        FarmTaskType.Sowing => "Gieo trồng",
        FarmTaskType.Watering => "Tưới nước",
        FarmTaskType.Fertilizing => "Bón phân",
        FarmTaskType.Weeding => "Làm cỏ",
        FarmTaskType.PestInspection => "Kiểm tra sâu bệnh",
        FarmTaskType.Spraying => "Phun thuốc",
        FarmTaskType.Other => string.IsNullOrWhiteSpace(typeOther) ? "Khác" : typeOther,
        _ => type.ToString(),
    };
}
