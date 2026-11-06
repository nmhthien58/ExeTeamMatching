using System.Net;
using System.Net.Http.Headers;
using BuildingBlocks.Auth;
using Collab.Api.Controllers.Workspaces;
using Collab.Repositories.Workspaces;
using Microsoft.EntityFrameworkCore;

namespace Collab.Tests;

/// <summary>GET /api/workspaces/{id} trả <c>people</c> theo schema WorkspaceOverview (gợi ý @ ở C03).</summary>
public sealed class WorkspacesApiTests : IDisposable
{
    private readonly CollabApiFactory _factory = new();

    public void Dispose() => _factory.Dispose();

    [Fact]
    public async Task Overview_lists_current_people_students_first_without_those_who_left()
    {
        var mentor = TestPerson.Mentor("Mentor An");
        var instructor = TestPerson.Instructor("Cô Bình");
        var member = TestPerson.Member("Sinh viên Cúc");
        var leader = TestPerson.Leader("Sinh viên Dũng");
        var left = TestPerson.Member("Sinh viên Đã rời");
        var workspaceId = await _factory.SeedWorkspaceAsync("Nhóm A", "EXE101", null, mentor, instructor, member, leader, left);
        await _factory.WithDbAsync(async db =>
            (await db.Set<WorkspaceMember>().SingleAsync(m => m.PersonId == left.Id)).LeftAt = DateTimeOffset.UtcNow);

        using var client = _factory.ClientFor(member);
        var response = await client.GetAsync($"/api/workspaces/{workspaceId}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var overview = await CollabApiFactory.ReadAsync<WorkspaceOverviewResponse>(response);
        Assert.Equal(WorkspaceRoles.Member, overview.MyRole);
        Assert.Equal([leader.Id, member.Id, instructor.Id, mentor.Id], overview.People.Select(p => p.PersonId));
        var person = overview.People[2];
        Assert.Equal(PersonTypes.Lecturer, person.PersonType);
        Assert.Equal("Cô Bình", person.DisplayName);
        Assert.Equal(WorkspaceRoles.Instructor, person.Role);
    }

    [Fact]
    public async Task Outsider_cannot_read_people()
    {
        var workspaceId = await _factory.SeedWorkspaceAsync("Nhóm B", "EXE101", null, TestPerson.Leader());

        using var client = _factory.ClientFor(TestPerson.Member("Người ngoài"));
        var response = await client.GetAsync($"/api/workspaces/{workspaceId}");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    /// <summary>
    /// BE-64: giảng viên kiêm mentor có một token với cả lecturer_id và mentor_id. Policy workspace nhận ra họ là giảng viên
    /// hướng dẫn ở nhóm này và mentor ở nhóm khác, không cần đổi vai trò.
    /// </summary>
    [Fact]
    public async Task Lecturer_who_is_mentor_reaches_both_workspaces_with_the_right_role()
    {
        var instructor = TestPerson.Instructor("Cô Hà");
        var mentor = TestPerson.Mentor("Cô Hà");
        var instructed = await _factory.SeedWorkspaceAsync("Nhóm hướng dẫn", "EXE201", null, TestPerson.Leader(), instructor);
        var mentored = await _factory.SeedWorkspaceAsync("Nhóm mentor", "EXE201", null, TestPerson.Leader(), mentor);
        var other = await _factory.SeedWorkspaceAsync("Nhóm khác", "EXE201", null, TestPerson.Leader());

        using var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", CollabApiFactory.Token(
            "Cô Hà", [Roles.Lecturer, Roles.Mentor], (ClaimNames.LecturerId, instructor.Id), (ClaimNames.MentorId, mentor.Id)));

        var mine = await CollabApiFactory.ReadAsync<List<WorkspaceSummaryResponse>>(await client.GetAsync("/api/workspaces/me"));
        Assert.Equal([(instructed, WorkspaceRoles.Instructor), (mentored, WorkspaceRoles.Mentor)],
            mine.OrderBy(w => w.TeamName).Select(w => (w.Id, w.MyRole)).OrderBy(x => x.MyRole == WorkspaceRoles.Mentor));
        Assert.Equal(WorkspaceRoles.Mentor,
            (await CollabApiFactory.ReadAsync<WorkspaceOverviewResponse>(await client.GetAsync($"/api/workspaces/{mentored}"))).MyRole);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync($"/api/workspaces/{other}")).StatusCode);
    }
}
