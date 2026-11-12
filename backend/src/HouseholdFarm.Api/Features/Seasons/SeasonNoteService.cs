using HouseholdFarm.Api.Common.Auth;
using HouseholdFarm.Api.Common.Errors;
using HouseholdFarm.Api.Data;
using HouseholdFarm.Api.Domain.Entities;
using HouseholdFarm.Api.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace HouseholdFarm.Api.Features.Seasons;

public sealed class SeasonNoteService(
    AppDbContext db,
    ICurrentUser currentUser) : ISeasonNoteService
{
    public async Task<IReadOnlyList<SeasonNoteResponse>> GetListAsync(Guid seasonId, CancellationToken ct)
    {
        // Ma trận phân quyền: Nhân công không xem ghi chú mùa vụ (BR-GEN-02)
        if (currentUser.Role == UserRole.Worker)
        {
            throw AppException.NotFound("mùa vụ");
        }

        // Kiểm tra quyền truy cập mùa vụ
        var isOwner = currentUser.Role == UserRole.Owner;
        if (!isOwner && !currentUser.AssignedSeasonIds.Contains(seasonId))
        {
            throw AppException.NotFound("mùa vụ");
        }

        var notes = await db.SeasonNotes
            .AsNoTracking()
            .Where(n => n.SeasonId == seasonId)
            .OrderByDescending(n => n.CreatedAt)
            .Select(n => new
            {
                n.Id,
                n.SeasonId,
                n.Content,
                n.CreatedById,
                AuthorName = db.Users.Where(u => u.Id == n.CreatedById).Select(u => u.FullName).FirstOrDefault() ?? "Người dùng",
                n.CreatedAt,
                n.UpdatedAt,
                n.Version,
            })
            .ToListAsync(ct);

        return notes.Select(n => new SeasonNoteResponse(
            n.Id,
            n.SeasonId,
            n.Content,
            n.CreatedById,
            n.AuthorName,
            n.CreatedAt,
            n.UpdatedAt,
            n.Version,
            n.CreatedById == currentUser.UserId)).ToList();
    }

    public async Task<SeasonNoteResponse> CreateAsync(CreateSeasonNoteRequest request, CancellationToken ct)
    {
        if (currentUser.Role == UserRole.Worker)
        {
            throw AppException.NotFound("mùa vụ");
        }

        var isOwner = currentUser.Role == UserRole.Owner;
        if (!isOwner && !currentUser.AssignedSeasonIds.Contains(request.SeasonId))
        {
            throw AppException.NotFound("mùa vụ");
        }

        var seasonExists = await db.Seasons
            .AnyAsync(s => s.Id == request.SeasonId, ct);
        if (!seasonExists)
        {
            throw AppException.NotFound("mùa vụ");
        }

        // BR-SEA-11: Ghi chú kinh nghiệm được ghi ở mọi trạng thái mùa vụ
        var trimmedContent = request.Content.Trim();

        var note = new SeasonNote
        {
            SeasonId = request.SeasonId,
            Content = trimmedContent,
        };

        db.SeasonNotes.Add(note);
        await db.SaveChangesAsync(ct);

        var authorName = await db.Users
            .Where(u => u.Id == currentUser.UserId)
            .Select(u => u.FullName)
            .FirstOrDefaultAsync(ct) ?? "Người dùng";

        return new SeasonNoteResponse(
            note.Id,
            note.SeasonId,
            note.Content,
            currentUser.UserId,
            authorName,
            note.CreatedAt,
            note.UpdatedAt,
            note.Version,
            CanEdit: true);
    }

    public async Task<SeasonNoteResponse> UpdateAsync(Guid id, UpdateSeasonNoteRequest request, CancellationToken ct)
    {
        if (currentUser.Role == UserRole.Worker)
        {
            throw AppException.NotFound("ghi chú");
        }

        var note = await db.SeasonNotes
            .FirstOrDefaultAsync(n => n.Id == id, ct)
            ?? throw AppException.NotFound("ghi chú");

        // BR-SEA-11, BR-USR-13: Thành viên bị thu hồi phân công không thể sửa ghi chú của mùa vụ ngoài phạm vi
        var isOwner = currentUser.Role == UserRole.Owner;
        if (!isOwner && !currentUser.AssignedSeasonIds.Contains(note.SeasonId))
        {
            throw AppException.NotFound("mùa vụ");
        }

        // BR-SEA-11: Chỉ người viết ghi chú sửa được nội dung của mình, ở mọi trạng thái mùa vụ
        if (note.CreatedById != currentUser.UserId)
        {
            throw new AppException(
                ErrorCodes.Forbidden,
                "Chỉ người viết ghi chú mới có thể sửa nội dung của mình.");
        }

        var trimmedContent = request.Content.Trim();
        note.Content = trimmedContent;

        // BR-GEN-11: Kiểm soát xung đột phiên bản
        db.Entry(note).Property(n => n.Version).OriginalValue = request.Version;

        await db.SaveChangesAsync(ct);

        var authorName = await db.Users
            .Where(u => u.Id == currentUser.UserId)
            .Select(u => u.FullName)
            .FirstOrDefaultAsync(ct) ?? "Người dùng";

        return new SeasonNoteResponse(
            note.Id,
            note.SeasonId,
            note.Content,
            note.CreatedById,
            authorName,
            note.CreatedAt,
            note.UpdatedAt,
            note.Version,
            CanEdit: true);
    }
}
