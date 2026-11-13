namespace HouseholdFarm.Api.Features.Seasons;

public interface ISeasonAssignmentService
{
    /// <summary>
    /// Lấy danh sách phân công của một mùa vụ (FR-SEA-06, FR-SEA-12).
    /// </summary>
    Task<IReadOnlyList<SeasonAssignmentResponse>> GetListAsync(Guid seasonId, CancellationToken ct);

    /// <summary>
    /// Phân công người vào mùa vụ (FR-SEA-06, BR-USR-10, BR-USR-11).
    /// </summary>
    Task<SeasonAssignmentResponse> AssignAsync(CreateSeasonAssignmentRequest request, CancellationToken ct);

    /// <summary>
    /// Thu hồi phân công người khỏi mùa vụ (FR-SEA-06, BR-USR-11, BR-USR-12, BR-GEN-14).
    /// </summary>
    Task UnassignAsync(Guid id, UnassignSeasonAssignmentRequest request, CancellationToken ct);
}
