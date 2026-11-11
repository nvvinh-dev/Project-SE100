namespace HouseholdFarm.Api.Features.Seasons;

public interface ISeasonNoteService
{
    /// <summary>
    /// Lấy danh sách ghi chú kinh nghiệm của mùa vụ (FR-SEA-13).
    /// </summary>
    Task<IReadOnlyList<SeasonNoteResponse>> GetListAsync(Guid seasonId, CancellationToken ct);

    /// <summary>
    /// Thêm ghi chú kinh nghiệm cho mùa vụ (FR-SEA-13, BR-SEA-11).
    /// </summary>
    Task<SeasonNoteResponse> CreateAsync(CreateSeasonNoteRequest request, CancellationToken ct);

    /// <summary>
    /// Sửa nội dung ghi chú kinh nghiệm của chính mình (FR-SEA-13, BR-SEA-11, BR-GEN-11).
    /// </summary>
    Task<SeasonNoteResponse> UpdateAsync(Guid id, UpdateSeasonNoteRequest request, CancellationToken ct);
}
