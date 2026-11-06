using FluentValidation;
using Team.Services.Teams;

namespace Team.Services.Suggestions;

// Schema của team.yaml (tag Suggestions) – Team gọi Matching.SuggestMembers / SuggestTeams qua gRPC.

public sealed record SuggestionQuery
{
    public int Limit { get; init; } = 10;
}

public sealed record MemberSuggestionItem(StudentRef Student, IReadOnlyList<string> Skills, double Score, IReadOnlyList<string> Reasons, bool AlreadyInvited);

/// <summary>Trả về của <c>GET /api/teams/{teamId}/suggested-members</c> (S18).</summary>
public sealed record MemberSuggestionsResponse(IReadOnlyList<CatalogRef> MissingSpecializations, IReadOnlyList<MemberSuggestionItem> Items);

/// <summary>Một phần tử của <c>GET /api/teams/suggested-for-me</c> (S07).</summary>
public sealed record TeamSuggestionResponse(TeamCardResponse Team, double Score, IReadOnlyList<string> Reasons);

public sealed class SuggestionQueryValidator : AbstractValidator<SuggestionQuery>
{
    public SuggestionQueryValidator() => RuleFor(x => x.Limit).InclusiveBetween(1, 50);
}
