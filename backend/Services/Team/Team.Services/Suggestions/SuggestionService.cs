using System.Security.Claims;
using BuildingBlocks.Auth;
using Team.Repositories.AutoAssign;
using Team.Repositories.Suggestions;
using Team.Repositories.Teams;
using Team.Services.Academic;
using Team.Services.Matching;
using Team.Services.Teams;

namespace Team.Services.Suggestions;

public interface ISuggestionService
{
    /// <summary>S18 – thành viên nhóm (hoặc Admin) xem sinh viên chưa có nhóm bù chuyên ngành, kỹ năng còn thiếu (rule 16).</summary>
    Task<MemberSuggestionsResponse> SuggestMembersAsync(Guid teamId, SuggestionQuery query, ClaimsPrincipal user, CancellationToken ct);

    /// <summary>S07 – nhóm cần chuyên ngành của sinh viên; chỉ trong đợt lập nhóm (rule 18), rỗng nếu đã có nhóm.</summary>
    Task<IReadOnlyList<TeamSuggestionResponse>> SuggestTeamsAsync(SuggestionQuery query, ClaimsPrincipal user, CancellationToken ct);
}

/// <summary>Module Suggestions của Team Service (BE-39). Matching tính trên read model; Team lọc lại theo dữ liệu mới nhất của mình.</summary>
public sealed class SuggestionService(
    ISuggestionRepository suggestions, IAutoAssignRepository membership, ITeamRepository teams, IMatchingClient matching, RoundGuard rounds,
    TeamReader reader, TimeProvider clock) : ISuggestionService
{
    public async Task<MemberSuggestionsResponse> SuggestMembersAsync(Guid teamId, SuggestionQuery query, ClaimsPrincipal user, CancellationToken ct)
    {
        var graph = await teams.GetGraphAsync(teamId, ct);
        if (graph is null || graph.Team.Status == TeamStatuses.Disbanded)
        {
            throw TeamErrors.TeamNotFound(teamId);
        }
        var viewer = user.GetStudentId();
        if (!user.IsInRole(Roles.Admin) && graph.Members.All(m => m.StudentId != viewer))
        {
            throw TeamErrors.NotMember();
        }

        var team = graph.Team;
        var result = await matching.SuggestMembersAsync(team.SemesterId, team.CourseCode, team.Id, query.Limit, ct);
        // Read model của Matching đi sau Kafka: bỏ người vừa vào một nhóm khác.
        var inTeam = await membership.StudentsInTeamAsync(team.SemesterId, result.Items.Select(i => i.StudentId).ToList(), ct);
        var items = result.Items.Where(i => !inTeam.Contains(i.StudentId)).ToList();

        var catalogs = await reader.LoadCatalogsAsync(ct);
        var students = await reader.StudentsAsync(team.SemesterId, items.Select(i => i.StudentId), catalogs, ct);
        var invited = await suggestions.ListInvitedStudentIdsAsync(team.Id, clock.GetUtcNow(), ct);
        return new MemberSuggestionsResponse(
            result.MissingSpecializationIds.Select(catalogs.Specialization).ToList(),
            items.Select(i => new MemberSuggestionItem(students[i.StudentId], i.Skills, i.Score, i.Reasons, invited.Contains(i.StudentId))).ToList());
    }

    public async Task<IReadOnlyList<TeamSuggestionResponse>> SuggestTeamsAsync(SuggestionQuery query, ClaimsPrincipal user, CancellationToken ct)
    {
        var studentId = user.GetStudentId() ?? throw TeamErrors.StudentsOnly();
        var (semester, me) = await rounds.ResolveStudentAsync(studentId, ct);
        await rounds.RequireTeamFormationAsync(me.CourseCode, semester.Id, ct);
        if (await teams.IsStudentInTeamAsync(semester.Id, studentId, ct))
        {
            return [];
        }

        var result = await matching.SuggestTeamsAsync(semester.Id, me.CourseCode, studentId, query.Limit, ct);
        // Bỏ nhóm vừa đủ người / đã chốt / đã giải tán mà read model chưa kịp biết.
        var maxSize = (await rounds.TeamRulesAsync(ct)).MaxSize;
        var graphs = (await teams.ListGraphsAsync(result.Select(r => r.TeamId).ToList(), ct))
            .Where(g => g.Team.Status is TeamStatuses.Draft or TeamStatuses.Active && g.Members.Count < maxSize)
            .ToDictionary(g => g.Team.Id);
        var catalogs = await reader.LoadCatalogsAsync(ct);
        return result.Where(r => graphs.ContainsKey(r.TeamId))
            .Select(r => new TeamSuggestionResponse(TeamMapper.ToCard(graphs[r.TeamId], catalogs), r.Score, r.Reasons))
            .ToList();
    }
}
