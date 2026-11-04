using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Collab.Repositories.Messages;
using Collab.Services.Advisor;
using Collab.Services.Meetings;
using Collab.Services.Messages;
using Collab.Services.Tasks;

namespace Collab.Tests;

/// <summary>BE-86 – <c>GET /api/advisor/overview</c>: tổng quan cố vấn cho L01, M01.</summary>
public sealed class AdvisorOverviewApiTests
{
    private sealed record Team(CollabApiFactory Factory, Guid WorkspaceId, Guid AdvisorChannel, TestPerson Leader, TestPerson Instructor, TestPerson Mentor);

    private static async Task<Team> SeedAsync(CollabApiFactory factory, string name = "Nhóm Bình Minh", TestPerson? instructor = null, TestPerson? mentor = null)
    {
        TestPerson leader = TestPerson.Leader();
        instructor ??= TestPerson.Instructor();
        mentor ??= TestPerson.Mentor();
        var workspaceId = await factory.SeedWorkspaceAsync(name, "EXE201", null, leader, instructor, mentor);
        var channel = Guid.NewGuid();
        await factory.WithDbAsync(db =>
        {
            db.Add(new Channel { Id = channel, WorkspaceId = workspaceId, Kind = ChannelKinds.Advisors, CreatedAt = DateTimeOffset.UtcNow });
            return Task.CompletedTask;
        });
        return new Team(factory, workspaceId, channel, leader, instructor, mentor);
    }

    private static async Task<MessageResponse> AskAsync(Team team, string text)
    {
        var response = await team.Factory.ClientFor(team.Leader).PostAsJsonAsync($"/api/channels/{team.AdvisorChannel}/messages",
            new { clientMessageId = Guid.NewGuid(), body = text, isAdvisorQuestion = true });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return await CollabApiFactory.ReadAsync<MessageResponse>(response);
    }

    private static async Task<TaskDetailResponse> DeliverableAsync(Team team, string title, DateTimeOffset dueAt)
    {
        var response = await team.Factory.ClientFor(team.Instructor).PostAsJsonAsync($"/api/workspaces/{team.WorkspaceId}/tasks",
            new { title, kind = "DELIVERABLE", dueAt });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return await CollabApiFactory.ReadAsync<TaskDetailResponse>(response);
    }

    private static async Task<MeetingDetailResponse> MeetingAsync(Team team, TestPerson organizer, string title, params DateTimeOffset[] starts)
    {
        var response = await team.Factory.ClientFor(organizer).PostAsJsonAsync($"/api/workspaces/{team.WorkspaceId}/meetings",
            new { title, location = "Phòng 101", slots = starts.Select(s => new { startsAt = s, durationMinutes = 60 }).ToArray() });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return await CollabApiFactory.ReadAsync<MeetingDetailResponse>(response);
    }

    private static async Task<AdvisorOverviewResponse> OverviewAsync(CollabApiFactory factory, TestPerson person)
    {
        var response = await factory.ClientFor(person).GetAsync("/api/advisor/overview");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await CollabApiFactory.ReadAsync<AdvisorOverviewResponse>(response);
    }

    [Fact]
    public async Task Instructor_sees_open_questions_from_every_team_oldest_first()
    {
        using var factory = new CollabApiFactory();
        var a = await SeedAsync(factory, "Nhóm A");
        var b = await SeedAsync(factory, "Nhóm B", instructor: a.Instructor);
        var first = await AskAsync(a, "Thầy xem giúp mô hình kinh doanh?");
        var second = await AskAsync(b, "Thầy duyệt khảo sát giúp em?");
        var answered = await AskAsync(a, "Câu này đã có trả lời");
        var reply = await factory.ClientFor(a.Mentor).PostAsJsonAsync($"/api/channels/{a.AdvisorChannel}/messages",
            new { clientMessageId = Guid.NewGuid(), body = "Trả lời", parentMessageId = answered.Id });
        Assert.Equal(HttpStatusCode.Created, reply.StatusCode);

        var overview = await OverviewAsync(factory, a.Instructor);

        Assert.Equal(2, overview.NeedsReplyCount);
        Assert.Equal([first.Id, second.Id], overview.NeedsReply.Select(q => q.Message.Id));
        Assert.Equal(["Nhóm A", "Nhóm B"], overview.NeedsReply.Select(q => q.TeamName));
        Assert.All(overview.NeedsReply, q => Assert.Equal("OPEN", q.Status));
        Assert.Equal((0, 0, 0), (overview.OverdueDeliverableCount, overview.UpcomingMeetingCount, overview.PendingScheduleCount));
    }

    [Fact]
    public async Task Overdue_deliverables_are_counted_for_instructor_but_never_for_mentor()
    {
        using var factory = new CollabApiFactory();
        var team = await SeedAsync(factory);
        var late = await DeliverableAsync(team, "Báo cáo tuần 3", DateTimeOffset.UtcNow.AddHours(-2));
        await DeliverableAsync(team, "Báo cáo tuần 4", DateTimeOffset.UtcNow.AddDays(3));

        var instructor = await OverviewAsync(factory, team.Instructor);
        var mentor = await OverviewAsync(factory, team.Mentor);

        Assert.Equal(1, instructor.OverdueDeliverableCount);
        var card = Assert.Single(instructor.OverdueDeliverables);
        Assert.Equal((late.Id, "DELIVERABLE", true, "Nhóm Bình Minh"), (card.Id, card.Kind, card.IsOverdue, card.TeamName));
        Assert.Equal(0, mentor.OverdueDeliverableCount);
        Assert.Empty(mentor.OverdueDeliverables);
    }

    [Fact]
    public async Task Upcoming_lists_scheduled_meetings_and_pending_counts_only_my_proposals()
    {
        using var factory = new CollabApiFactory();
        var team = await SeedAsync(factory);
        var now = DateTimeOffset.UtcNow;
        var later = await MeetingAsync(team, team.Leader, "Họp chốt MVP", now.AddDays(2));
        var sooner = await MeetingAsync(team, team.Mentor, "Buổi mentor", now.AddHours(3));
        await MeetingAsync(team, team.Mentor, "Mentor chờ chốt", now.AddDays(1), now.AddDays(2));
        await MeetingAsync(team, team.Leader, "Nhóm chờ chốt", now.AddDays(1), now.AddDays(3));
        var cancelled = await MeetingAsync(team, team.Leader, "Đã hủy", now.AddDays(4));
        var cancel = await factory.ClientFor(team.Leader).PostAsJsonAsync($"/api/meetings/{cancelled.Id}/cancel", new { reason = "Trùng lịch" });
        Assert.Equal(HttpStatusCode.OK, cancel.StatusCode);

        var mentor = await OverviewAsync(factory, team.Mentor);
        var instructor = await OverviewAsync(factory, team.Instructor);

        Assert.Equal(2, mentor.UpcomingMeetingCount);
        Assert.Equal([sooner.Id, later.Id], mentor.UpcomingMeetings.Select(m => m.Id));
        var first = mentor.UpcomingMeetings[0];
        Assert.Equal(("SCHEDULED", true, (DateTimeOffset?)sooner.StartsAt, 1, false), (first.Status, first.IsMentorSession, first.StartsAt, first.SlotCount, first.MyAnswerPending));
        Assert.Equal(("Mentor", "Nhóm Bình Minh"), (first.Organizer.DisplayName, first.TeamName));
        Assert.Equal(1, mentor.PendingScheduleCount);
        Assert.Equal(0, instructor.PendingScheduleCount);
        Assert.Equal(2, instructor.UpcomingMeetingCount);
    }

    [Fact]
    public async Task Meeting_in_progress_is_still_upcoming_but_finished_one_is_not()
    {
        using var factory = new CollabApiFactory();
        var team = await SeedAsync(factory);
        var start = DateTimeOffset.UtcNow.AddMinutes(10);
        var meeting = await MeetingAsync(team, team.Instructor, "Họp tuần", start);

        factory.Clock.Now = start.AddMinutes(30); // đang họp (khung 60 phút)
        var during = await OverviewAsync(factory, team.Instructor);
        factory.Clock.Now = start.AddMinutes(61);
        var after = await OverviewAsync(factory, team.Instructor);

        Assert.Equal(meeting.Id, Assert.Single(during.UpcomingMeetings).Id);
        Assert.Equal(0, after.UpcomingMeetingCount);
        Assert.Empty(after.UpcomingMeetings);
    }

    [Fact]
    public async Task Arrays_are_capped_at_five_while_counts_stay_total()
    {
        using var factory = new CollabApiFactory();
        var team = await SeedAsync(factory);
        var now = DateTimeOffset.UtcNow;
        for (var i = 1; i <= 7; i++)
        {
            await AskAsync(team, $"Câu hỏi {i}");
            await DeliverableAsync(team, $"Bài nộp {i}", now.AddHours(-i));
            await MeetingAsync(team, team.Leader, $"Họp {i}", now.AddDays(i));
        }

        var overview = await OverviewAsync(factory, team.Instructor);

        Assert.Equal((7, 7, 7), (overview.NeedsReplyCount, overview.OverdueDeliverableCount, overview.UpcomingMeetingCount));
        Assert.Equal((5, 5, 5), (overview.NeedsReply.Count, overview.OverdueDeliverables.Count, overview.UpcomingMeetings.Count));
        Assert.Equal(Enumerable.Range(1, 5).Select(i => $"Họp {i}"), overview.UpcomingMeetings.Select(m => m.Title));
    }

    [Fact]
    public async Task Advisor_without_teams_gets_zeros_and_empty_arrays()
    {
        using var factory = new CollabApiFactory();

        var response = await factory.ClientFor(TestPerson.Mentor()).GetAsync("/api/advisor/overview");
        var json = await CollabApiFactory.ReadAsync<JsonElement>(response);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        foreach (var count in new[] { "needsReplyCount", "overdueDeliverableCount", "upcomingMeetingCount", "pendingScheduleCount" })
        {
            Assert.Equal(0, json.GetProperty(count).GetInt32());
        }
        foreach (var list in new[] { "needsReply", "overdueDeliverables", "upcomingMeetings" })
        {
            Assert.Equal(0, json.GetProperty(list).GetArrayLength());
        }
    }

    [Fact]
    public async Task Students_and_admin_are_forbidden_with_problem_details()
    {
        using var factory = new CollabApiFactory();
        var team = await SeedAsync(factory);

        var student = await factory.ClientFor(team.Leader).GetAsync("/api/advisor/overview");
        var admin = await factory.ClientFor(TestPerson.Admin()).GetAsync("/api/advisor/overview");
        var anonymous = await factory.CreateClient().GetAsync("/api/advisor/overview");

        Assert.Equal(HttpStatusCode.Forbidden, student.StatusCode);
        Assert.Equal("application/problem+json", student.Content.Headers.ContentType?.MediaType);
        Assert.Equal(403, (await CollabApiFactory.ReadAsync<JsonElement>(student)).GetProperty("status").GetInt32());
        Assert.Equal(HttpStatusCode.Forbidden, admin.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, anonymous.StatusCode);
    }
}
