using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Collab.Repositories.Checkpoints;
using Collab.Services.Checkpoints;

namespace Collab.Tests;

public sealed class CheckpointsApiTests
{
    private static readonly TimeZoneInfo Vietnam = TimeZoneInfo.FindSystemTimeZoneById("Asia/Ho_Chi_Minh");

    /// <summary>Ngày hôm nay theo giờ Việt Nam – cách server tính ngày trình bày.</summary>
    private static DateOnly VietnamToday() => DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, Vietnam).DateTime);

    [Fact]
    public async Task Early_morning_in_Vietnam_counts_as_the_local_day_not_the_utc_day()
    {
        // 00:50 ngày 04/11 giờ Việt Nam = 17:50 ngày 03/11 UTC.
        using var factory = new CollabApiFactory();
        factory.Clock.Now = new DateTimeOffset(2026, 11, 3, 17, 50, 0, TimeSpan.Zero);
        var instructor = TestPerson.Instructor();
        var workspaceId = await factory.SeedWorkspaceAsync("Nhóm EXE101", "EXE101", null, instructor);
        var client = factory.ClientFor(instructor);
        var url = $"/api/workspaces/{workspaceId}/checkpoints";

        var marked = await client.PutAsJsonAsync($"{url}/1", new { presentedOn = new DateOnly(2026, 11, 4) });
        var tomorrow = await client.PutAsJsonAsync($"{url}/2", new { presentedOn = new DateOnly(2026, 11, 5) });
        var progress = await CollabApiFactory.ReadAsync<CheckpointProgress>(await client.GetAsync(url));

        Assert.Equal(HttpStatusCode.OK, marked.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, tomorrow.StatusCode);
        Assert.True(progress.Items[0].CanUndo); // ghi nhận lúc 00:50 vẫn trong ngày 04/11
    }

    [Fact]
    public async Task Instructor_marks_checkpoint_and_team_sees_progress_with_event()
    {
        using var factory = new CollabApiFactory();
        var student = TestPerson.Leader();
        var instructor = TestPerson.Instructor();
        var workspaceId = await factory.SeedWorkspaceAsync("Nhóm EXE101", "EXE101", null, student, instructor);
        var day = VietnamToday();

        var marked = await factory.ClientFor(instructor).PutAsJsonAsync(
            $"/api/workspaces/{workspaceId}/checkpoints/2", new { presentedOn = day, comment = "Đã trình bày" });
        var progress = await CollabApiFactory.ReadAsync<CheckpointProgress>(marked);
        var viewed = await factory.ClientFor(student).GetAsync($"/api/workspaces/{workspaceId}/checkpoints");

        Assert.Equal(HttpStatusCode.OK, marked.StatusCode);
        Assert.Equal(1, progress.DoneCount);
        Assert.Equal(4, progress.Items.Count);
        Assert.True(progress.Items[1].Presented);
        Assert.Equal("Đã trình bày", progress.Items[1].Comment);
        Assert.Equal(HttpStatusCode.OK, viewed.StatusCode);
        Assert.Single(await factory.EventsAsync("CheckpointReviewed"));
    }

    [Fact]
    public async Task Mentor_cannot_mark_and_instructor_can_undo_only_same_day()
    {
        using var factory = new CollabApiFactory();
        var instructor = TestPerson.Instructor();
        var mentor = TestPerson.Mentor();
        var workspaceId = await factory.SeedWorkspaceAsync("Nhóm EXE101", "EXE101", null, instructor, mentor);
        var day = VietnamToday();
        var url = $"/api/workspaces/{workspaceId}/checkpoints/1";

        var forbidden = await factory.ClientFor(mentor).PutAsJsonAsync(url, new { presentedOn = day });
        var marked = await factory.ClientFor(instructor).PutAsJsonAsync(url, new { presentedOn = day });
        var duplicate = await factory.ClientFor(instructor).PutAsJsonAsync(url, new { presentedOn = day });
        var undone = await factory.ClientFor(instructor).DeleteAsync(url);

        Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);
        Assert.Equal(HttpStatusCode.OK, marked.StatusCode);
        Assert.Equal("checkpoint-already-reviewed", await CollabApiFactory.ErrorCodeAsync(duplicate));
        Assert.Equal(HttpStatusCode.NoContent, undone.StatusCode);
        var progress = await CollabApiFactory.ReadAsync<CheckpointProgress>(
            await factory.ClientFor(instructor).GetAsync($"/api/workspaces/{workspaceId}/checkpoints"));
        Assert.Equal(0, progress.DoneCount);
    }

    [Fact]
    public async Task Instructor_can_filter_own_groups_missing_checkpoint()
    {
        using var factory = new CollabApiFactory();
        var instructor = TestPerson.Instructor();
        var first = await factory.SeedWorkspaceAsync("Nhóm A", "EXE101", null, instructor);
        var second = await factory.SeedWorkspaceAsync("Nhóm B", "EXE101", null, instructor);
        var day = VietnamToday();
        await factory.ClientFor(instructor).PutAsJsonAsync($"/api/workspaces/{first}/checkpoints/1", new { presentedOn = day });

        var response = await factory.ClientFor(instructor).GetAsync("/api/checkpoints/me?missing=1");
        var groups = await CollabApiFactory.ReadAsync<List<CheckpointProgress>>(response);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(second, Assert.Single(groups).WorkspaceId);
    }

    [Fact]
    public async Task Rejects_invalid_checkpoint_number_future_date_and_long_comment_without_event()
    {
        using var factory = new CollabApiFactory();
        var instructor = TestPerson.Instructor();
        var workspaceId = await factory.SeedWorkspaceAsync("Nhóm EXE101", "EXE101", null, instructor);
        var client = factory.ClientFor(instructor);
        var today = VietnamToday();
        var baseUrl = $"/api/workspaces/{workspaceId}/checkpoints";

        var zero = await client.PutAsJsonAsync($"{baseUrl}/0", new { presentedOn = today });
        var five = await client.PutAsJsonAsync($"{baseUrl}/5", new { presentedOn = today });
        var future = await client.PutAsJsonAsync($"{baseUrl}/1", new { presentedOn = today.AddDays(1) });
        var longComment = await client.PutAsJsonAsync($"{baseUrl}/1",
            new { presentedOn = today, comment = new string('x', 501) });

        Assert.Equal(HttpStatusCode.BadRequest, zero.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, five.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, future.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, longComment.StatusCode);
        Assert.Empty(await factory.EventsAsync("CheckpointReviewed"));
    }

    [Fact]
    public async Task Non_exe101_workspace_rejects_checkpoints_and_outsider_is_forbidden()
    {
        using var factory = new CollabApiFactory();
        var instructor = TestPerson.Instructor();
        var student = TestPerson.Leader();
        var outsider = TestPerson.Member("Người ngoài");
        var workspaceId = await factory.SeedWorkspaceAsync("Nhóm EXE201", "EXE201", null, instructor, student);
        var today = VietnamToday();
        var url = $"/api/workspaces/{workspaceId}/checkpoints";

        var viewed = await factory.ClientFor(student).GetAsync(url);
        var marked = await factory.ClientFor(instructor).PutAsJsonAsync($"{url}/1", new { presentedOn = today });
        var outsiderView = await factory.ClientFor(outsider).GetAsync(url);

        Assert.Equal("checkpoint-exe101-only", await CollabApiFactory.ErrorCodeAsync(viewed));
        Assert.Equal("checkpoint-exe101-only", await CollabApiFactory.ErrorCodeAsync(marked));
        Assert.Equal(HttpStatusCode.Forbidden, outsiderView.StatusCode);
        Assert.Empty(await factory.EventsAsync("CheckpointReviewed"));
    }

    [Fact]
    public async Task Checkpoint_marked_on_previous_day_cannot_be_undone()
    {
        using var factory = new CollabApiFactory();
        var instructor = TestPerson.Instructor();
        var workspaceId = await factory.SeedWorkspaceAsync("Nhóm EXE101", "EXE101", null, instructor);
        await factory.WithDbAsync(db =>
        {
            db.Set<CheckpointReview>().Add(new CheckpointReview
            {
                Id = Guid.NewGuid(),
                WorkspaceId = workspaceId,
                CheckpointNo = 3,
                PresentedOn = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-1)),
                ReviewedByLecturerId = instructor.Id,
                ReviewedByName = instructor.Name,
                CreatedAt = DateTimeOffset.UtcNow.AddDays(-1),
            });
            return Task.CompletedTask;
        });
        var client = factory.ClientFor(instructor);
        var url = $"/api/workspaces/{workspaceId}/checkpoints";

        var progress = await CollabApiFactory.ReadAsync<CheckpointProgress>(await client.GetAsync(url));
        var undone = await client.DeleteAsync($"{url}/3");

        Assert.Equal(1, progress.DoneCount);
        Assert.False(progress.Items[2].CanUndo);
        Assert.Equal("checkpoint-locked", await CollabApiFactory.ErrorCodeAsync(undone));
    }

    [Fact]
    public async Task Another_instructor_cannot_undo_and_event_targets_active_students()
    {
        using var factory = new CollabApiFactory();
        var first = TestPerson.Instructor("Giảng viên A");
        var second = TestPerson.Instructor("Giảng viên B");
        var student = TestPerson.Leader();
        var workspaceId = await factory.SeedWorkspaceAsync("Nhóm EXE101", "EXE101", null,
            first, second, student);
        var today = VietnamToday();
        var url = $"/api/workspaces/{workspaceId}/checkpoints/1";

        var marked = await factory.ClientFor(first).PutAsJsonAsync(url,
            new { presentedOn = today, comment = "  Đã trình bày  " });
        var undone = await factory.ClientFor(second).DeleteAsync(url);
        var progress = await CollabApiFactory.ReadAsync<CheckpointProgress>(
            await factory.ClientFor(student).GetAsync($"/api/workspaces/{workspaceId}/checkpoints"));

        Assert.Equal(HttpStatusCode.OK, marked.StatusCode);
        Assert.Equal("checkpoint-locked", await CollabApiFactory.ErrorCodeAsync(undone));
        Assert.Equal("Đã trình bày", progress.Items[0].Comment);
        Assert.Equal(1, progress.DoneCount);
        var published = Assert.Single(await factory.EventsAsync("CheckpointReviewed"));
        using var payload = JsonDocument.Parse(published.Payload);
        Assert.Equal(student.Id.ToString(), Assert.Single(payload.RootElement
            .GetProperty("memberStudentIds").EnumerateArray()).GetString());
        Assert.Equal("Đã trình bày", payload.RootElement.GetProperty("comment").GetString());
    }

    [Fact]
    public async Task Lecturer_group_list_rejects_invalid_filter_and_excludes_other_groups()
    {
        using var factory = new CollabApiFactory();
        var instructor = TestPerson.Instructor();
        var other = TestPerson.Instructor("Giảng viên khác");
        var student = TestPerson.Leader();
        var mine = await factory.SeedWorkspaceAsync("Nhóm của tôi", "EXE101", null, instructor);
        await factory.SeedWorkspaceAsync("Nhóm khác", "EXE101", null, other);
        await factory.SeedWorkspaceAsync("Nhóm EXE201", "EXE201", null, instructor);

        var listed = await factory.ClientFor(instructor).GetAsync("/api/checkpoints/me");
        var groups = await CollabApiFactory.ReadAsync<List<CheckpointProgress>>(listed);
        var invalid = await factory.ClientFor(instructor).GetAsync("/api/checkpoints/me?missing=5");
        var forbidden = await factory.ClientFor(student).GetAsync("/api/checkpoints/me");

        Assert.Equal(HttpStatusCode.OK, listed.StatusCode);
        Assert.Equal(mine, Assert.Single(groups).WorkspaceId);
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
        Assert.Equal("lecturer-only", await CollabApiFactory.ErrorCodeAsync(forbidden));
    }

    [Fact]
    public async Task Target_weeks_and_overdue_calculation_reflects_semester_settings()
    {
        using var factory = new CollabApiFactory();
        var semesterId = Guid.NewGuid();
        var instructor = TestPerson.Instructor();
        var student = TestPerson.Leader();
        var workspaceId = await factory.SeedWorkspaceWithSemesterAsync("Nhóm EXE101 TargetWeek", "EXE101", semesterId, null, instructor, student);

        // Giả sử học kỳ bắt đầu cách đây 21 ngày (3 tuần tròn) -> hôm nay đang ở Tuần 4
        var today = VietnamToday();
        var startDate = today.AddDays(-21);
        factory.AcademicSemesters.Configs[semesterId] = new SemesterCheckpointConfig(startDate, new[] { 2, 3, 5, 8 });

        // Đánh dấu Checkpoint 2 đã trình bày
        await factory.ClientFor(instructor).PutAsJsonAsync(
            $"/api/workspaces/{workspaceId}/checkpoints/2", new { presentedOn = today, comment = "Hoàn thành CP2" });

        var response = await factory.ClientFor(student).GetAsync($"/api/workspaces/{workspaceId}/checkpoints");
        var progress = await CollabApiFactory.ReadAsync<CheckpointProgress>(response);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(1, progress.DoneCount);

        // CP 1: TargetWeek = 2, chưa trình bày, tuần hiện tại 4 > 2 -> Overdue = true
        Assert.Equal(2, progress.Items[0].TargetWeek);
        Assert.False(progress.Items[0].Presented);
        Assert.True(progress.Items[0].Overdue);

        // CP 2: TargetWeek = 3, ĐÃ trình bày -> Overdue = false
        Assert.Equal(3, progress.Items[1].TargetWeek);
        Assert.True(progress.Items[1].Presented);
        Assert.False(progress.Items[1].Overdue);

        // CP 3: TargetWeek = 5, chưa trình bày, tuần hiện tại 4 <= 5 -> Overdue = false
        Assert.Equal(5, progress.Items[2].TargetWeek);
        Assert.False(progress.Items[2].Presented);
        Assert.False(progress.Items[2].Overdue);

        // CP 4: TargetWeek = 8, chưa trình bày, tuần hiện tại 4 <= 8 -> Overdue = false
        Assert.Equal(8, progress.Items[3].TargetWeek);
        Assert.False(progress.Items[3].Presented);
        Assert.False(progress.Items[3].Overdue);
    }

    [Fact]
    public async Task Academic_semester_client_null_config_graceful_fallback()
    {
        using var factory = new CollabApiFactory();
        var instructor = TestPerson.Instructor();
        var student = TestPerson.Leader();
        // Không cấu hình semester config (trả về null)
        var workspaceId = await factory.SeedWorkspaceAsync("Nhóm EXE101 Fallback", "EXE101", null, instructor, student);

        var response = await factory.ClientFor(student).GetAsync($"/api/workspaces/{workspaceId}/checkpoints");
        var progress = await CollabApiFactory.ReadAsync<CheckpointProgress>(response);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.All(progress.Items, item =>
        {
            Assert.Null(item.TargetWeek);
            Assert.False(item.Overdue);
        });
    }
}
