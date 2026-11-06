using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using BuildingBlocks.Auth;
using BuildingBlocks.Outbox;
using Microsoft.EntityFrameworkCore;
using Team.Repositories.Projects;
using Team.Repositories.Teams;
using Team.Services.Projects;

namespace Team.Tests;

/// <summary>Chạy với EXE_INTEGRATION=1 và team_db thật sau khi khởi động PostgreSQL.</summary>
public sealed class ProjectsApiTests : IAsyncLifetime
{
    private static bool Enabled => Environment.GetEnvironmentVariable("EXE_INTEGRATION") == "1";
    private readonly TeamApiFactory _api = new();
    private readonly Guid _sourceId = Guid.NewGuid();
    private readonly Guid _targetId = Guid.NewGuid();
    private readonly Guid _projectId = Guid.NewGuid();

    [Fact]
    public async Task Admin_links_team_once_and_writes_typed_outbox_event()
    {
        if (!Enabled) return;

        var sourceSemesterId = Guid.NewGuid();
        await _api.QueryDbAsync(async db =>
        {
            db.Add(new Project { Id = _projectId, Name = "Đề tài xuyên kỳ", OriginSemesterId = sourceSemesterId,
                OriginCourseCode = "EXE101" });
            db.Add(new ProjectVersion { ProjectId = _projectId, Version = 1, Name = "Đề tài xuyên kỳ",
                ChangeType = "CREATED", SemesterId = sourceSemesterId, CourseCode = "EXE101" });
            db.Add(new TeamEntity { Id = _sourceId, SemesterId = sourceSemesterId, CourseCode = "EXE101",
                Name = "Nhóm EXE101 BE76", ProjectId = _projectId });
            db.Add(new TeamEntity { Id = _targetId, SemesterId = _api.Academic.Semester.Id, CourseCode = "EXE201",
                Name = "Nhóm EXE201 BE76", IsInherited = true });
            return await db.SaveChangesAsync();
        });

        var client = _api.ClientFor(null, Roles.Admin);
        var url = $"/api/projects/{_projectId}/link-team";
        var response = await client.PostAsJsonAsync(url, new LinkTeamInput(_targetId));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var detail = await response.Content.ReadFromJsonAsync<ProjectDetailResponse>();
        Assert.Equal(2, detail!.Teams.Count);
        Assert.Equal(_projectId, await _api.QueryDbAsync(db => db.Set<TeamEntity>()
            .Where(t => t.Id == _targetId).Select(t => t.ProjectId).SingleAsync()));

        var repeated = await client.PostAsJsonAsync(url, new LinkTeamInput(_targetId));
        Assert.Equal(HttpStatusCode.Conflict, repeated.StatusCode);
        var events = await _api.EventsAsync(_targetId);
        var linked = Assert.Single(events.Where(e => e.EventType == "TeamLinkedToProject"));
        using var payload = JsonDocument.Parse(linked.Payload);
        Assert.Equal(_sourceId, payload.RootElement.GetProperty("previousTeamId").GetGuid());
        Assert.Equal("ADMIN", payload.RootElement.GetProperty("linkedBy").GetString());
    }

    public Task InitializeAsync() => Task.CompletedTask;

    public async Task DisposeAsync()
    {
        if (Enabled)
        {
            await _api.QueryDbAsync(async db =>
            {
                await db.Set<TeamEntity>().Where(t => t.Id == _targetId).ExecuteDeleteAsync();
                await db.Set<TeamEntity>().Where(t => t.Id == _sourceId).ExecuteDeleteAsync();
                await db.Set<Project>().Where(p => p.Id == _projectId).ExecuteDeleteAsync();
                await db.Set<OutboxMessage>().Where(m => m.AggregateId == _targetId).ExecuteDeleteAsync();
                return 0;
            });
        }
        await _api.DisposeAsync();
    }
}
