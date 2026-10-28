using Team.Repositories.AuditLog;
using Team.Repositories.Members;
using Team.Repositories.Teams;
using Team.Services.Matching;
using Team.Services.Teams;

namespace Team.Services.Finalization;

/// <summary>
/// Chốt nhóm / đưa nhóm vào "Cần xem lại" (rule 13, 14), dùng chung cho đóng đợt, Admin và ghép tự động.
/// Điều kiện hợp lệ (rule 1, 2) hỏi Matching.ValidateComposition; trigger <c>check_team_composition</c> của team_db chặn lần cuối lúc COMMIT.
/// </summary>
public sealed class TeamFinalizer(IMatchingClient matching, ITeamRepository teams, AuditLogWriter audit, TimeProvider clock)
{
    public const string BySystem = "SYSTEM";
    public const string ByAdmin = "ADMIN";

    public Task<CompositionCheck> CheckAsync(IEnumerable<TeamMember> members, CancellationToken ct) =>
        matching.ValidateCompositionAsync(members.Select(m => (m.StudentId, m.SpecializationId)).ToList(), ct);

    public void Finalize(TeamEntity team, IEnumerable<TeamMember> members, string finalizedBy, AuditActor actor, string? reason = null)
    {
        var before = team.Status;
        (team.Status, team.FinalizedAt) = (TeamStatuses.Finalized, clock.GetUtcNow());
        audit.Add(team.Id, "Team", team.Id, "TEAM_FINALIZED", actor, new { status = before }, new { status = team.Status, finalizedBy }, reason);
        teams.AddEvent(FinalizationEventTypes.TeamFinalized, team.Id,
            new TeamFinalized(team.Id, team.SemesterId, members.Select(m => m.StudentId).ToList(), finalizedBy));
    }

    public void MarkNeedsReview(TeamEntity team, IReadOnlyList<string> violations, AuditActor actor, string? reason = null)
    {
        var before = team.Status;
        (team.Status, team.FinalizedAt) = (TeamStatuses.NeedsReview, null);
        audit.Add(team.Id, "Team", team.Id, "TEAM_NEEDS_REVIEW", actor, new { status = before }, new { status = team.Status, violations }, reason);
        teams.AddEvent(FinalizationEventTypes.TeamNeedsReview, team.Id, new TeamNeedsReview(team.Id, team.SemesterId, violations));
    }

    /// <summary>
    /// Trạng thái sau khi Admin hoặc ghép tự động đổi thành viên: Nháp / Hoạt động theo sĩ số (rule 15);
    /// nhóm đã chốt mà hết hợp lệ thì về Cần xem lại; Cần xem lại giữ nguyên tới khi chốt.
    /// </summary>
    public async Task AfterMembersChangedAsync(TeamEntity team, IReadOnlyCollection<TeamMember> members, AuditActor actor, CancellationToken ct)
    {
        if (team.Status is TeamStatuses.Draft or TeamStatuses.Active)
        {
            team.Status = TeamRules.StatusAfterMemberChange(team.Status, members.Count);
        }
        else if (team.Status == TeamStatuses.Finalized && await CheckAsync(members, ct) is { IsValid: false } check)
        {
            MarkNeedsReview(team, check.Violations, actor);
        }
    }
}
