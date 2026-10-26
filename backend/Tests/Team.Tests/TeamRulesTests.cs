using Team.Services.Teams;

namespace Team.Tests;

public sealed class TeamRulesTests
{
    private static NextActionInput Team(int members = 4, int specializations = 2, bool leader = true, string phase = Phases.TeamFormation,
        string status = TeamStatuses.Active, bool lecturer = false, bool preferences = false, bool unsubmitted = false, bool pendingRequest = false) =>
        new(true, leader, phase, status, members, specializations, lecturer, preferences, unsubmitted, pendingRequest);

    [Theory]
    [InlineData(Phases.TeamFormation, "CREATE_OR_JOIN")]
    [InlineData(Phases.SemesterRunning, "NONE")]
    public void Student_without_team(string phase, string expected) =>
        Assert.Equal(expected, TeamRules.NextAction(new NextActionInput(false, false, phase, null, 0, 0, false, false, false, false)));

    [Fact]
    public void Forming_team_is_asked_for_members_then_specializations_then_lecturer()
    {
        Assert.Equal("INVITE_MEMBERS", TeamRules.NextAction(Team(members: 3)));
        Assert.Equal("ADD_SPECIALIZATION", TeamRules.NextAction(Team(specializations: 1)));
        Assert.Equal("CHOOSE_LECTURER", TeamRules.NextAction(Team()));
        Assert.Equal("NONE", TeamRules.NextAction(Team(preferences: true)));
        Assert.Equal("NONE", TeamRules.NextAction(Team(leader: false)));
    }

    [Fact]
    public void Change_requests_take_priority()
    {
        Assert.Equal("WAIT_REVIEW", TeamRules.NextAction(Team(members: 2, pendingRequest: true)));
        Assert.Equal("SUBMIT_CHANGE_REQUEST", TeamRules.NextAction(Team(unsubmitted: true)));
        Assert.Equal("INVITE_MEMBERS", TeamRules.NextAction(Team(members: 2, leader: false, unsubmitted: true)));
    }

    [Theory]
    [InlineData(TeamStatuses.Finalized, "OPEN_WORKSPACE")]
    [InlineData(TeamStatuses.NeedsReview, "NONE")]
    public void After_formation_finalized_team_opens_workspace(string status, string expected) =>
        Assert.Equal(expected, TeamRules.NextAction(Team(phase: Phases.SemesterRunning, status: status)));

    [Theory]
    [InlineData(4, 2, true)]
    [InlineData(6, 3, true)]
    [InlineData(3, 2, false)]
    [InlineData(7, 2, false)]
    [InlineData(5, 1, false)]
    public void Team_is_valid_with_4_to_6_members_and_2_specializations(int members, int specializations, bool valid) =>
        Assert.Equal(valid, TeamRules.IsValid(members, specializations));
}
