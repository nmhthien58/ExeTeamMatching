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
}
