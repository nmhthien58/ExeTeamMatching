using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Collab.Repositories.Tasks;
using Collab.Services.Meetings;
using Microsoft.EntityFrameworkCore;

namespace Collab.Tests;

/// <summary>Họp đề xuất, biên bản, nhắc họp (BE-68).</summary>
public sealed class MeetingsApiTests
{
    private static DateTimeOffset InDays(int days) => DateTimeOffset.UtcNow.AddDays(days);

    private static object Slots(params DateTimeOffset[] starts) => starts.Select(s => new { startsAt = s, durationMinutes = 30 }).ToArray();

    private sealed record Team(CollabApiFactory Factory, Guid WorkspaceId, TestPerson Leader, TestPerson Member, TestPerson Instructor, TestPerson Mentor);

    private static async Task<Team> SeedAsync(CollabApiFactory factory)
    {
        TestPerson leader = TestPerson.Leader(), member = TestPerson.Member(), instructor = TestPerson.Instructor(), mentor = TestPerson.Mentor();
        var workspaceId = await factory.SeedWorkspaceAsync("Nhóm Bình Minh", "EXE101", null, leader, member, instructor, mentor);
        return new Team(factory, workspaceId, leader, member, instructor, mentor);
    }

    private static async Task<MeetingDetailResponse> ProposeAsync(Team team, TestPerson organizer, params DateTimeOffset[] starts)
    {
        var response = await team.Factory.ClientFor(organizer).PostAsJsonAsync($"/api/workspaces/{team.WorkspaceId}/meetings",
            new { title = " Họp nhóm ", agenda = "Chốt ý tưởng", location = "Phòng 101", slots = Slots(starts) });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return await CollabApiFactory.ReadAsync<MeetingDetailResponse>(response);
    }

    private static async Task AnswerAsync(Team team, TestPerson person, Guid meetingId, params (Guid Slot, string Answer)[] answers)
    {
        var response = await team.Factory.ClientFor(person).PutAsJsonAsync($"/api/meetings/{meetingId}/responses",
            new { answers = answers.Select(a => new { slotId = a.Slot, answer = a.Answer }) });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Team_answers_proposal_and_organizer_confirms_suggested_slot()
    {
        using var factory = new CollabApiFactory();
        var team = await SeedAsync(factory);
        var proposed = await ProposeAsync(team, team.Leader, InDays(2), InDays(1));

        Assert.Equal(("PROPOSED", "Họp nhóm", 2, false, (DateTimeOffset?)null), (proposed.Status, proposed.Title, proposed.SlotCount, proposed.IsMentorSession, proposed.StartsAt));
        Assert.Equal(("Trưởng nhóm", "LEADER"), (proposed.Organizer.DisplayName, proposed.Organizer.Role));
        var (early, late) = (proposed.Slots[0], proposed.Slots[1]);
        Assert.True(early.StartsAt < late.StartsAt);
        Assert.Equal(3, early.InviteeCount); // mọi người trừ người tổ chức

        var event1 = JsonDocument.Parse(Assert.Single(await factory.EventsAsync("MeetingProposed")).Payload).RootElement;
        Assert.Equal((2, 3), (event1.GetProperty("slots").GetArrayLength(), event1.GetProperty("invitees").GetArrayLength()));

        var memberView = await CollabApiFactory.ReadAsync<MeetingListResponse>(await factory.ClientFor(team.Member).GetAsync($"/api/workspaces/{team.WorkspaceId}/meetings"));
        Assert.True(memberView.Items.Single().MyAnswerPending);

        await AnswerAsync(team, team.Member, proposed.Id, (early.Id, "NO"), (late.Id, "YES"));
        await AnswerAsync(team, team.Instructor, proposed.Id, (late.Id, "YES"));
        await AnswerAsync(team, team.Mentor, proposed.Id, (early.Id, "YES"));
        await AnswerAsync(team, team.Mentor, proposed.Id, (early.Id, "NO"), (late.Id, "YES")); // đổi ý

        var detail = await CollabApiFactory.ReadAsync<MeetingDetailResponse>(await factory.ClientFor(team.Member).GetAsync($"/api/meetings/{proposed.Id}"));
        Assert.Equal(late.Id, detail.SuggestedSlotId);
        var lateView = detail.Slots.Single(s => s.Id == late.Id);
        Assert.Equal((3, "YES", 3), (lateView.YesCount, lateView.MyAnswer, lateView.YesPeople.Count));
        Assert.Equal(0, detail.Slots.Single(s => s.Id == early.Id).YesCount);
        Assert.False(detail.MyAnswerPending);

        var denied = await factory.ClientFor(team.Member).PostAsJsonAsync($"/api/meetings/{proposed.Id}/confirm", new { slotId = late.Id });
        Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
        Assert.Equal("not-organizer", await CollabApiFactory.ErrorCodeAsync(denied));

        var confirmed = await CollabApiFactory.ReadAsync<MeetingDetailResponse>(
            await factory.ClientFor(team.Leader).PostAsJsonAsync($"/api/meetings/{proposed.Id}/confirm", new { slotId = late.Id }));
        Assert.Equal(("SCHEDULED", (DateTimeOffset?)late.StartsAt, (Guid?)null), (confirmed.Status, confirmed.StartsAt, confirmed.SuggestedSlotId));
        var event2 = JsonDocument.Parse(Assert.Single(await factory.EventsAsync("MeetingScheduled")).Payload).RootElement;
        Assert.Equal(late.Id, event2.GetProperty("confirmedSlotId").GetGuid());

        var again = await factory.ClientFor(team.Member).PutAsJsonAsync($"/api/meetings/{proposed.Id}/responses", new { answers = new[] { new { slotId = late.Id, answer = "NO" } } });
        Assert.Equal((HttpStatusCode.Conflict, "meeting-not-open"), (again.StatusCode, await CollabApiFactory.ErrorCodeAsync(again)));
    }

    [Fact]
    public async Task One_slot_is_scheduled_at_once_and_only_leader_or_advisor_can_organize()
    {
        using var factory = new CollabApiFactory();
        var team = await SeedAsync(factory);
        var start = InDays(3);
        var meeting = await ProposeAsync(team, team.Instructor, start);

        Assert.Equal(("SCHEDULED", (DateTimeOffset?)meeting.Slots.Single().StartsAt), (meeting.Status, meeting.StartsAt));
        Assert.Single(await factory.EventsAsync("MeetingScheduled"));
        Assert.Empty(await factory.EventsAsync("MeetingProposed"));

        var url = $"/api/workspaces/{team.WorkspaceId}/meetings";
        var body = new { title = "Họp", slots = Slots(InDays(1)) };
        var member = await factory.ClientFor(team.Member).PostAsJsonAsync(url, body);
        Assert.Equal((HttpStatusCode.Forbidden, "not-organizer"), (member.StatusCode, await CollabApiFactory.ErrorCodeAsync(member)));
        var admin = await factory.ClientFor(TestPerson.Admin()).PostAsJsonAsync(url, body);
        Assert.Equal((HttpStatusCode.Forbidden, "workspace-read-only"), (admin.StatusCode, await CollabApiFactory.ErrorCodeAsync(admin)));
        var outsider = await factory.ClientFor(TestPerson.Leader("Người ngoài")).GetAsync($"/api/meetings/{meeting.Id}");
        Assert.Equal(HttpStatusCode.Forbidden, outsider.StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await factory.ClientFor(TestPerson.Admin()).GetAsync($"/api/meetings/{meeting.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await factory.ClientFor(team.Leader).GetAsync($"/api/meetings/{Guid.NewGuid()}")).StatusCode);
    }

    [Fact]
    public async Task Proposal_input_is_validated()
    {
        using var factory = new CollabApiFactory();
        var team = await SeedAsync(factory);
        var client = factory.ClientFor(team.Leader);
        var url = $"/api/workspaces/{team.WorkspaceId}/meetings";
        var start = InDays(1);

        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync(url, new { title = "", slots = Slots(start) })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync(url, new { title = "Họp", slots = Array.Empty<object>() })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync(url, new { title = "Họp", slots = Slots(InDays(1), InDays(2), InDays(3), InDays(4)) })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync(url, new { title = "Họp", slots = Slots(InDays(-1)) })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync(url, new { title = "Họp", slots = Slots(start, start) })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync(url, new { title = "Họp", slots = new[] { new { startsAt = start, durationMinutes = 5 } } })).StatusCode);
    }

    [Fact]
    public async Task Organizer_cannot_answer_and_answers_must_belong_to_meeting()
    {
        using var factory = new CollabApiFactory();
        var team = await SeedAsync(factory);
        var meeting = await ProposeAsync(team, team.Leader, InDays(1), InDays(2));
        var answer = new { answers = new[] { new { slotId = meeting.Slots[0].Id, answer = "YES" } } };

        Assert.Equal(HttpStatusCode.Forbidden, (await factory.ClientFor(team.Leader).PutAsJsonAsync($"/api/meetings/{meeting.Id}/responses", answer)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await factory.ClientFor(TestPerson.Admin()).PutAsJsonAsync($"/api/meetings/{meeting.Id}/responses", answer)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await factory.ClientFor(team.Member).PutAsJsonAsync($"/api/meetings/{meeting.Id}/responses",
            new { answers = new[] { new { slotId = Guid.NewGuid(), answer = "YES" } } })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await factory.ClientFor(team.Member).PutAsJsonAsync($"/api/meetings/{meeting.Id}/responses",
            new { answers = new[] { new { slotId = meeting.Slots[0].Id, answer = "MAYBE" } } })).StatusCode);
    }

    [Fact]
    public async Task Organizer_adds_slot_up_to_three_keeping_answers()
    {
        using var factory = new CollabApiFactory();
        var team = await SeedAsync(factory);
        var meeting = await ProposeAsync(team, team.Leader, InDays(1), InDays(2));
        await AnswerAsync(team, team.Member, meeting.Id, (meeting.Slots[0].Id, "YES"));
        var client = factory.ClientFor(team.Leader);
        var url = $"/api/meetings/{meeting.Id}/slots";

        Assert.Equal(HttpStatusCode.Forbidden, (await factory.ClientFor(team.Member).PostAsJsonAsync(url, new { startsAt = InDays(3) })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync(url, new { startsAt = meeting.Slots[0].StartsAt })).StatusCode);

        var added = await client.PostAsJsonAsync(url, new { startsAt = InDays(3) });
        Assert.Equal(HttpStatusCode.Created, added.StatusCode);
        var detail = await CollabApiFactory.ReadAsync<MeetingDetailResponse>(added);
        Assert.Equal((3, 1, 60), (detail.Slots.Count, detail.Slots[0].YesCount, detail.Slots[2].DurationMinutes));
        Assert.Equal(2, (await factory.EventsAsync("MeetingProposed")).Count); // người được mời biết có khung mới

        var limit = await client.PostAsJsonAsync(url, new { startsAt = InDays(4) });
        Assert.Equal((HttpStatusCode.Conflict, "meeting-slot-limit"), (limit.StatusCode, await CollabApiFactory.ErrorCodeAsync(limit)));
    }

    [Fact]
    public async Task Organizer_cancels_with_reason()
    {
        using var factory = new CollabApiFactory();
        var team = await SeedAsync(factory);
        var meeting = await ProposeAsync(team, team.Mentor, InDays(1));
        var url = $"/api/meetings/{meeting.Id}/cancel";

        Assert.Equal(HttpStatusCode.BadRequest, (await factory.ClientFor(team.Mentor).PostAsJsonAsync(url, new { reason = " " })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await factory.ClientFor(team.Leader).PostAsJsonAsync(url, new { reason = "Bận" })).StatusCode);

        var cancelled = await CollabApiFactory.ReadAsync<MeetingDetailResponse>(await factory.ClientFor(team.Mentor).PostAsJsonAsync(url, new { reason = " Bận đột xuất " }));
        Assert.Equal(("CANCELLED", "Bận đột xuất", (DateTimeOffset?)null), (cancelled.Status, cancelled.CancelReason, cancelled.StartsAt));
        var payload = JsonDocument.Parse(Assert.Single(await factory.EventsAsync("MeetingCancelled")).Payload).RootElement;
        Assert.Equal("Bận đột xuất", payload.GetProperty("cancelReason").GetString());

        var again = await factory.ClientFor(team.Mentor).PostAsJsonAsync(url, new { reason = "Lần nữa" });
        Assert.Equal((HttpStatusCode.Conflict, "meeting-not-open"), (again.StatusCode, await CollabApiFactory.ErrorCodeAsync(again)));
        var filtered = await CollabApiFactory.ReadAsync<MeetingListResponse>(await factory.ClientFor(team.Leader).GetAsync($"/api/workspaces/{team.WorkspaceId}/meetings?status=SCHEDULED"));
        Assert.Empty(filtered.Items);
    }

    [Fact]
    public async Task Minutes_finish_mentor_session_and_lines_become_tasks()
    {
        using var factory = new CollabApiFactory();
        var team = await SeedAsync(factory);
        var proposed = await ProposeAsync(team, team.Mentor, InDays(1), InDays(2));
        Assert.True(proposed.IsMentorSession);
        var client = factory.ClientFor(team.Mentor);
        var minutesUrl = $"/api/meetings/{proposed.Id}/minutes";

        var tooEarly = await client.PutAsJsonAsync(minutesUrl, new { content = "Biên bản" });
        Assert.Equal((HttpStatusCode.Conflict, "meeting-not-confirmed"), (tooEarly.StatusCode, await CollabApiFactory.ErrorCodeAsync(tooEarly)));
        await client.PostAsJsonAsync($"/api/meetings/{proposed.Id}/confirm", new { slotId = proposed.Slots[0].Id });
        Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsJsonAsync($"/api/meetings/{proposed.Id}/minutes/tasks", new { items = new[] { new { title = "Việc" } } })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await factory.ClientFor(team.Leader).PutAsJsonAsync(minutesUrl, new { content = "Biên bản" })).StatusCode);

        var done = await CollabApiFactory.ReadAsync<MeetingDetailResponse>(await client.PutAsJsonAsync(minutesUrl, new { content = "Biên bản buổi 1" }));
        Assert.Equal(("DONE", "Biên bản buổi 1", "Mentor"), (done.Status, done.Minutes!.Content, done.Minutes.RecordedByName));
        var edited = await CollabApiFactory.ReadAsync<MeetingDetailResponse>(await client.PutAsJsonAsync(minutesUrl, new { content = "Biên bản đã sửa" }));
        Assert.Equal("Biên bản đã sửa", edited.Minutes!.Content);

        var list = await CollabApiFactory.ReadAsync<MeetingListResponse>(await factory.ClientFor(team.Member).GetAsync($"/api/workspaces/{team.WorkspaceId}/meetings"));
        Assert.Equal((1, MentorSessionTargetClient.DefaultSessions), (list.MentorSessionsDone, list.ExpectedMentorSessions));

        var created = await client.PostAsJsonAsync($"/api/meetings/{proposed.Id}/minutes/tasks", new { items = new[] { new { title = "Viết báo cáo" }, new { title = "Gọi khách hàng" } } });
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var tasks = await factory.QueryDbAsync(db => db.Set<TaskItem>().Where(t => t.SourceMeetingId == proposed.Id).ToListAsync());
        Assert.Equal(2, tasks.Count);
        var detail = await CollabApiFactory.ReadAsync<MeetingDetailResponse>(await client.GetAsync($"/api/meetings/{proposed.Id}"));
        Assert.Equal(["Gọi khách hàng", "Viết báo cáo"], detail.CreatedTasks.Select(t => t.Title).Order());
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync($"/api/meetings/{proposed.Id}/minutes/tasks", new { items = Array.Empty<object>() })).StatusCode);
    }

    [Fact]
    public async Task Job_reads_upcoming_meetings_once_per_offset()
    {
        using var factory = new CollabApiFactory();
        var team = await SeedAsync(factory);
        var soon = await ProposeAsync(team, team.Leader, DateTimeOffset.UtcNow.AddMinutes(40));
        await ProposeAsync(team, team.Leader, InDays(3));
        await ProposeAsync(team, team.Leader, DateTimeOffset.UtcNow.AddMinutes(20), DateTimeOffset.UtcNow.AddMinutes(30)); // chưa chốt
        var service = factory.ClientFor(TestPerson.Service());

        var hour = await CollabApiFactory.ReadAsync<List<UpcomingMeetingResponse>>(await service.GetAsync("/internal/meetings/upcoming?offsetMinutes=60"));
        Assert.Equal(soon.Id, hour.Single().Id);
        Assert.Equal(4, hour.Single().Invitees.Count); // cả người tổ chức cũng được nhắc
        var day = await CollabApiFactory.ReadAsync<List<UpcomingMeetingResponse>>(await service.GetAsync("/internal/meetings/upcoming?offsetMinutes=1440"));
        Assert.Equal(soon.Id, day.Single().Id);

        var mark = await service.PostAsJsonAsync($"/internal/meetings/{soon.Id}/reminders", new { offsetMinutes = 60 });
        Assert.Equal(HttpStatusCode.NoContent, mark.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await service.PostAsJsonAsync($"/internal/meetings/{soon.Id}/reminders", new { offsetMinutes = 60 })).StatusCode); // gọi lại không lỗi
        Assert.Empty(await CollabApiFactory.ReadAsync<List<UpcomingMeetingResponse>>(await service.GetAsync("/internal/meetings/upcoming?offsetMinutes=60")));
        Assert.Single(await CollabApiFactory.ReadAsync<List<UpcomingMeetingResponse>>(await service.GetAsync("/internal/meetings/upcoming?offsetMinutes=1440")));

        Assert.Equal(HttpStatusCode.BadRequest, (await service.GetAsync("/internal/meetings/upcoming?offsetMinutes=30")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await service.PostAsJsonAsync($"/internal/meetings/{Guid.NewGuid()}/reminders", new { offsetMinutes = 60 })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await factory.ClientFor(team.Leader).GetAsync("/internal/meetings/upcoming?offsetMinutes=60")).StatusCode);
    }
}
