using BuildingBlocks.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Team.Services.Suggestions;

namespace Team.Api.Controllers.Suggestions;

/// <summary>Gợi ý qua Matching: S07, S18 (BE-39) – team.yaml tag Suggestions.</summary>
[ApiController]
[Authorize]
[Route("api/teams")]
public sealed class SuggestionsController(ISuggestionService suggestions) : ControllerBase
{
    /// <summary>Thành viên nhóm hoặc Admin.</summary>
    [HttpGet("{teamId:guid}/suggested-members")]
    public async Task<ActionResult<MemberSuggestionsResponse>> SuggestedMembers(Guid teamId, [FromQuery] SuggestionQuery query, CancellationToken ct) =>
        Ok(await suggestions.SuggestMembersAsync(teamId, query, User, ct));

    [HttpGet("suggested-for-me")]
    [Authorize(Policy = Policies.Student)]
    public async Task<ActionResult<IReadOnlyList<TeamSuggestionResponse>>> SuggestedForMe([FromQuery] SuggestionQuery query, CancellationToken ct) =>
        Ok(await suggestions.SuggestTeamsAsync(query, User, ct));
}
