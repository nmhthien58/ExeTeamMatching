using Team.Services.Matching;
using Team.Services.Teams;

namespace Team.Tests;

/// <summary>Matching giả: kiểm tra cơ cấu theo rule 1, 2 như Matching thật; phương án ghép và gợi ý do test đặt sẵn.</summary>
public sealed class FakeMatching : IMatchingClient
{
    public AutoAssignPlan Plan { get; set; } = new([], [], []);

    public SuggestedMembers Members { get; set; } = new([], []);

    public IReadOnlyList<SuggestedTeam> Teams { get; set; } = [];

    public Task<CompositionCheck> ValidateCompositionAsync(IReadOnlyCollection<(Guid StudentId, int SpecializationId)> members, CancellationToken ct)
    {
        var violations = new List<string>();
        if (members.Count < TeamRules.MinSize)
        {
            violations.Add("TOO_FEW_MEMBERS");
        }
        if (members.Count > TeamRules.MaxSize)
        {
            violations.Add("TOO_MANY_MEMBERS");
        }
        if (members.Select(m => m.SpecializationId).Distinct().Count() < TeamRules.MinSpecializations)
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
}
