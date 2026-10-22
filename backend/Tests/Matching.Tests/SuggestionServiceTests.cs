using BuildingBlocks.Errors;
using Matching.Services.ReadModel;
using Matching.Services.Suggestions;

namespace Matching.Tests;

/// <summary>BE-36, BE-71 – SuggestMembers / SuggestTeams: chuyên ngành còn thiếu và kỹ năng yếu của nhóm.</summary>
public sealed class SuggestionServiceTests
{
    private static readonly CancellationToken Ct = CancellationToken.None;

    [Fact]
    public async Task Members_only_students_without_team_in_same_semester_and_course()
    {
        var s = new Scenario();
        var leader = await s.StudentAsync(Scenario.SE);
        var team = await s.TeamAsync("A", leader);
        var free = await s.StudentAsync(Scenario.SE);
        var otherCourse = await s.StudentAsync(Scenario.GD, Courses.Exe201);
        var inOtherTeam = await s.StudentAsync(Scenario.GD);
        await s.TeamAsync("B", inOtherTeam);

        var result = await new SuggestionService(s.Model).SuggestMembersAsync(team, 10, Ct);

        Assert.Equal([free], result.Suggestions.Select(x => x.Student.Id));
        Assert.DoesNotContain(otherCourse, result.Suggestions.Select(x => x.Student.Id));
    }

    [Fact]
    public async Task Missing_specialization_ranks_first_and_is_listed()
    {
        var s = new Scenario();
        var team = await s.TeamAsync("A", await s.StudentAsync(Scenario.SE), [await s.StudentAsync(Scenario.SE)]);
        var sameSpec = await s.StudentAsync(Scenario.SE);
        var designer = await s.StudentAsync(Scenario.GD);

        var result = await new SuggestionService(s.Model).SuggestMembersAsync(team, 10, Ct);

        Assert.Equal(designer, result.Suggestions[0].Student.Id);
        Assert.Contains("Bù chuyên ngành nhóm đang thiếu", result.Suggestions[0].Reasons);
        Assert.Equal(sameSpec, result.Suggestions[1].Student.Id);
        Assert.Equal([Scenario.GD], result.MissingSpecializationIds);
    }

    [Fact]
    public async Task Strong_student_in_weak_skill_ranks_higher_with_reason()
    {
        var s = new Scenario();
        var leader = await s.StudentAsync(Scenario.SE, skills:
            [(Skill.Technology, 5), (Skill.UiUxDesign, 1), (Skill.Management, 4), (Skill.Marketing, 4), (Skill.CommunicationPlanning, 3)]);
        var team = await s.TeamAsync("A", leader, [await s.StudentAsync(Scenario.GD, skills: [(Skill.Technology, 3), (Skill.UiUxDesign, 2)])]);
        var coder = await s.StudentAsync(Scenario.SE, skills: [(Skill.Technology, 5), (Skill.UiUxDesign, 1)]);
        var designer = await s.StudentAsync(Scenario.SE, skills: [(Skill.Technology, 2), (Skill.UiUxDesign, 5)]);

        var result = await new SuggestionService(s.Model).SuggestMembersAsync(team, 10, Ct);

        Assert.Equal(Skill.UiUxDesign, result.WeakSkills[0]);
        Assert.Equal(designer, result.Suggestions[0].Student.Id);
        Assert.Contains(result.Suggestions[0].Reasons, r => r.StartsWith("Mạnh Thiết kế UI/UX (5/5)"));
        Assert.True(result.Suggestions[0].Score > result.Suggestions.Single(x => x.Student.Id == coder).Score);
        Assert.Empty(result.MissingSpecializationIds);
    }

    [Fact]
    public async Task Full_team_gets_no_member_suggestions()
    {
        var s = new Scenario();
        var team = await s.TeamAsync("A", await s.StudentAsync(Scenario.SE), await s.StudentsAsync(5, Scenario.GD));
        await s.StudentAsync(Scenario.DM);

        var result = await new SuggestionService(s.Model).SuggestMembersAsync(team, 10, Ct);

        Assert.Empty(result.Suggestions);
    }

    [Fact]
    public async Task Unknown_team_is_not_found()
    {
        await Assert.ThrowsAsync<NotFoundException>(() =>
            new SuggestionService(new Scenario().Model).SuggestMembersAsync(Guid.NewGuid(), 10, Ct));
    }

    [Fact]
    public async Task Teams_needing_my_specialization_rank_first_and_closed_teams_are_hidden()
    {
        var s = new Scenario();
        var me = await s.StudentAsync(Scenario.GD);
        var allSe = await s.TeamAsync("Toàn SE", await s.StudentAsync(Scenario.SE), [await s.StudentAsync(Scenario.SE)]);
        var mixed = await s.TeamAsync("Đã có GD", await s.StudentAsync(Scenario.SE), [await s.StudentAsync(Scenario.GD)]);
        await s.TeamAsync("Đã chốt", await s.StudentAsync(Scenario.SE), status: TeamStatuses.Finalized);
        await s.TeamAsync("Đủ 6", await s.StudentAsync(Scenario.SE), await s.StudentsAsync(5, Scenario.DM));

        var result = await new SuggestionService(s.Model).SuggestTeamsAsync(me, 10, Ct);

        Assert.Equal([allSe, mixed], result.Select(x => x.Team.Id));
        Assert.Equal([Scenario.GD], result[0].MissingSpecializationIds);
        Assert.Contains("Nhóm đang cần chuyên ngành của bạn", result[0].Reasons);
    }

    [Fact]
    public async Task Student_with_team_gets_no_team_suggestions()
    {
        var s = new Scenario();
        var me = await s.StudentAsync(Scenario.SE);
        await s.TeamAsync("Của tôi", me);
        await s.TeamAsync("Khác", await s.StudentAsync(Scenario.GD));

        Assert.Empty(await new SuggestionService(s.Model).SuggestTeamsAsync(me, 10, Ct));
    }
}
