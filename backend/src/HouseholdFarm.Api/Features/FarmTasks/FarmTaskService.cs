using HouseholdFarm.Api.Common.Auth;
using HouseholdFarm.Api.Common.Errors;
using HouseholdFarm.Api.Common.Persistence;
using HouseholdFarm.Api.Common.Responses;
using HouseholdFarm.Api.Common.Time;
using HouseholdFarm.Api.Data;
using HouseholdFarm.Api.Domain.Entities;
using HouseholdFarm.Api.Domain.Enums;
using HouseholdFarm.Api.Features.Seasons;
using HouseholdFarm.Api.Features.Supplies;
using Microsoft.EntityFrameworkCore;

namespace HouseholdFarm.Api.Features.FarmTasks;

public sealed class FarmTaskService(
    AppDbContext db,
    ICurrentUser currentUser,
    IAppClock clock,
    IConfiguration configuration,
    ISeasonStateGuard seasonStateGuard,
    IStockService stockService) : IFarmTaskService
{
    public async Task<PagedResult<FarmTaskListItem>> GetListAsync(FarmTaskQuery query, CancellationToken ct)
    {
        currentUser.EnsureHouseholdUser();

        var today = clock.Today;
        var tasks = db.FarmTasks
            .AsNoTracking()
            .VisibleTo(currentUser);

        if (query.SeasonId.HasValue)
        {
            tasks = tasks.Where(t => t.SeasonId == query.SeasonId.Value);
        }

        if (query.Mine)
        {
            var userId = currentUser.UserId;
            tasks = tasks.Where(t => t.AssigneeId == userId);
        }
        else if (query.AssigneeId.HasValue)
        {
            tasks = tasks.Where(t => t.AssigneeId == query.AssigneeId.Value);
        }

        if (query.Status.HasValue)
        {
            tasks = tasks.Where(t => t.Status == query.Status.Value);
        }

        if (query.IsOverdue.HasValue)
        {
            if (query.IsOverdue.Value)
            {
                tasks = tasks.Where(t =>
                    (t.Status == FarmTaskStatus.Pending || t.Status == FarmTaskStatus.WaitingForSupply)
                    && t.ScheduledDate < today);
            }
            else
            {
                tasks = tasks.Where(t =>
                    (t.Status != FarmTaskStatus.Pending && t.Status != FarmTaskStatus.WaitingForSupply)
                    || t.ScheduledDate >= today);
            }
        }

        if (query.From.HasValue)
        {
            tasks = tasks.Where(t => t.ScheduledDate >= query.From.Value);
        }

        if (query.To.HasValue)
        {
            tasks = tasks.Where(t => t.ScheduledDate <= query.To.Value);
        }

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var pattern = SearchPatterns.Contains(query.Search);
            tasks = tasks.Where(t =>
                EF.Functions.ILike(t.Season.Plot.Code, pattern, SearchPatterns.EscapeCharacter)
                || EF.Functions.ILike(t.Season.Crop.Name, pattern, SearchPatterns.EscapeCharacter)
                || (t.TypeOther != null && EF.Functions.ILike(t.TypeOther, pattern, SearchPatterns.EscapeCharacter))
                || (t.Note != null && EF.Functions.ILike(t.Note, pattern, SearchPatterns.EscapeCharacter))
                || (t.Assignee != null && EF.Functions.ILike(t.Assignee.FullName, pattern, SearchPatterns.EscapeCharacter)));
        }

        return await ProjectListItems(
                Sort(tasks, query.Sort).ThenBy(t => t.Id),
                today)
            .ToPagedResultAsync(query, ct);
    }

    public async Task<FarmTaskRemindersResponse> GetRemindersAsync(bool mine, CancellationToken ct)
    {
        currentUser.EnsureHouseholdUser();

        // BR-TSK-12: số ngày sắp tới đọc từ cấu hình Reminders:UpcomingDays, ngày "hôm nay" tính theo giờ Việt Nam qua IAppClock.
        var upcomingDays = Math.Max(0, configuration.GetValue("Reminders:UpcomingDays", 3));
        var today = clock.Today;
        var upcomingEnd = today.AddDays(upcomingDays);

        var tasks = db.FarmTasks
            .AsNoTracking()
            .VisibleTo(currentUser)
            .Where(t => (t.Status == FarmTaskStatus.Pending || t.Status == FarmTaskStatus.WaitingForSupply)
                        && t.ScheduledDate <= upcomingEnd);

        if (mine)
        {
            var userId = currentUser.UserId;
            tasks = tasks.Where(t => t.AssigneeId == userId);
        }

        var items = await ProjectListItems(
                tasks.OrderBy(t => t.ScheduledDate).ThenBy(t => t.Id),
                today)
            .ToListAsync(ct);

        var overdue = items.Where(t => t.ScheduledDate < today).ToList();
        var todayItems = items.Where(t => t.ScheduledDate == today).ToList();
        var upcoming = items.Where(t => t.ScheduledDate > today).ToList();

        return new FarmTaskRemindersResponse(
            today,
            upcomingDays,
            overdue.Count,
            todayItems.Count,
            upcoming.Count,
            overdue,
            todayItems,
            upcoming);
    }

    private static IQueryable<FarmTaskListItem> ProjectListItems(IQueryable<FarmTask> query, DateOnly today) =>
        query.Select(t => new FarmTaskListItem(
            t.Id,
            t.SeasonId,
            t.Season.PlotId,
            t.Season.Plot.Code,
            t.Season.CropId,
            t.Season.Crop.Name,
            t.Type,
            t.TypeOther,
            t.ScheduledDate,
            t.PlannedDurationMinutes,
            t.AssigneeId,
            t.Assignee != null ? t.Assignee.FullName : null,
            t.Status,
            (t.Status == FarmTaskStatus.Pending || t.Status == FarmTaskStatus.WaitingForSupply)
            && t.ScheduledDate < today,
            t.PerformedDate,
            t.RecurrenceGroupId));

    public async Task<IReadOnlyList<FarmTaskAssigneeOption>> GetAssigneesAsync(Guid? seasonId, CancellationToken ct)
    {
        currentUser.EnsureNotWorker();

        if (seasonId.HasValue)
        {
            var seasonExists = await db.Seasons
                .AsNoTracking()
                .VisibleTo(currentUser)
                .AnyAsync(s => s.Id == seasonId.Value, ct);
            if (!seasonExists)
            {
                throw AppException.NotFound("mùa vụ");
            }

            // BR-TSK-02: Chủ hộ không có bản ghi SeasonAssignment (BR-USR-10) nên lấy trực tiếp cùng người được phân công vào mùa vụ.
            var assignedIds = db.SeasonAssignments
                .AsNoTracking()
                .Where(a => a.SeasonId == seasonId.Value)
                .Select(a => a.UserId);

            return await db.Users
                .AsNoTracking()
                .InCurrentHousehold(currentUser)
                .Where(u => !u.IsLocked && (u.Role == UserRole.Owner || assignedIds.Contains(u.Id)))
                .OrderBy(u => u.Role)
                .ThenBy(u => u.FullName)
                .Select(u => new FarmTaskAssigneeOption(u.Id, u.FullName, u.Role))
                .ToListAsync(ct);
        }

        if (currentUser.Role == UserRole.Owner)
        {
            return await db.Users
                .AsNoTracking()
                .InCurrentHousehold(currentUser)
                .Where(u => !u.IsLocked)
                .OrderBy(u => u.Role)
                .ThenBy(u => u.FullName)
                .Select(u => new FarmTaskAssigneeOption(u.Id, u.FullName, u.Role))
                .ToListAsync(ct);
        }

        // Thành viên hộ chỉ thấy Chủ hộ và người thuộc các mùa vụ trong phạm vi phân công của mình.
        var visibleSeasonIds = db.Seasons
            .AsNoTracking()
            .VisibleTo(currentUser)
            .Select(s => s.Id);

        var visibleAssignedIds = db.SeasonAssignments
            .AsNoTracking()
            .Where(a => visibleSeasonIds.Contains(a.SeasonId))
            .Select(a => a.UserId);

        return await db.Users
            .AsNoTracking()
            .InCurrentHousehold(currentUser)
            .Where(u => !u.IsLocked && (u.Role == UserRole.Owner || visibleAssignedIds.Contains(u.Id)))
            .OrderBy(u => u.Role)
            .ThenBy(u => u.FullName)
            .Select(u => new FarmTaskAssigneeOption(u.Id, u.FullName, u.Role))
            .ToListAsync(ct);
    }

    public async Task<object> GetAsync(Guid id, CancellationToken ct)
    {
        currentUser.EnsureHouseholdUser();

        var task = await LoadTaskQuery()
            .AsNoTracking()
            .VisibleTo(currentUser)
            .FirstOrDefaultAsync(t => t.Id == id, ct)
            ?? throw AppException.NotFound("công việc");

        return await BuildDetailResultForCurrentUserAsync(task, ct);
    }

    public async Task<FarmTaskResponse> CreateAsync(CreateFarmTaskRequest request, CancellationToken ct)
    {
        currentUser.EnsureNotWorker();

        await seasonStateGuard.EnsureOperationAllowedAsync(request.SeasonId, SeasonOperation.ManageFarmTasks, forUpdate: false, ct);
        await EnsureAssigneeValidAsync(request.SeasonId, request.AssigneeId, ct);

        var supplyInputs = NormalizeSupplyInputs(request.Supplies);
        await EnsurePlannedSuppliesValidAsync(supplyInputs.Select(s => s.SupplyId), existingSupplyIds: [], ct);

        var taskType = request.Type!.Value;
        var task = new FarmTask
        {
            SeasonId = request.SeasonId,
            AssigneeId = request.AssigneeId,
            Type = taskType,
            TypeOther = taskType == FarmTaskType.Other ? TrimToNull(request.TypeOther) : null,
            ScheduledDate = request.ScheduledDate!.Value,
            PlannedDurationMinutes = request.PlannedDurationMinutes,
            Note = TrimToNull(request.Note),
            Status = FarmTaskStatus.Pending,
            SupplyUsages = supplyInputs
                .Select(s => new FarmTaskSupplyUsage
                {
                    SupplyId = s.SupplyId,
                    PlannedQuantity = s.PlannedQuantity,
                })
                .ToList(),
        };

        db.FarmTasks.Add(task);
        await db.SaveChangesAsync(ct);

        var loaded = await LoadTaskQuery().AsNoTracking().FirstAsync(t => t.Id == task.Id, ct);
        return await BuildOwnerOrMemberResponseAsync(loaded, clock.Today, ct);
    }

    public async Task<IReadOnlyList<FarmTaskResponse>> CreateRecurringAsync(
        CreateRecurringFarmTasksRequest request,
        CancellationToken ct)
    {
        currentUser.EnsureNotWorker();

        var season = await seasonStateGuard.EnsureOperationAllowedAsync(
            request.SeasonId,
            SeasonOperation.ManageFarmTasks,
            forUpdate: false,
            ct);

        var firstDate = request.FirstScheduledDate!.Value;
        var intervalDays = request.IntervalDays!.Value;

        // BR-TSK-03: ngày đầu tiên phải nhỏ hơn hoặc bằng ngày dự kiến thu hoạch của mùa vụ.
        if (firstDate > season.ExpectedHarvestDate)
        {
            throw new AppException(
                ErrorCodes.ValidationError,
                "Ngày bắt đầu lặp không được sau ngày dự kiến thu hoạch của mùa vụ.");
        }

        await EnsureAssigneeValidAsync(request.SeasonId, request.AssigneeId, ct);

        var supplyInputs = NormalizeSupplyInputs(request.Supplies);
        await EnsurePlannedSuppliesValidAsync(supplyInputs.Select(s => s.SupplyId), existingSupplyIds: [], ct);

        var groupId = Guid.CreateVersion7();
        var taskType = request.Type!.Value;
        var typeOther = taskType == FarmTaskType.Other ? TrimToNull(request.TypeOther) : null;
        var note = TrimToNull(request.Note);

        var createdTasks = new List<FarmTask>();
        for (var date = firstDate; date <= season.ExpectedHarvestDate; date = date.AddDays(intervalDays))
        {
            var task = new FarmTask
            {
                SeasonId = request.SeasonId,
                AssigneeId = request.AssigneeId,
                Type = taskType,
                TypeOther = typeOther,
                ScheduledDate = date,
                PlannedDurationMinutes = request.PlannedDurationMinutes,
                Note = note,
                Status = FarmTaskStatus.Pending,
                RecurrenceGroupId = groupId,
                SupplyUsages = supplyInputs
                    .Select(s => new FarmTaskSupplyUsage
                    {
                        SupplyId = s.SupplyId,
                        PlannedQuantity = s.PlannedQuantity,
                    })
                    .ToList(),
            };
            createdTasks.Add(task);
        }

        db.FarmTasks.AddRange(createdTasks);
        await db.SaveChangesAsync(ct);

        var createdIds = createdTasks.Select(t => t.Id).ToList();
        var loaded = await LoadTaskQuery()
            .AsNoTracking()
            .Where(t => createdIds.Contains(t.Id))
            .OrderBy(t => t.ScheduledDate)
            .ThenBy(t => t.Id)
            .ToListAsync(ct);

        var supplyIds = loaded
            .SelectMany(t => t.SupplyUsages)
            .Select(u => u.SupplyId)
            .Distinct()
            .ToList();
        var stockBySupply = await LoadStockLookupAsync(supplyIds, ct);
        var createdByName = await GetUserDisplayNameAsync(loaded[0].CreatedById, ct);
        var today = clock.Today;

        return loaded
            .Select(t => MapOwnerOrMemberResponse(t, today, createdByName, stockBySupply))
            .ToList();
    }

    public async Task<FarmTaskResponse> UpdateAsync(Guid id, UpdateFarmTaskRequest request, CancellationToken ct)
    {
        currentUser.EnsureNotWorker();

        var task = await db.FarmTasks
            .VisibleTo(currentUser)
            .Include(t => t.SupplyUsages)
            .FirstOrDefaultAsync(t => t.Id == id, ct)
            ?? throw AppException.NotFound("công việc");

        if (currentUser.Role == UserRole.Member && task.CreatedById != currentUser.UserId)
        {
            throw new AppException(ErrorCodes.Forbidden, "Thành viên hộ chỉ được sửa công việc do mình tạo.");
        }

        // BR-TSK-01: chỉ sửa khi công việc đang Chưa làm hoặc Chờ bổ sung vật tư.
        if (task.Status is not (FarmTaskStatus.Pending or FarmTaskStatus.WaitingForSupply))
        {
            throw new AppException(
                ErrorCodes.TaskInvalidState,
                "Chỉ sửa được công việc đang ở trạng thái Chưa làm hoặc Chờ bổ sung vật tư.");
        }

        await seasonStateGuard.EnsureOperationAllowedAsync(task.SeasonId, SeasonOperation.ManageFarmTasks, forUpdate: false, ct);
        await EnsureAssigneeValidAsync(task.SeasonId, request.AssigneeId, ct);

        var supplyInputs = NormalizeSupplyInputs(request.Supplies);
        var existingSupplyIds = task.SupplyUsages.Select(u => u.SupplyId).ToHashSet();
        await EnsurePlannedSuppliesValidAsync(supplyInputs.Select(s => s.SupplyId), existingSupplyIds, ct);

        // BR-GEN-11: kiểm soát xung đột cập nhật đồng thời bằng xmin.
        db.Entry(task).Property(t => t.Version).OriginalValue = request.Version;

        var taskType = request.Type!.Value;
        task.Type = taskType;
        task.TypeOther = taskType == FarmTaskType.Other ? TrimToNull(request.TypeOther) : null;
        task.ScheduledDate = request.ScheduledDate!.Value;
        task.AssigneeId = request.AssigneeId;
        task.PlannedDurationMinutes = request.PlannedDurationMinutes;
        task.Note = TrimToNull(request.Note);

        var desiredBySupply = supplyInputs.ToDictionary(s => s.SupplyId, s => s.PlannedQuantity);
        var existingBySupply = task.SupplyUsages.ToDictionary(u => u.SupplyId);

        // Xóa các dòng vật tư dự kiến không còn nằm trong danh sách mới.
        foreach (var existing in task.SupplyUsages.Where(u => !desiredBySupply.ContainsKey(u.SupplyId)).ToList())
        {
            db.FarmTaskSupplyUsages.Remove(existing);
        }

        // Cập nhật lượng dự kiến hoặc thêm dòng mới.
        foreach (var input in supplyInputs)
        {
            if (existingBySupply.TryGetValue(input.SupplyId, out var existing))
            {
                existing.PlannedQuantity = input.PlannedQuantity;
            }
            else
            {
                db.FarmTaskSupplyUsages.Add(new FarmTaskSupplyUsage
                {
                    FarmTaskId = task.Id,
                    SupplyId = input.SupplyId,
                    PlannedQuantity = input.PlannedQuantity,
                });
            }
        }

        await SaveWithConcurrencyCheckAsync(ct);

        var loaded = await LoadTaskQuery().AsNoTracking().FirstAsync(t => t.Id == task.Id, ct);
        return await BuildOwnerOrMemberResponseAsync(loaded, clock.Today, ct);
    }

    public async Task<FarmTaskResponse> CancelAsync(Guid id, CancelFarmTaskRequest request, CancellationToken ct)
    {
        currentUser.EnsureNotWorker();

        var task = await db.FarmTasks
            .VisibleTo(currentUser)
            .FirstOrDefaultAsync(t => t.Id == id, ct)
            ?? throw AppException.NotFound("công việc");

        if (currentUser.Role == UserRole.Member && task.CreatedById != currentUser.UserId)
        {
            throw new AppException(ErrorCodes.Forbidden, "Thành viên hộ chỉ được hủy công việc do mình tạo.");
        }

        // BR-TSK-01: chỉ hủy khi công việc đang Chưa làm hoặc Chờ bổ sung vật tư.
        if (task.Status is not (FarmTaskStatus.Pending or FarmTaskStatus.WaitingForSupply))
        {
            throw new AppException(
                ErrorCodes.TaskInvalidState,
                "Chỉ hủy được công việc đang ở trạng thái Chưa làm hoặc Chờ bổ sung vật tư.");
        }

        await seasonStateGuard.EnsureOperationAllowedAsync(task.SeasonId, SeasonOperation.ManageFarmTasks, forUpdate: false, ct);

        db.Entry(task).Property(t => t.Version).OriginalValue = request.Version;

        task.Status = FarmTaskStatus.Cancelled;
        task.CancelledAt = clock.UtcNow;

        await SaveWithConcurrencyCheckAsync(ct);

        var loaded = await LoadTaskQuery().AsNoTracking().FirstAsync(t => t.Id == task.Id, ct);
        return await BuildOwnerOrMemberResponseAsync(loaded, clock.Today, ct);
    }

    public async Task<CancelRecurrenceGroupResponse> CancelRecurrenceGroupAsync(Guid recurrenceGroupId, CancellationToken ct)
    {
        currentUser.EnsureNotWorker();

        var groupTasks = await db.FarmTasks
            .VisibleTo(currentUser)
            .Where(t => t.RecurrenceGroupId == recurrenceGroupId)
            .ToListAsync(ct);

        if (groupTasks.Count == 0)
        {
            throw AppException.NotFound("nhóm công việc lặp");
        }

        if (currentUser.Role == UserRole.Member && groupTasks.Any(t => t.CreatedById != currentUser.UserId))
        {
            throw new AppException(ErrorCodes.Forbidden, "Thành viên hộ chỉ được hủy nhóm công việc do mình tạo.");
        }

        var seasonId = groupTasks[0].SeasonId;
        await seasonStateGuard.EnsureOperationAllowedAsync(seasonId, SeasonOperation.ManageFarmTasks, forUpdate: false, ct);

        // BR-TSK-04: chỉ hủy các công việc còn Pending hoặc WaitingForSupply, giữ nguyên công việc Done và Cancelled.
        var cancellableTasks = groupTasks
            .Where(t => t.Status is FarmTaskStatus.Pending or FarmTaskStatus.WaitingForSupply)
            .ToList();

        if (cancellableTasks.Count == 0)
        {
            throw new AppException(
                ErrorCodes.TaskInvalidState,
                "Nhóm lặp này không còn công việc chưa thực hiện nào để hủy.");
        }

        var now = clock.UtcNow;
        foreach (var task in cancellableTasks)
        {
            task.Status = FarmTaskStatus.Cancelled;
            task.CancelledAt = now;
        }

        await SaveWithConcurrencyCheckAsync(ct);

        return new CancelRecurrenceGroupResponse(recurrenceGroupId, cancellableTasks.Count);
    }

    public async Task<object> RecordResultAsync(Guid id, RecordFarmTaskResultRequest request, CancellationToken ct)
    {
        currentUser.EnsureHouseholdUser();

        var today = clock.Today;
        var targetStatus = request.Status!.Value;

        if (request.PerformedDate.HasValue && request.PerformedDate.Value > today)
        {
            throw AppException.Validation(new Dictionary<string, string[]>
            {
                ["performedDate"] = ["Ngày thực hiện không được ở tương lai."],
            });
        }

        // BR-TSK-07: Nhân công chỉ ghi xác nhận Done, ngày thực hiện, thời lượng và ghi chú; không ghi vật tư.
        if (currentUser.Role == UserRole.Worker &&
            (targetStatus != FarmTaskStatus.Done
             || request.Supplies is { Count: > 0 }
             || !string.IsNullOrWhiteSpace(request.Reason)))
        {
            throw new AppException(
                ErrorCodes.Forbidden,
                "Nhân công chỉ được xác nhận hoàn thành và ghi chú, không được cập nhật vật tư hoặc chuyển trạng thái chờ vật tư.");
        }

        await using var tx = await db.Database.BeginTransactionAsync(ct);

        var seasonId = await db.FarmTasks
            .AsNoTracking()
            .VisibleTo(currentUser)
            .Where(t => t.Id == id)
            .Select(t => (Guid?)t.SeasonId)
            .FirstOrDefaultAsync(ct)
            ?? throw AppException.NotFound("công việc");

        // BR-GEN-12: khóa dòng mùa vụ trước khi đọc và cập nhật công việc, vật tư trong giao dịch.
        // Nhân công không thấy mùa vụ qua Seasons.VisibleTo (BR-USR-14), nên khóa và kiểm tra trực tiếp trạng thái InProgress của mùa vụ chứa việc được giao.
        if (currentUser.Role == UserRole.Worker)
        {
            await LockAndEnsureSeasonInProgressForWorkerAsync(seasonId, ct);
        }
        else
        {
            await seasonStateGuard.EnsureOperationAllowedAsync(seasonId, SeasonOperation.RecordFarmTaskResult, forUpdate: true, ct);
        }

        var task = await db.FarmTasks
            .VisibleTo(currentUser)
            .Include(t => t.SupplyUsages)
            .FirstOrDefaultAsync(t => t.Id == id, ct)
            ?? throw AppException.NotFound("công việc");

        if (currentUser.Role == UserRole.Member
            && task.CreatedById != currentUser.UserId
            && task.AssigneeId != currentUser.UserId)
        {
            throw new AppException(
                ErrorCodes.Forbidden,
                "Thành viên hộ chỉ được ghi kết quả cho công việc do mình tạo hoặc được giao cho mình.");
        }

        // BR-TSK-06: Đã hủy không thể ghi kết quả; Đã hoàn thành chỉ được bổ sung dữ liệu, không quay lại trạng thái khác.
        if (task.Status == FarmTaskStatus.Cancelled)
        {
            throw new AppException(ErrorCodes.TaskInvalidState, "Không thể ghi kết quả cho công việc đã hủy.");
        }

        if (task.Status == FarmTaskStatus.Done && targetStatus != FarmTaskStatus.Done)
        {
            throw new AppException(
                ErrorCodes.TaskInvalidState,
                "Công việc đã hoàn thành không thể chuyển sang trạng thái khác.");
        }

        // BR-GEN-11: kiểm tra version ngay sau khi khóa mùa vụ, trước khi thao tác kho.
        if (task.Version != request.Version)
        {
            throw new AppException(
                ErrorCodes.ConcurrencyConflict,
                "Công việc này vừa được người khác cập nhật. Vui lòng tải lại trước khi lưu.");
        }

        db.Entry(task).Property(t => t.Version).OriginalValue = request.Version;

        if (currentUser.Role != UserRole.Worker && request.Supplies is not null)
        {
            await ApplyActualSuppliesAsync(task, targetStatus, request.PerformedDate, request.Supplies, request.Reason, ct);
        }

        if (task.Status != FarmTaskStatus.Done && targetStatus == FarmTaskStatus.Done)
        {
            task.CompletedAt = clock.UtcNow;
        }

        task.Status = targetStatus;
        task.PerformedDate = request.PerformedDate ?? task.PerformedDate;
        task.ActualDurationMinutes = request.ActualDurationMinutes;
        task.AbnormalNote = TrimToNull(request.AbnormalNote);
        task.FieldNote = TrimToNull(request.FieldNote);

        await SaveWithConcurrencyCheckAsync(ct);
        await tx.CommitAsync(ct);

        var loaded = await LoadTaskQuery().AsNoTracking().FirstAsync(t => t.Id == task.Id, ct);
        return await BuildDetailResultForCurrentUserAsync(loaded, ct);
    }

    private async Task ApplyActualSuppliesAsync(
        FarmTask task,
        FarmTaskStatus targetStatus,
        DateOnly? performedDate,
        IReadOnlyList<FarmTaskActualSupplyInput> inputs,
        string? rawReason,
        CancellationToken ct)
    {
        if (targetStatus == FarmTaskStatus.WaitingForSupply && inputs.Any(s => s.ActualQuantity is > 0))
        {
            throw AppException.Validation(new Dictionary<string, string[]>
            {
                ["supplies"] = ["Khi chuyển công việc sang Chờ bổ sung vật tư thì không xuất kho vật tư."],
            });
        }

        var existingBySupply = task.SupplyUsages.ToDictionary(u => u.SupplyId);
        var desiredBySupply = inputs.ToDictionary(s => s.SupplyId!.Value, s => s.ActualQuantity);

        await EnsurePlannedSuppliesValidAsync(desiredBySupply.Keys, existingBySupply.Keys.ToHashSet(), ct);

        var reason = TrimToNull(rawReason);

        // Sắp xếp theo SupplyId tăng dần để tránh khóa chéo khi gọi IStockService cho nhiều vật tư.
        var allSupplyIds = existingBySupply.Keys
            .Union(desiredBySupply.Keys)
            .OrderBy(id => id)
            .ToList();

        foreach (var supplyId in allSupplyIds)
        {
            existingBySupply.TryGetValue(supplyId, out var existing);
            var inDesired = desiredBySupply.TryGetValue(supplyId, out var newActual);

            if (existing is not null && existing.StockTransactionId is { } oldTxId)
            {
                var quantityRemovedOrCleared = !inDesired || newActual is null or <= 0;
                var quantityChanged = inDesired && newActual != existing.ActualQuantity;

                if (quantityRemovedOrCleared)
                {
                    if (reason is null)
                    {
                        throw AppException.Validation(new Dictionary<string, string[]>
                        {
                            ["reason"] = ["Vui lòng nhập lý do khi hủy hoặc bỏ vật tư đã xuất kho."],
                        });
                    }

                    // BR-TSK-10: bỏ lượng thực dùng -> Void giao dịch xuất cũ.
                    await stockService.VoidFarmTaskStockOutAsync(oldTxId, reason, ct);
                    existing.StockTransactionId = null;
                    existing.ActualQuantity = null;

                    if (!inDesired || existing.PlannedQuantity is null)
                    {
                        db.FarmTaskSupplyUsages.Remove(existing);
                    }
                }
                else if (quantityChanged)
                {
                    if (reason is null)
                    {
                        throw AppException.Validation(new Dictionary<string, string[]>
                        {
                            ["reason"] = ["Vui lòng nhập lý do khi sửa lượng vật tư đã xuất kho."],
                        });
                    }

                    // BR-TSK-10: Void giao dịch cũ trước khi xuất kho lại lượng mới trong cùng giao dịch database.
                    await stockService.VoidFarmTaskStockOutAsync(oldTxId, reason, ct);
                    existing.StockTransactionId = null;
                    existing.ActualQuantity = null;

                    var newTx = await stockService.StockOutForFarmTaskAsync(
                        task.Id,
                        supplyId,
                        newActual!.Value,
                        performedDate,
                        ct);
                    existing.ActualQuantity = newActual.Value;
                    existing.StockTransactionId = newTx.Id;
                }

                // BR-TSK-08: cùng ActualQuantity với dòng đã có StockTransactionId -> không tạo giao dịch mới.
                continue;
            }

            if (!inDesired)
            {
                if (existing is not null)
                {
                    db.FarmTaskSupplyUsages.Remove(existing);
                }
                continue;
            }

            if (targetStatus == FarmTaskStatus.Done && newActual is > 0)
            {
                var createdTx = await stockService.StockOutForFarmTaskAsync(
                    task.Id,
                    supplyId,
                    newActual.Value,
                    performedDate,
                    ct);

                if (existing is not null)
                {
                    existing.ActualQuantity = newActual.Value;
                    existing.StockTransactionId = createdTx.Id;
                }
                else
                {
                    db.FarmTaskSupplyUsages.Add(new FarmTaskSupplyUsage
                    {
                        FarmTaskId = task.Id,
                        SupplyId = supplyId,
                        ActualQuantity = newActual.Value,
                        StockTransactionId = createdTx.Id,
                    });
                }
            }
            else if (existing is not null && existing.PlannedQuantity is null)
            {
                db.FarmTaskSupplyUsages.Remove(existing);
            }
        }
    }

    private async Task<object> BuildDetailResultForCurrentUserAsync(FarmTask task, CancellationToken ct)
    {
        var today = clock.Today;
        var isOverdue = ComputeIsOverdue(task.Status, task.ScheduledDate, today);

        if (currentUser.Role == UserRole.Worker)
        {
            // BR-USR-14: tách kiểu phản hồi riêng cho Nhân công để JSON không mang các trường tồn kho, người tạo hay chi phí.
            var workerSupplies = task.SupplyUsages
                .OrderBy(u => u.Supply.Name)
                .ThenBy(u => u.Id)
                .Select(u => new WorkerFarmTaskSupplyItem(
                    u.Id,
                    u.SupplyId,
                    u.Supply.Name,
                    u.Supply.Unit,
                    u.PlannedQuantity))
                .ToList();

            return new WorkerFarmTaskResponse(
                task.Id,
                task.SeasonId,
                task.Season.PlotId,
                task.Season.Plot.Code,
                task.Season.CropId,
                task.Season.Crop.Name,
                task.AssigneeId,
                task.Assignee?.FullName,
                task.Type,
                task.TypeOther,
                task.ScheduledDate,
                task.PlannedDurationMinutes,
                task.Note,
                task.Status,
                isOverdue,
                task.PerformedDate,
                task.ActualDurationMinutes,
                task.AbnormalNote,
                task.FieldNote,
                task.CompletedAt,
                task.UpdatedAt,
                task.Version,
                workerSupplies);
        }

        return await BuildOwnerOrMemberResponseAsync(task, today, ct);
    }

    private IQueryable<FarmTask> LoadTaskQuery() => db.FarmTasks
        .Include(t => t.Season).ThenInclude(s => s.Plot)
        .Include(t => t.Season).ThenInclude(s => s.Crop)
        .Include(t => t.Assignee)
        .Include(t => t.SupplyUsages).ThenInclude(u => u.Supply);

    private async Task EnsureAssigneeValidAsync(Guid seasonId, Guid? assigneeId, CancellationToken ct)
    {
        if (!assigneeId.HasValue)
        {
            return;
        }

        var user = await db.Users
            .AsNoTracking()
            .InCurrentHousehold(currentUser)
            .Where(u => u.Id == assigneeId.Value)
            .Select(u => new { u.Id, u.Role, u.IsLocked })
            .FirstOrDefaultAsync(ct);

        if (user is null || user.IsLocked)
        {
            throw new AppException(
                ErrorCodes.AssigneeNotInSeason,
                "Người thực hiện phải thuộc hộ, chưa bị khóa và được phép tham gia mùa vụ này.");
        }

        // BR-TSK-02: Chủ hộ không có bản ghi SeasonAssignment nhưng luôn hợp lệ.
        if (user.Role == UserRole.Owner)
        {
            return;
        }

        var assigned = await db.SeasonAssignments
            .AsNoTracking()
            .AnyAsync(a => a.SeasonId == seasonId && a.UserId == assigneeId.Value, ct);

        if (!assigned)
        {
            throw new AppException(
                ErrorCodes.AssigneeNotInSeason,
                "Người thực hiện chưa được phân công vào mùa vụ này.");
        }
    }

    private async Task EnsurePlannedSuppliesValidAsync(
        IEnumerable<Guid> supplyIds,
        IReadOnlyCollection<Guid> existingSupplyIds,
        CancellationToken ct)
    {
        var distinctIds = supplyIds.Distinct().ToList();
        if (distinctIds.Count == 0)
        {
            return;
        }

        var supplies = await db.Supplies
            .AsNoTracking()
            .Where(s => distinctIds.Contains(s.Id))
            .Select(s => new { s.Id, s.Name, s.Status })
            .ToListAsync(ct);

        if (supplies.Count != distinctIds.Count)
        {
            throw AppException.NotFound("vật tư");
        }

        // BR-GEN-13: vật tư mới thêm vào công việc phải ở trạng thái Active; vật tư đã gắn từ trước được giữ nguyên khi sửa.
        foreach (var supply in supplies)
        {
            if (supply.Status != RecordStatus.Active && !existingSupplyIds.Contains(supply.Id))
            {
                throw new AppException(
                    ErrorCodes.CatalogInactive,
                    $"Vật tư '{supply.Name}' đang ngừng sử dụng, không thể chọn cho công việc.");
            }
        }
    }

    private static List<(Guid SupplyId, decimal PlannedQuantity)> NormalizeSupplyInputs(
        IReadOnlyList<FarmTaskPlannedSupplyInput>? supplies)
    {
        if (supplies is null || supplies.Count == 0)
        {
            return [];
        }

        return supplies
            .Select(s => (SupplyId: s.SupplyId!.Value, PlannedQuantity: s.PlannedQuantity!.Value))
            .ToList();
    }

    private async Task<FarmTaskResponse> BuildOwnerOrMemberResponseAsync(FarmTask task, DateOnly today, CancellationToken ct)
    {
        var supplyIds = task.SupplyUsages.Select(u => u.SupplyId).Distinct().ToList();
        var stockBySupply = await LoadStockLookupAsync(supplyIds, ct);
        var createdByName = await GetUserDisplayNameAsync(task.CreatedById, ct);

        return MapOwnerOrMemberResponse(task, today, createdByName, stockBySupply);
    }

    private async Task<IReadOnlyDictionary<Guid, SupplyStockResponse>> LoadStockLookupAsync(
        IReadOnlyCollection<Guid> supplyIds,
        CancellationToken ct)
    {
        if (supplyIds.Count == 0)
        {
            return new Dictionary<Guid, SupplyStockResponse>();
        }

        var stocks = await stockService.GetStockAsync(supplyIds, ct);
        return stocks.ToDictionary(s => s.SupplyId);
    }

    private async Task<string?> GetUserDisplayNameAsync(Guid userId, CancellationToken ct) =>
        await db.Users
            .AsNoTracking()
            .InCurrentHousehold(currentUser)
            .Where(u => u.Id == userId)
            .Select(u => u.FullName)
            .FirstOrDefaultAsync(ct);

    private static FarmTaskResponse MapOwnerOrMemberResponse(
        FarmTask task,
        DateOnly today,
        string? createdByName,
        IReadOnlyDictionary<Guid, SupplyStockResponse> stockBySupply)
    {
        var supplies = task.SupplyUsages
            .OrderBy(u => u.Supply.Name)
            .ThenBy(u => u.Id)
            .Select(u =>
            {
                stockBySupply.TryGetValue(u.SupplyId, out var stock);
                return new FarmTaskSupplyUsageItem(
                    u.Id,
                    u.SupplyId,
                    u.Supply.Name,
                    u.Supply.Unit,
                    u.PlannedQuantity,
                    u.ActualQuantity,
                    u.StockTransactionId,
                    stock?.StockQuantity ?? 0m,
                    stock?.MinStockLevel ?? 0m);
            })
            .ToList();

        return new FarmTaskResponse(
            task.Id,
            task.SeasonId,
            task.Season.PlotId,
            task.Season.Plot.Code,
            task.Season.CropId,
            task.Season.Crop.Name,
            task.AssigneeId,
            task.Assignee?.FullName,
            task.Type,
            task.TypeOther,
            task.ScheduledDate,
            task.PlannedDurationMinutes,
            task.Note,
            task.Status,
            ComputeIsOverdue(task.Status, task.ScheduledDate, today),
            task.PerformedDate,
            task.ActualDurationMinutes,
            task.AbnormalNote,
            task.FieldNote,
            task.CompletedAt,
            task.CancelledAt,
            task.RecurrenceGroupId,
            task.CreatedAt,
            task.CreatedById,
            createdByName,
            task.UpdatedAt,
            task.Version,
            supplies);
    }

    private async Task LockAndEnsureSeasonInProgressForWorkerAsync(Guid seasonId, CancellationToken ct)
    {
        if (currentUser.HouseholdId is { } householdId)
        {
            await db.Database.ExecuteSqlAsync(
                $"SELECT 1 FROM seasons WHERE id = {seasonId} AND household_id = {householdId} FOR UPDATE",
                ct);
        }

        var status = await db.Seasons
            .AsNoTracking()
            .Where(s => s.Id == seasonId)
            .Select(s => (SeasonStatus?)s.Status)
            .FirstOrDefaultAsync(ct)
            ?? throw AppException.NotFound("công việc");

        if (status != SeasonStatus.InProgress)
        {
            throw new AppException(
                ErrorCodes.SeasonInvalidState,
                "Chỉ ghi được kết quả cho công việc thuộc mùa vụ đang canh tác.");
        }
    }

    private static bool ComputeIsOverdue(FarmTaskStatus status, DateOnly scheduledDate, DateOnly today) =>
        (status is FarmTaskStatus.Pending or FarmTaskStatus.WaitingForSupply) && scheduledDate < today;

    private async Task SaveWithConcurrencyCheckAsync(CancellationToken ct)
    {
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new AppException(
                ErrorCodes.ConcurrencyConflict,
                "Công việc này vừa được người khác cập nhật. Vui lòng tải lại trước khi lưu.");
        }
    }

    private static IOrderedQueryable<FarmTask> Sort(IQueryable<FarmTask> query, string? sort)
    {
        var descending = sort?.StartsWith('-') == true;
        var field = sort?.TrimStart('-');

        return (field, descending) switch
        {
            ("scheduledDate", true) => query.OrderByDescending(t => t.ScheduledDate),
            ("status", false) => query.OrderBy(t => t.Status).ThenBy(t => t.ScheduledDate),
            ("status", true) => query.OrderByDescending(t => t.Status).ThenBy(t => t.ScheduledDate),
            ("createdAt", false) => query.OrderBy(t => t.CreatedAt),
            ("createdAt", true) => query.OrderByDescending(t => t.CreatedAt),
            _ => query.OrderBy(t => t.ScheduledDate),
        };
    }

    private static string? TrimToNull(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
