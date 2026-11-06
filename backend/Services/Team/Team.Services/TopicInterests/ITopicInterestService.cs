using System.Security.Claims;

namespace Team.Services.TopicInterests;

public interface ITopicInterestService
{
    Task<TopicRecommendationsResponse> GetRecommendationsAsync(
        string advisorKind,
        string? courseCode,
        int page,
        int pageSize,
        ClaimsPrincipal user,
        CancellationToken ct);

    Task<IReadOnlyList<TopicInterestResponse>> GetMyInterestsAsync(
        string advisorKind,
        ClaimsPrincipal user,
        CancellationToken ct);

    Task<IReadOnlyList<TopicInterestResponse>> GetTeamInterestsAsync(
        Guid teamId,
        string? advisorKind,
        ClaimsPrincipal user,
        CancellationToken ct);

    Task<TopicInterestResponse> OpenInterestAsync(
        Guid teamId,
        OpenTopicInterestInput input,
        ClaimsPrincipal user,
        CancellationToken ct);

    Task WithdrawInterestAsync(
        Guid id,
        ClaimsPrincipal user,
        CancellationToken ct);

    Task CloseInterestsOnAssignmentAsync(
        Guid teamId,
        string advisorKind,
        Guid matchedAdvisorId,
        CancellationToken ct);

    /// <summary>
    /// Nhóm bị giải tán: đóng mọi quan tâm OPEN (giảng viên và mentor) với reason <c>TEAM_DISBANDED</c> và thêm
    /// <c>TopicInterestClosed</c> vào Outbox. Không gọi SaveChanges – người gọi lưu cùng lần đổi trạng thái nhóm.
    /// </summary>
    Task CloseInterestsOnTeamDisbandedAsync(Guid teamId, CancellationToken ct);
}
