using Team.Services.Teams;

namespace Team.Services.TopicInterests;

public static class AdvisorKinds
{
    public const string Lecturer = "LECTURER";
    public const string Mentor = "MENTOR";
}

public static class TopicInterestStatuses
{
    public const string Open = "OPEN";
    public const string Withdrawn = "WITHDRAWN";
    public const string Closed = "CLOSED";
}

public static class ClosedReasons
{
    public const string ThisAdvisorMatched = "THIS_ADVISOR_MATCHED";
    public const string OtherAdvisorMatched = "OTHER_ADVISOR_MATCHED";
    public const string TeamDisbanded = "TEAM_DISBANDED";
}

public sealed record OpenTopicInterestInput(string AdvisorKind);

public sealed record TopicInterestResponse(
    Guid Id,
    Guid TeamId,
    string AdvisorKind,
    Guid AdvisorId,
    string AdvisorName,
    bool WasPreviousInstructor,
    string Status,
    string? ClosedReason,
    DateTimeOffset CreatedAt);

public sealed record TopicRecommendationResponse(
    TeamCardResponse Team,
    IReadOnlyList<CatalogRef> MatchedDomains,
    int OtherInterestedCount,
    int? MyPreferenceRank,
    TopicInterestResponse? MyInterest,
    bool WasMyEXE101Team,
    bool HasAdvisor);

public sealed record TopicRecommendationsResponse(
    int OpenInterestCount,
    int InterestLimit,
    int? RemainingCapacity,
    bool InterestOpen,
    DateTimeOffset? MatchingAt,
    IReadOnlyList<TopicRecommendationResponse> Items);
