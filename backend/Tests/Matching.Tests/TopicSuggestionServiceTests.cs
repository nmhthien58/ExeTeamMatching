using Matching.Services.Events;
using Matching.Services.ReadModel;
using Matching.Services.TopicSuggestions;

namespace Matching.Tests;

/// <summary>BE-71 – SuggestTopicsForLecturer (rule 19, 26).</summary>
public sealed class TopicSuggestionServiceTests
{
    private static readonly CancellationToken Ct = CancellationToken.None;

    [Fact]
    public async Task Only_teams_without_instructor_with_overlapping_domains_ranked_by_overlap_then_interest()
    {
        var s = new Scenario();
        var me = Guid.NewGuid();
        await s.LecturerAsync(me, 4, 10);
        var oneDomainCrowded = await s.TeamAsync("A", await s.StudentAsync(Scenario.SE), domainIds: [4]);
        var oneDomainQuiet = await s.TeamAsync("B", await s.StudentAsync(Scenario.SE), domainIds: [10, 12]);
        var twoDomains = await s.TeamAsync("C", await s.StudentAsync(Scenario.SE), domainIds: [4, 10]);
        await s.TeamAsync("Không trùng", await s.StudentAsync(Scenario.SE), domainIds: [1]);
        var assigned = await s.TeamAsync("Đã có GV", await s.StudentAsync(Scenario.SE), domainIds: [4]);
        await s.Projector.HandleAsync(new LecturerAssigned(assigned, Guid.NewGuid()), Ct);
        await s.Projector.HandleAsync(new TopicInterestOpened(oneDomainCrowded, "LECTURER", Guid.NewGuid()), Ct);
        await s.Projector.HandleAsync(new TopicInterestOpened(oneDomainCrowded, "LECTURER", me), Ct); // của tôi – không tính

        var page = await new TopicSuggestionService(s.Model).SuggestAsync(
            new TopicQuery(s.SemesterId, null, AdvisorKind.Lecturer, me, 20, 0), Ct);

        Assert.Equal(3, page.Total);
        Assert.Equal([twoDomains, oneDomainQuiet, oneDomainCrowded], page.Items.Select(x => x.Team.Id));
        Assert.Equal(1, page.Items[2].InterestedOthersCount);
        Assert.Equal([10], page.Items[1].MatchedDomainIds);
    }

    [Fact]
    public async Task Exe101_instructor_of_the_project_sees_it_first_even_without_domain_overlap()
    {
        var s = new Scenario();
        var me = Guid.NewGuid();
        await s.LecturerAsync(me, 4);
        var exe101 = await s.TeamAsync("T1", await s.StudentAsync(Scenario.SE), domainIds: [15]);
        await s.Projector.HandleAsync(new LecturerAssigned(exe101, me), Ct);
        var projectId = (await s.Model.GetTeamAsync(exe101, Ct))!.ProjectId!.Value;
        var continued = await s.TeamAsync("T2", await s.StudentAsync(Scenario.SE, Courses.Exe201), course: Courses.Exe201);
        await s.Projector.HandleAsync(new TeamLinkedToProject(continued, projectId), Ct);
        var domainMatch = await s.TeamAsync("T3", await s.StudentAsync(Scenario.SE, Courses.Exe201), course: Courses.Exe201, domainIds: [4]);

        var page = await new TopicSuggestionService(s.Model).SuggestAsync(
            new TopicQuery(s.SemesterId, Courses.Exe201, AdvisorKind.Lecturer, me, 20, 0), Ct);

        Assert.Equal([continued, domainMatch], page.Items.Select(x => x.Team.Id));
        Assert.True(page.Items[0].WasAdvisorExe101Team);
        Assert.Contains("Bạn đã hướng dẫn đề tài này ở EXE101", page.Items[0].Reasons);
    }

    [Fact]
    public async Task Mentor_kind_skips_teams_that_already_have_a_mentor()
    {
        var s = new Scenario();
        var mentor = Guid.NewGuid();
        await s.Projector.HandleAsync(new MentorRegistered(mentor, [9], true), Ct);
        var withLecturer = await s.TeamAsync("A", await s.StudentAsync(Scenario.SE), domainIds: [9]);
        await s.Projector.HandleAsync(new LecturerAssigned(withLecturer, Guid.NewGuid()), Ct);
        var withMentor = await s.TeamAsync("B", await s.StudentAsync(Scenario.SE), domainIds: [9]);
        await s.Projector.HandleAsync(new MentorAssigned(withMentor, Guid.NewGuid()), Ct);

        var page = await new TopicSuggestionService(s.Model).SuggestAsync(
            new TopicQuery(s.SemesterId, null, AdvisorKind.Mentor, mentor, 20, 0), Ct);

        Assert.Equal([withLecturer], page.Items.Select(x => x.Team.Id));
    }

    [Fact]
    public async Task Paging_returns_total_and_slice()
    {
        var s = new Scenario();
        var me = Guid.NewGuid();
        await s.LecturerAsync(me, 4);
        for (var i = 0; i < 5; i++)
        {
            await s.TeamAsync($"N{i}", await s.StudentAsync(Scenario.SE), domainIds: [4]);
        }

        var page = await new TopicSuggestionService(s.Model).SuggestAsync(
            new TopicQuery(s.SemesterId, null, AdvisorKind.Lecturer, me, 2, 4), Ct);

        Assert.Equal(5, page.Total);
        Assert.Single(page.Items);
    }

    [Fact]
    public async Task Unknown_advisor_gets_empty_page()
    {
        var page = await new TopicSuggestionService(new Scenario().Model).SuggestAsync(
            new TopicQuery(Guid.NewGuid(), null, AdvisorKind.Lecturer, Guid.NewGuid(), 20, 0), Ct);

        Assert.Equal(0, page.Total);
    }
}
