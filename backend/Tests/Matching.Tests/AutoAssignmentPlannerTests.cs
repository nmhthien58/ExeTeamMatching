using Matching.Services.AutoAssignment;
using Matching.Services.Composition;
using Matching.Services.ReadModel;

namespace Matching.Tests;

/// <summary>BE-38 – PlanAutoAssignment (rule 14).</summary>
public sealed class AutoAssignmentPlannerTests
{
    private static readonly CancellationToken Ct = CancellationToken.None;

    private static Task<AutoAssignmentPlan> PlanAsync(Scenario s) =>
        new AutoAssignmentPlanner(s.Model).PlanAsync(s.SemesterId, Courses.Exe101, CompositionRules.Default, Ct);

    [Fact]
    public async Task Fills_needs_review_team_first_then_builds_new_team_and_places_leftover()
    {
        var s = new Scenario();
        var review = await s.TeamAsync("Cần xem lại", await s.StudentAsync(Scenario.SE), await s.StudentsAsync(2, Scenario.SE),
            status: TeamStatuses.NeedsReview);
        await s.StudentsAsync(4, Scenario.SE);
        await s.StudentsAsync(2, Scenario.GD);
        await s.StudentsAsync(2, Scenario.DM);

        var plan = await PlanAsync(s);

        var fill = Assert.Single(plan.Fills);
        Assert.Equal(review, fill.TeamId);
        var added = await s.Model.GetStudentsByIdsAsync(fill.StudentIds, Ct);
        Assert.All(added, a => Assert.NotEqual(Scenario.SE, a.SpecializationId)); // bù chuyên ngành thứ hai
        var newTeam = Assert.Single(plan.NewTeams);
        Assert.Equal(6, newTeam.Members.Count);
        Assert.True(CompositionValidator.Validate(newTeam.Members, CompositionRules.Default).IsValid);
        Assert.Empty(plan.UnassignedStudentIds);
        Assert.Equal(8, fill.StudentIds.Count + newTeam.Members.Count);
    }

    [Fact]
    public async Task Ten_students_become_two_teams_of_five_each_with_two_specializations()
    {
        var s = new Scenario();
        await s.StudentsAsync(6, Scenario.SE);
        await s.StudentsAsync(4, Scenario.GD);

        var plan = await PlanAsync(s);

        Assert.Equal([5, 5], plan.NewTeams.Select(t => t.Members.Count));
        Assert.All(plan.NewTeams, t => Assert.True(CompositionValidator.Validate(t.Members, CompositionRules.Default).IsValid));
        Assert.Equal(["Auto-EXE101-01", "Auto-EXE101-02"], plan.NewTeams.Select(t => t.SuggestedName));
    }

    [Fact]
    public async Task Too_few_students_and_no_room_are_left_for_admin()
    {
        var s = new Scenario();
        var students = await s.StudentsAsync(3, Scenario.SE);
        await s.TeamAsync("Đã chốt", await s.StudentAsync(Scenario.GD), await s.StudentsAsync(3, Scenario.SE), status: TeamStatuses.Finalized);

        var plan = await PlanAsync(s);

        Assert.Empty(plan.Fills);
        Assert.Empty(plan.NewTeams);
        Assert.Equal(students.Order(), plan.UnassignedStudentIds.Order());
    }

    [Fact]
    public async Task Needs_review_team_that_cannot_get_a_second_specialization_is_not_filled_with_same_specialization()
    {
        var s = new Scenario();
        await s.TeamAsync("Toàn SE", await s.StudentAsync(Scenario.SE), await s.StudentsAsync(3, Scenario.SE), status: TeamStatuses.NeedsReview);
        await s.StudentsAsync(2, Scenario.SE);

        var plan = await PlanAsync(s);

        Assert.Empty(plan.Fills);
        Assert.Equal(2, plan.UnassignedStudentIds.Count);
    }

    [Fact]
    public async Task Leader_is_best_at_management_and_names_continue_after_existing_auto_teams()
    {
        var s = new Scenario();
        await s.TeamAsync("Auto-EXE101-03", await s.StudentAsync(Scenario.SE), await s.StudentsAsync(4, Scenario.GD), status: TeamStatuses.Finalized);
        await s.StudentsAsync(3, Scenario.SE);
        var manager = await s.StudentAsync(Scenario.GD, skills: [(Skill.Management, 5)]);

        var plan = await PlanAsync(s);

        var team = Assert.Single(plan.NewTeams);
        Assert.Equal(manager, team.LeaderStudentId);
        Assert.Equal("Auto-EXE101-04", team.SuggestedName);
    }

    [Fact]
    public async Task Same_data_gives_same_plan()
    {
        var s = new Scenario();
        await s.StudentsAsync(7, Scenario.SE);
        await s.StudentsAsync(5, Scenario.DM);

        var first = await PlanAsync(s);
        var second = await PlanAsync(s);

        Assert.Equal(
            first.NewTeams.Select(t => string.Join(',', t.Members.Select(m => m.StudentId))),
            second.NewTeams.Select(t => string.Join(',', t.Members.Select(m => m.StudentId))));
    }
}
