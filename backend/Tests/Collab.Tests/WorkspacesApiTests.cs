using System.Net;
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
}
