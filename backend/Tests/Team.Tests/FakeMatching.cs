using Matching.Services.AdvisorMatching;
using Team.Services.Matching;
using Team.Services.Teams;
using ReadModelAdvisorKind = Matching.Services.ReadModel.AdvisorKind;

namespace Team.Tests;

/// <summary>Matching giả: kiểm tra cơ cấu theo rule 1, 2 như Matching thật; phương án ghép và gợi ý do test đặt sẵn.</summary>
public sealed class FakeMatching : IMatchingClient
{
    /// <summary>Quy tắc W05 test đang dùng (BE-90).</summary>
    public TeamRuleSet Rules { get; set; } = TeamRuleSet.Default;

    public AutoAssignPlan Plan { get; set; } = new([], [], []);

    public SuggestedMembers Members { get; set; } = new([], []);

    public IReadOnlyList<SuggestedTeam> Teams { get; set; } = [];

    /// <summary>Phương án ghép cố vấn do test đặt; null = dùng <see cref="AdvisorMatchingPlanner"/> thật của Matching (BE-79).</summary>
    public Func<IReadOnlyCollection<AdvisorMatchingTeam>, IReadOnlyCollection<AdvisorMatchingAdvisor>, IReadOnlyList<PlannedAdvisorMatch>>? AdvisorPlan { get; set; }

    /// <summary>true = Matching lỗi (503 matching-unavailable) khi tính phương án ghép cố vấn.</summary>
    public bool AdvisorMatchingDown { get; set; }

    /// <summary>Các lời gọi PlanAdvisorMatching đã nhận.</summary>
    public List<(string Kind, string CourseCode, IReadOnlyCollection<AdvisorMatchingTeam> Teams, IReadOnlyCollection<AdvisorMatchingAdvisor> Advisors)>
        AdvisorRequests { get; } = [];

    public Task<CompositionCheck> ValidateCompositionAsync(IReadOnlyCollection<(Guid StudentId, int SpecializationId)> members, CancellationToken ct)
    {
        var violations = new List<string>();
        if (members.Count < Rules.MinSize)
        {
            violations.Add("TOO_FEW_MEMBERS");
        }
        if (members.Count > Rules.MaxSize)
        {
            violations.Add("TOO_MANY_MEMBERS");
        }
        if (members.Select(m => m.SpecializationId).Distinct().Count() < Rules.MinSpecializations)
        {
            violations.Add("NOT_ENOUGH_SPECIALIZATIONS");
        }
        return Task.FromResult(new CompositionCheck(violations.Count == 0, violations, violations));
    }

    public Task<AutoAssignPlan> PlanAutoAssignmentAsync(Guid semesterId, string courseCode, CancellationToken ct) => Task.FromResult(Plan);

    public Task<SuggestedMembers> SuggestMembersAsync(Guid semesterId, string courseCode, Guid teamId, int limit, CancellationToken ct) =>
        Task.FromResult(Members);

    public Task<IReadOnlyList<SuggestedTeam>> SuggestTeamsAsync(Guid semesterId, string courseCode, Guid studentId, int limit, CancellationToken ct) =>
        Task.FromResult(Teams);

    public Task<IReadOnlyList<PlannedAdvisorMatch>> PlanAdvisorMatchingAsync(string advisorKind, string courseCode,
        IReadOnlyCollection<AdvisorMatchingTeam> teams, IReadOnlyCollection<AdvisorMatchingAdvisor> advisors, CancellationToken ct)
    {
        lock (AdvisorRequests)
        {
            AdvisorRequests.Add((advisorKind, courseCode, teams, advisors));
        }
        if (AdvisorMatchingDown)
        {
            throw MatchingErrors.Unavailable();
        }
        if (AdvisorPlan is not null)
        {
            return Task.FromResult(AdvisorPlan(teams, advisors));
        }

        var planned = new AdvisorMatchingPlanner().Plan(
            advisorKind == "MENTOR" ? ReadModelAdvisorKind.Mentor : ReadModelAdvisorKind.Lecturer,
            teams.Select(t => new TeamToMatch(t.TeamId, t.DomainIds, t.PreferredAdvisorIds, t.InterestedAdvisorIds, t.PreviousInstructorId)),
            advisors.Select(a => new AdvisorOption(a.AdvisorId, a.DomainIds, a.CurrentTeamCount, a.MaxTeams, a.PreferredTeams)));
        return Task.FromResult<IReadOnlyList<PlannedAdvisorMatch>>(planned.Select(m => new PlannedAdvisorMatch(
            m.TeamId, m.AdvisorId, m.AdvisorId is null ? null : ReasonCode(m.Reason), m.PreferenceRank > 0 ? m.PreferenceRank : null)).ToList());
    }

    /// <summary>Như MatchingClient: enum MatchReason → mã lý do của team.yaml.</summary>
    private static string ReasonCode(AdvisorMatchReason reason) => reason switch
    {
        AdvisorMatchReason.Mutual => "MUTUAL",
        AdvisorMatchReason.Preference => "PREFERENCE",
        AdvisorMatchReason.LecturerInterest => "LECTURER_INTEREST",
        AdvisorMatchReason.PreviousInstructor => "PREVIOUS_INSTRUCTOR",
        AdvisorMatchReason.Domain => "DOMAIN",
        AdvisorMatchReason.MentorInterest => "MENTOR_INTEREST",
        _ => throw new ArgumentOutOfRangeException(nameof(reason)),
    };
}
