using Team.Services.Matching;
using Team.Services.Teams;

namespace Team.Tests;

/// <summary>Matching giả: kiểm tra cơ cấu theo rule 1, 2 như Matching thật; phương án ghép tự động do test đặt trong <see cref="Plan"/>.</summary>
public sealed class FakeMatching : IMatchingClient
{
    public AutoAssignPlan Plan { get; set; } = new([], [], []);

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
}
