using System.Net;
using System.Net.Http.Json;
using System.Text;
using Collab.Repositories.Tasks;
using Collab.Services.Files;
using Collab.Services.Tasks;

namespace Collab.Tests;

/// <summary>BE-70 – việc, bài nộp, duyệt bài (rule 23); endpoint nội bộ cho Job.</summary>
public sealed class TasksApiTests : IClassFixture<CollabApiFactory>
{
    private readonly CollabApiFactory _factory;
    private readonly TestPerson _leader = TestPerson.Leader();
    private readonly TestPerson _member = TestPerson.Member();
    private readonly TestPerson _instructor = TestPerson.Instructor();
    private readonly TestPerson _mentor = TestPerson.Mentor();
    private Guid _workspace;

    public TasksApiTests(CollabApiFactory factory) => _factory = factory;

    private async Task SeedAsync(Guid? previous = null) =>
        _workspace = await _factory.SeedWorkspaceAsync("Nhóm Tasks", "EXE201", previous, _leader, _member, _instructor, _mentor);

    private async Task<TaskDetailResponse> CreateAsync(TestPerson by, object body)
    {
        var response = await _factory.ClientFor(by).PostAsJsonAsync($"/api/workspaces/{_workspace}/tasks", body);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return await CollabApiFactory.ReadAsync<TaskDetailResponse>(response);
    }

    [Fact]
    public async Task Member_creates_task_with_assignee_and_event_is_written()
    {
        await SeedAsync();

        var task = await CreateAsync(_leader, new { title = "Làm landing page", assigneeStudentIds = new[] { _member.Id } });
        var outsider = await _factory.ClientFor(_leader).PostAsJsonAsync($"/api/workspaces/{_workspace}/tasks",
            new { title = "Sai người", assigneeStudentIds = new[] { Guid.NewGuid() } });

        Assert.Equal(TaskStatuses.ToDo, task.Status);
        Assert.Equal("Thành viên", Assert.Single(task.Assignees).DisplayName);
        Assert.Equal(HttpStatusCode.BadRequest, outsider.StatusCode);
        var assigned = (await _factory.EventsAsync(nameof(TaskAssigned))).Single(e => e.Payload.Contains(task.Id.ToString()));
        Assert.Equal(_workspace, assigned.AggregateId);
    }

    [Fact]
    public async Task Deliverables_are_instructor_only_and_need_due_date_mentor_only_comments()
    {
        await SeedAsync();
        var due = DateTimeOffset.UtcNow.AddDays(3);

        var byStudent = await _factory.ClientFor(_member).PostAsJsonAsync($"/api/workspaces/{_workspace}/tasks",
            new { title = "Báo cáo tuần 3", kind = "DELIVERABLE", dueAt = due });
        var noDue = await _factory.ClientFor(_instructor).PostAsJsonAsync($"/api/workspaces/{_workspace}/tasks",
            new { title = "Báo cáo tuần 3", kind = "DELIVERABLE" });
        var deliverable = await CreateAsync(_instructor, new { title = "Báo cáo tuần 3", kind = "DELIVERABLE", dueAt = due });
        var byMentor = await _factory.ClientFor(_mentor).PostAsJsonAsync($"/api/workspaces/{_workspace}/tasks", new { title = "Việc của mentor" });
        var mentorComment = await _factory.ClientFor(_mentor).PostAsJsonAsync($"/api/tasks/{deliverable.Id}/comments", new { body = "Nhớ phần số liệu" });

        Assert.Equal("instructor-only", await CollabApiFactory.ErrorCodeAsync(byStudent));
        Assert.Equal(HttpStatusCode.BadRequest, noDue.StatusCode);
        Assert.Equal("NOT_SUBMITTED", deliverable.SubmissionStatus);
        Assert.Equal(HttpStatusCode.Forbidden, byMentor.StatusCode);
        Assert.Equal(HttpStatusCode.Created, mentorComment.StatusCode);
        Assert.Contains(await _factory.EventsAsync(nameof(DeliverableCreated)), e => e.Payload.Contains(deliverable.Id.ToString()));
    }

    [Fact]
    public async Task Move_reorders_column_and_done_sets_completed_at()
    {
        await SeedAsync();
        var a = await CreateAsync(_member, new { title = "A" });
        var b = await CreateAsync(_member, new { title = "B" });
        var c = await CreateAsync(_member, new { title = "C" });
        var client = _factory.ClientFor(_member);

        await client.PostAsJsonAsync($"/api/tasks/{c.Id}/move", new { status = "TODO", position = 0 });
        var done = await CollabApiFactory.ReadAsync<TaskCardResponse>(await client.PostAsJsonAsync($"/api/tasks/{a.Id}/move", new { status = "DONE" }));
        var board = await CollabApiFactory.ReadAsync<List<TaskCardResponse>>(await client.GetAsync($"/api/workspaces/{_workspace}/tasks?status=TODO"));
        var detail = await CollabApiFactory.ReadAsync<TaskDetailResponse>(await client.GetAsync($"/api/tasks/{a.Id}"));
        var byInstructor = await _factory.ClientFor(_instructor).PostAsJsonAsync($"/api/tasks/{b.Id}/move", new { status = "DOING" });

        Assert.Equal(TaskStatuses.Done, done.Status);
        Assert.NotNull(detail.CompletedAt);
        Assert.Equal([c.Id, b.Id], board.OrderBy(t => t.Position).Select(t => t.Id));
        Assert.Equal([0, 1], board.OrderBy(t => t.Position).Select(t => t.Position));
        Assert.Equal(HttpStatusCode.Forbidden, byInstructor.StatusCode);
    }

    [Fact]
    public async Task Submission_review_cycle_ends_with_done_deliverable()
    {
        await SeedAsync();
        var deliverable = await CreateAsync(_instructor, new { title = "Báo cáo", kind = "DELIVERABLE", dueAt = DateTimeOffset.UtcNow.AddDays(1) });
        var v1 = await UploadAsync("bao-cao.pdf");
        var member = _factory.ClientFor(_member);
        var instructor = _factory.ClientFor(_instructor);

        var first = await CollabApiFactory.ReadAsync<SubmissionResponse>(
            await member.PostAsJsonAsync($"/api/tasks/{deliverable.Id}/submissions", new { fileVersionId = v1 }));
        var noComment = await instructor.PostAsJsonAsync($"/api/submissions/{first.Id}/review", new { status = "NEEDS_REVISION" });
        var byMember = await member.PostAsJsonAsync($"/api/submissions/{first.Id}/review", new { status = "ACCEPTED" });
        var revision = await instructor.PostAsJsonAsync($"/api/submissions/{first.Id}/review", new { status = "NEEDS_REVISION", comment = "Thiếu phần 2" });
        var again = await instructor.PostAsJsonAsync($"/api/submissions/{first.Id}/review", new { status = "ACCEPTED" });
        var v2 = await UploadAsync("bao-cao.pdf");
        var second = await CollabApiFactory.ReadAsync<SubmissionResponse>(
            await member.PostAsJsonAsync($"/api/tasks/{deliverable.Id}/submissions", new { fileVersionId = v2 }));
        var accepted = await instructor.PostAsJsonAsync($"/api/submissions/{second.Id}/review", new { status = "ACCEPTED" });
        var detail = await CollabApiFactory.ReadAsync<TaskDetailResponse>(await member.GetAsync($"/api/tasks/{deliverable.Id}"));
        var delete = await instructor.DeleteAsync($"/api/tasks/{deliverable.Id}");

        Assert.Equal(1, first.Version);
        Assert.False(first.IsLate);
        Assert.Equal(HttpStatusCode.BadRequest, noComment.StatusCode);
        Assert.Equal("instructor-only", await CollabApiFactory.ErrorCodeAsync(byMember));
        Assert.Equal(HttpStatusCode.OK, revision.StatusCode);
        Assert.Equal("submission-reviewed", await CollabApiFactory.ErrorCodeAsync(again));
        Assert.Equal(2, second.Version);
        Assert.Equal(HttpStatusCode.OK, accepted.StatusCode);
        Assert.Equal(TaskStatuses.Done, detail.Status);
        Assert.Equal(SubmissionStatuses.Accepted, detail.SubmissionStatus);
        Assert.Equal(2, detail.Submissions.Count);
        Assert.Equal("deliverable-has-submissions", await CollabApiFactory.ErrorCodeAsync(delete));
        Assert.Equal(2, (await _factory.EventsAsync(nameof(DeliverableReviewed))).Count(e => e.Payload.Contains(deliverable.Id.ToString())));
        // Phiên bản 2 của file đã nộp → báo cố vấn.
        Assert.Contains(await _factory.EventsAsync(nameof(FileUploaded)), e => e.AggregateId == _workspace);
    }

    [Fact]
    public async Task Late_deliverable_is_overdue_and_listed_for_job_with_recipients()
    {
        await SeedAsync();
        var late = await CreateAsync(_instructor, new { title = "Đã trễ", kind = "DELIVERABLE", dueAt = DateTimeOffset.UtcNow.AddHours(-2) });
        var soon = await CreateAsync(_member, new { title = "Sắp tới hạn", dueAt = DateTimeOffset.UtcNow.AddHours(5), assigneeStudentIds = new[] { _member.Id } });
        await CreateAsync(_member, new { title = "Còn lâu", dueAt = DateTimeOffset.UtcNow.AddDays(5) });

        var overdue = await CollabApiFactory.ReadAsync<List<TaskCardResponse>>(
            await _factory.ClientFor(_member).GetAsync($"/api/workspaces/{_workspace}/tasks?overdue=true"));
        var forbidden = await _factory.ClientFor(_member).GetAsync("/internal/tasks/due");
        var due = await CollabApiFactory.ReadAsync<List<DueTaskResponse>>(
            await _factory.ClientFor(TestPerson.Service()).GetAsync("/internal/tasks/due?withinHours=24"));

        Assert.Equal([late.Id], overdue.Select(t => t.Id));
        Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);
        var mine = due.Where(d => d.WorkspaceId == _workspace).ToList();
        Assert.Equal([late.Id, soon.Id], mine.Select(d => d.Id));
        Assert.Equal(_instructor.Id, mine[0].LecturerId);
        Assert.Equal(new[] { _leader.Id, _member.Id }.Order(), mine[0].MemberStudentIds.Order());
        Assert.Equal([_member.Id], mine[1].AssigneeStudentIds);
    }

    [Fact]
    public async Task Archive_shows_done_tasks_of_exe101_read_only()
    {
        var oldLeader = TestPerson.Leader("Trưởng nhóm cũ");
        var exe101 = await _factory.SeedWorkspaceAsync("Nhóm EXE101", "EXE101", null, oldLeader);
        var oldClient = _factory.ClientFor(oldLeader);
        var done = await CollabApiFactory.ReadAsync<TaskDetailResponse>(
            await oldClient.PostAsJsonAsync($"/api/workspaces/{exe101}/tasks", new { title = "Khảo sát khách hàng" }));
        await oldClient.PostAsJsonAsync($"/api/tasks/{done.Id}/move", new { status = "DONE" });
        await oldClient.PostAsJsonAsync($"/api/workspaces/{exe101}/tasks", new { title = "Chưa xong" });
        await SeedAsync(previous: exe101);
        var client = _factory.ClientFor(_member);

        var board = await CollabApiFactory.ReadAsync<List<TaskCardResponse>>(
            await client.GetAsync($"/api/workspaces/{_workspace}/tasks?includeArchive=true"));
        var edit = await client.PatchAsJsonAsync($"/api/tasks/{done.Id}", new { title = "Sửa" });

        var archived = Assert.Single(board);
        Assert.Equal(done.Id, archived.Id);
        Assert.True(archived.IsArchive);
        Assert.Equal("archive-read-only", await CollabApiFactory.ErrorCodeAsync(edit));
    }

    [Fact]
    public async Task Only_creator_or_leader_deletes_a_task()
    {
        await SeedAsync();
        var task = await CreateAsync(_leader, new { title = "Của trưởng nhóm" });
        var other = await CreateAsync(_member, new { title = "Của thành viên" });

        var memberDeletes = await _factory.ClientFor(_member).DeleteAsync($"/api/tasks/{task.Id}");
        var leaderDeletes = await _factory.ClientFor(_leader).DeleteAsync($"/api/tasks/{other.Id}");

        Assert.Equal(HttpStatusCode.Forbidden, memberDeletes.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, leaderDeletes.StatusCode);
    }

    private async Task<Guid> UploadAsync(string name)
    {
        var form = new MultipartFormDataContent { { new ByteArrayContent(Encoding.UTF8.GetBytes(Guid.NewGuid().ToString())), "file", name } };
        var client = _factory.ClientFor(_member);
        var file = await CollabApiFactory.ReadAsync<UploadFileResponse>(await client.PostAsync($"/api/workspaces/{_workspace}/files", form));
        var detail = await CollabApiFactory.ReadAsync<ProjectFileDetailResponse>(await client.GetAsync($"/api/files/{file.File.Id}"));
        return detail.Versions.Single(v => v.Version == file.File.CurrentVersion).Id;
    }
}
