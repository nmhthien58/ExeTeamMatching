using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Collab.Repositories.Meetings;
using Collab.Services.Meetings;
using Collab.Services.Realtime;
using Microsoft.EntityFrameworkCore;

namespace Collab.Tests;

/// <summary>Lịch họp hằng tuần EXE201 (BE-82, C09, rule 25). Bản chống đặt trùng với Postgres thật ở <see cref="MeetingSchedulesDbTests"/>.</summary>
public sealed class MeetingSchedulesApiTests
{
    private static readonly DateOnly Start = new(2026, 9, 7);
    private static readonly DateOnly End = new(2026, 10, 25); // 7 tuần
    private static readonly Guid SemesterId = Guid.NewGuid();
    private static readonly DateTimeOffset InWeekOne = ScheduleRules.Local(Start.AddDays(2), new TimeOnly(10, 0));
    private static readonly DateTimeOffset AfterDeadline = ScheduleRules.BookingDeadline(Start, 2).AddMinutes(1);

    private const string Link = "https://meet.example.test/abc";

    private sealed record Team(Guid WorkspaceId, TestPerson Leader, TestPerson Member);

    private sealed record Setup(CollabApiFactory Factory, TestPerson Lecturer, Team A, Team B, Team C)
    {
        public HttpClient AsLecturer => Factory.ClientFor(Lecturer);
    }

    private static async Task<Setup> SeedAsync(CollabApiFactory factory)
    {
        factory.Clock.Now = InWeekOne;
        factory.ScheduleSemesters.Default = new ScheduleSemester(SemesterId, Start, End, SlotBookingDeadlineWeek: 2, MeetingSlotMinutes: 15);
        var lecturer = TestPerson.Instructor("Cô Hà");

        async Task<Team> TeamAsync(string name)
        {
            TestPerson leader = TestPerson.Leader(), member = TestPerson.Member();
            return new Team(await factory.SeedWorkspaceWithSemesterAsync(name, "EXE201", SemesterId, null, leader, member, lecturer), leader, member);
        }

        return new Setup(factory, lecturer, await TeamAsync("Nhóm An"), await TeamAsync("Nhóm Bình"), await TeamAsync("Nhóm Chi"));
    }

    private static object Windows(params (int Day, string Start, string End)[] windows) =>
        windows.Select(w => new { dayOfWeek = w.Day, startTime = w.Start, endTime = w.End }).ToArray();

    private static async Task<MeetingScheduleResponse> SaveAsync(Setup s, object windows, int? slotMinutes = null)
    {
        var response = await s.AsLecturer.PutAsJsonAsync("/api/meeting-schedules/me", new { windows, slotMinutes, location = Link });
        Assert.True(response.IsSuccessStatusCode, await response.Content.ReadAsStringAsync());
        return await CollabApiFactory.ReadAsync<MeetingScheduleResponse>(response);
    }

    private static async Task<MeetingScheduleResponse> PublishAsync(Setup s, object? windows = null)
    {
        await SaveAsync(s, windows ?? Windows((2, "09:00", "10:00")));
        var response = await s.AsLecturer.PostAsync("/api/meeting-schedules/me/publish", null);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await CollabApiFactory.ReadAsync<MeetingScheduleResponse>(response);
    }

    private static Task<HttpResponseMessage> BookAsync(Setup s, Team team, Guid slotId) =>
        s.Factory.ClientFor(team.Leader).PutAsJsonAsync($"/api/workspaces/{team.WorkspaceId}/slot-booking", new { slotId });

    private static async Task<TeamMeetingScheduleResponse> ViewAsync(Setup s, Team team) =>
        await CollabApiFactory.ReadAsync<TeamMeetingScheduleResponse>(await s.Factory.ClientFor(team.Member).GetAsync($"/api/workspaces/{team.WorkspaceId}/meeting-schedule"));

    private static async Task<CloseBookingResponse> CloseAsync(Setup s)
    {
        s.Factory.Clock.Now = AfterDeadline;
        var response = await s.Factory.ClientFor(TestPerson.Service()).PostAsync("/internal/meeting-schedules/close-booking", null);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await CollabApiFactory.ReadAsync<CloseBookingResponse>(response);
    }

    private static async Task<List<MeetingCardResponse>> WeeklyAsync(Setup s, Team team) =>
        (await CollabApiFactory.ReadAsync<MeetingListResponse>(await s.Factory.ClientFor(team.Member).GetAsync($"/api/workspaces/{team.WorkspaceId}/meetings")))
        .Items.Where(m => m.IsWeekly).OrderBy(m => m.WeekNo).ToList();

    [Fact]
    public async Task Lecturer_cuts_slots_and_teams_see_nothing_until_published()
    {
        using var factory = new CollabApiFactory();
        var s = await SeedAsync(factory);

        Assert.Equal("null", await (await s.AsLecturer.GetAsync("/api/meeting-schedules/me")).Content.ReadAsStringAsync());

        var draft = await SaveAsync(s, Windows((2, "09:00", "09:45")));
        Assert.Equal(("DRAFT", 15, Link, 3, 3), (draft.Status, draft.SlotMinutes, draft.Location, draft.TeamCount, draft.TeamsWithoutSlot.Count));
        Assert.Equal(["09:00", "09:15", "09:30"], draft.Slots.Select(x => x.StartTime));
        Assert.All(draft.Slots, x => Assert.Equal("FREE", x.State));
        Assert.Equal(ScheduleRules.BookingDeadline(Start, 2), draft.BookingDeadline);

        var read = await factory.ClientFor(s.A.Member).GetAsync($"/api/workspaces/{s.A.WorkspaceId}/meeting-schedule");
        Assert.Equal((HttpStatusCode.OK, "null"), (read.StatusCode, await read.Content.ReadAsStringAsync()));
        Assert.Empty(await factory.EventsAsync("MeetingSlotsPublished"));

        var published = await CollabApiFactory.ReadAsync<MeetingScheduleResponse>(await s.AsLecturer.PostAsync("/api/meeting-schedules/me/publish", null));
        Assert.Equal("PUBLISHED", published.Status);
        Assert.Empty(published.Warnings);

        var published1 = Assert.Single(await factory.EventsAsync("MeetingSlotsPublished")); // một sự kiện cho cả lịch, key = id lịch
        Assert.Equal(published.Id, published1.AggregateId);
        var payload = JsonDocument.Parse(published1.Payload).RootElement;
        Assert.Equal((3, "Cô Hà", 6), (payload.GetProperty("slotCount").GetInt32(), payload.GetProperty("lecturerName").GetString(), payload.GetProperty("recipients").GetArrayLength()));
        Assert.All(payload.GetProperty("recipients").EnumerateArray(), r => Assert.Equal("STUDENT", r.GetProperty("personType").GetString()));

        await s.AsLecturer.PostAsync("/api/meeting-schedules/me/publish", null);
        Assert.Single(await factory.EventsAsync("MeetingSlotsPublished")); // đăng lại không báo lần nữa

        var view = await ViewAsync(s, s.A);
        Assert.Equal(("Cô Hà", "PUBLISHED", Link, false, (ScheduleSlotResponse?)null, 3), (view.LecturerName, view.Status, view.Location, view.CanBook, view.MySlot, view.Slots.Count));
    }

    [Fact]
    public async Task Too_few_slots_still_publish_with_a_warning()
    {
        using var factory = new CollabApiFactory();
        var s = await SeedAsync(factory);
        var published = await PublishAsync(s, Windows((2, "09:00", "09:30")));
        Assert.Equal(["NOT_ENOUGH_SLOTS"], published.Warnings);
    }

    [Fact]
    public async Task Slot_length_defaults_to_the_semester_setting()
    {
        using var factory = new CollabApiFactory();
        var s = await SeedAsync(factory);
        factory.ScheduleSemesters.Default = factory.ScheduleSemesters.Default! with { MeetingSlotMinutes = 20 };
        Assert.Equal(20, (await SaveAsync(s, Windows((2, "09:00", "10:00")))).SlotMinutes);
        Assert.Equal(30, (await SaveAsync(s, Windows((2, "09:00", "10:00")), 30)).SlotMinutes);
    }

    [Fact]
    public async Task Deadline_falls_back_when_academic_is_down()
    {
        using var factory = new CollabApiFactory();
        var s = await SeedAsync(factory);
        factory.ScheduleSemesters.Default = null;
        var draft = await SaveAsync(s, Windows((2, "09:00", "10:00")));
        Assert.Equal((InWeekOne.AddDays(14), 15), (draft.BookingDeadline, draft.SlotMinutes));
    }

    [Fact]
    public async Task Lecturer_without_exe201_team_cannot_create_a_schedule()
    {
        using var factory = new CollabApiFactory();
        var lecturer = TestPerson.Instructor();
        var exe101 = await factory.SeedWorkspaceAsync("Nhóm EXE101", "EXE101", null, TestPerson.Leader(), lecturer);

        var response = await factory.ClientFor(lecturer).PutAsJsonAsync("/api/meeting-schedules/me", new { windows = Windows((2, "09:00", "10:00")), location = Link });
        Assert.Equal((HttpStatusCode.Conflict, "no-exe201-team"), (response.StatusCode, await CollabApiFactory.ErrorCodeAsync(response)));
        Assert.Equal("null", await (await factory.ClientFor(lecturer).GetAsync($"/api/workspaces/{exe101}/meeting-schedule")).Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Students_and_bad_windows_are_rejected()
    {
        using var factory = new CollabApiFactory();
        var s = await SeedAsync(factory);
        var body = new { windows = Windows((2, "09:00", "10:00")), location = Link };
        Assert.Equal(HttpStatusCode.Forbidden, (await factory.ClientFor(s.A.Leader).PutAsJsonAsync("/api/meeting-schedules/me", body)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await factory.ClientFor(s.A.Leader).GetAsync("/api/meeting-schedules/me")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await s.AsLecturer.PostAsync("/api/meeting-schedules/me/publish", null)).StatusCode);

        var overlap = await s.AsLecturer.PutAsJsonAsync("/api/meeting-schedules/me", new { windows = Windows((2, "09:00", "10:00"), (2, "09:30", "11:00")), location = Link });
        Assert.Equal(HttpStatusCode.BadRequest, overlap.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await s.AsLecturer.PutAsJsonAsync("/api/meeting-schedules/me", new { windows = Windows((2, "09:00", "10:00")) })).StatusCode);
    }

    [Fact]
    public async Task First_leader_gets_the_slot_and_teams_do_not_see_each_other()
    {
        using var factory = new CollabApiFactory();
        var s = await SeedAsync(factory);
        var schedule = await PublishAsync(s);
        var (first, second) = (schedule.Slots[0].Id, schedule.Slots[1].Id);

        var booked = await BookAsync(s, s.A, first);
        Assert.Equal(HttpStatusCode.OK, booked.StatusCode);
        var view = await CollabApiFactory.ReadAsync<TeamMeetingScheduleResponse>(booked);
        Assert.Equal((first, "MINE", true), (view.MySlot!.Id, view.MySlot.State, view.CanBook));

        var taken = await BookAsync(s, s.B, first);
        Assert.Equal((HttpStatusCode.Conflict, "slot-taken"), (taken.StatusCode, await CollabApiFactory.ErrorCodeAsync(taken)));

        var other = await ViewAsync(s, s.B);
        Assert.Equal("TAKEN", other.Slots.Single(x => x.Id == first).State);
        Assert.All(other.Slots, x => Assert.Equal(((Guid?)null, (string?)null), (x.WorkspaceId, x.TeamName)));

        var forLecturer = await CollabApiFactory.ReadAsync<MeetingScheduleResponse>(await s.AsLecturer.GetAsync("/api/meeting-schedules/me"));
        var slot = forLecturer.Slots.Single(x => x.Id == first);
        Assert.Equal(("TAKEN", s.A.WorkspaceId, "Nhóm An"), (slot.State, slot.WorkspaceId!.Value, slot.TeamName));
        Assert.Equal(["Nhóm Bình", "Nhóm Chi"], forLecturer.TeamsWithoutSlot.Select(t => t.TeamName));

        var payload = JsonDocument.Parse(Assert.Single(await factory.EventsAsync("MeetingSlotBooked")).Payload).RootElement;
        Assert.Equal(("LEADER", 2, "09:00", "09:15", 2), (payload.GetProperty("source").GetString(), payload.GetProperty("dayOfWeek").GetInt32(),
            payload.GetProperty("startTime").GetString(), payload.GetProperty("endTime").GetString(), payload.GetProperty("recipients").GetArrayLength()));

        Assert.Equal(3, factory.Realtime.Sent.Count(e => e.Event == HubEvents.SlotsChanged)); // mọi nhóm trong lịch

        Assert.Equal(HttpStatusCode.OK, (await BookAsync(s, s.A, second)).StatusCode); // đổi tới hạn
        Assert.Equal(second, (await ViewAsync(s, s.A)).MySlot!.Id);
        Assert.Equal("FREE", (await ViewAsync(s, s.B)).Slots.Single(x => x.Id == first).State);
        Assert.Equal(1, await factory.QueryDbAsync(db => db.Set<SlotBooking>().CountAsync(b => b.WorkspaceId == s.A.WorkspaceId))); // một nhóm một dòng

        Assert.Equal(HttpStatusCode.OK, (await BookAsync(s, s.A, second)).StatusCode); // chọn lại đúng slot không báo lại
        Assert.Equal(2, (await factory.EventsAsync("MeetingSlotBooked")).Count);
    }

    [Fact]
    public async Task Only_the_leader_books_within_the_deadline()
    {
        using var factory = new CollabApiFactory();
        var s = await SeedAsync(factory);
        var slot = (await PublishAsync(s)).Slots[0].Id;
        var url = $"/api/workspaces/{s.A.WorkspaceId}/slot-booking";

        var member = await factory.ClientFor(s.A.Member).PutAsJsonAsync(url, new { slotId = slot });
        Assert.Equal((HttpStatusCode.Conflict, "not-leader"), (member.StatusCode, await CollabApiFactory.ErrorCodeAsync(member)));
        Assert.False((await ViewAsync(s, s.A)).CanBook);

        Assert.Equal(HttpStatusCode.Forbidden, (await factory.ClientFor(TestPerson.Admin()).PutAsJsonAsync(url, new { slotId = slot })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await factory.ClientFor(s.B.Leader).PutAsJsonAsync(url, new { slotId = slot })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await BookAsync(s, s.A, Guid.NewGuid())).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await BookAsync(s, s.A, Guid.Empty)).StatusCode);

        factory.Clock.Now = AfterDeadline;
        var late = await BookAsync(s, s.A, slot);
        Assert.Equal((HttpStatusCode.Conflict, "booking-closed"), (late.StatusCode, await CollabApiFactory.ErrorCodeAsync(late)));
    }

    [Fact]
    public async Task Editing_keeps_booked_slots_and_refuses_to_drop_them()
    {
        using var factory = new CollabApiFactory();
        var s = await SeedAsync(factory);
        var slots = (await PublishAsync(s, Windows((2, "09:00", "09:30")))).Slots;
        Assert.Equal(HttpStatusCode.OK, (await BookAsync(s, s.A, slots[1].Id)).StatusCode);

        var bigger = await SaveAsync(s, Windows((2, "09:00", "10:00"), (4, "14:00", "14:15")));
        Assert.Equal(5, bigger.Slots.Count);
        Assert.Equal(slots[1].Id, bigger.Slots.Single(x => x.StartTime == "09:15").Id); // slot cũ giữ id, booking không mất
        Assert.Equal("TAKEN", bigger.Slots.Single(x => x.StartTime == "09:15").State);

        var dropped = await s.AsLecturer.PutAsJsonAsync("/api/meeting-schedules/me", new { windows = Windows((3, "09:00", "10:00")), location = Link });
        Assert.Equal((HttpStatusCode.Conflict, "slot-booked"), (dropped.StatusCode, await CollabApiFactory.ErrorCodeAsync(dropped)));

        var longer = await s.AsLecturer.PutAsJsonAsync("/api/meeting-schedules/me", new { windows = Windows((2, "09:00", "10:00")), slotMinutes = 30, location = Link });
        Assert.Equal("slot-booked", await CollabApiFactory.ErrorCodeAsync(longer)); // slot đã có nhóm không đổi giờ

        Assert.Equal(5, (await CollabApiFactory.ReadAsync<MeetingScheduleResponse>(await s.AsLecturer.GetAsync("/api/meeting-schedules/me"))).Slots.Count);
    }

    [Fact]
    public async Task Unbooked_slots_are_recut_when_the_length_changes()
    {
        using var factory = new CollabApiFactory();
        var s = await SeedAsync(factory);
        await SaveAsync(s, Windows((2, "09:00", "10:00")));
        var recut = await SaveAsync(s, Windows((2, "09:00", "10:00")), 30);
        Assert.Equal([("09:00", "09:30"), ("09:30", "10:00")], recut.Slots.Select(x => (x.StartTime, x.EndTime)));
    }

    [Fact]
    public async Task Close_places_waiting_teams_and_creates_weekly_meetings_once()
    {
        using var factory = new CollabApiFactory();
        var s = await SeedAsync(factory);
        var slots = (await PublishAsync(s, Windows((2, "09:00", "09:30")))).Slots;
        Assert.Equal(HttpStatusCode.OK, (await BookAsync(s, s.B, slots[0].Id)).StatusCode);

        var result = await CloseAsync(s);
        Assert.Equal((1, 1, 10), (result.AutoBooked, result.TeamsWithoutSlot, result.MeetingsCreated)); // hai nhóm × tuần 3–7

        var mine = await CollabApiFactory.ReadAsync<MeetingScheduleResponse>(await s.AsLecturer.GetAsync("/api/meeting-schedules/me"));
        Assert.Equal("CLOSED", mine.Status);
        Assert.Equal("Nhóm Chi", mine.TeamsWithoutSlot.Single().TeamName);
        Assert.Equal(["NOT_ENOUGH_SLOTS"], mine.Warnings);
        Assert.Equal(s.A.WorkspaceId, mine.Slots.Single(x => x.Id == slots[1].Id).WorkspaceId); // xếp theo tên nhóm

        var auto = (await factory.EventsAsync("MeetingSlotBooked")).Select(e => JsonDocument.Parse(e.Payload).RootElement).Single(e => e.GetProperty("source").GetString() == "AUTO");
        Assert.Equal(s.A.WorkspaceId, auto.GetProperty("workspaceId").GetGuid());

        var meetings = await WeeklyAsync(s, s.A);
        Assert.Equal([3, 4, 5, 6, 7], meetings.Select(m => m.WeekNo!.Value));
        var week3 = meetings[0];
        Assert.Equal(("Họp hằng tuần – Tuần 3", "SCHEDULED", Link, "Cô Hà", ScheduleRules.Local(new DateOnly(2026, 9, 22), new TimeOnly(9, 15))),
            (week3.Title, week3.Status, week3.Location, week3.Organizer.DisplayName, week3.StartsAt!.Value));
        Assert.Empty(await factory.EventsAsync("MeetingScheduled")); // không báo từng tuần

        Assert.Equal((0, 0, 0), ((await CloseAsync(s)).AutoBooked, (await CloseAsync(s)).TeamsWithoutSlot, (await CloseAsync(s)).MeetingsCreated)); // gọi lại không tạo trùng
        Assert.Equal(5, (await WeeklyAsync(s, s.A)).Count);
    }

    [Fact]
    public async Task Close_waits_for_the_deadline_and_for_academic()
    {
        using var factory = new CollabApiFactory();
        var s = await SeedAsync(factory);
        await PublishAsync(s);
        var job = factory.ClientFor(TestPerson.Service());

        var early = await CollabApiFactory.ReadAsync<CloseBookingResponse>(await job.PostAsync("/internal/meeting-schedules/close-booking", null));
        Assert.Equal(0, early.AutoBooked + early.MeetingsCreated);

        factory.Clock.Now = AfterDeadline;
        factory.ScheduleSemesters.Default = null;
        var down = await job.PostAsync("/internal/meeting-schedules/close-booking", null);
        Assert.Equal((HttpStatusCode.ServiceUnavailable, "semester-unavailable"), (down.StatusCode, await CollabApiFactory.ErrorCodeAsync(down)));
        Assert.Equal("PUBLISHED", (await ViewAsync(s, s.A)).Status); // Job thử lại, lịch chưa đóng

        Assert.Equal(HttpStatusCode.Forbidden, (await factory.ClientFor(TestPerson.Admin()).PostAsync("/internal/meeting-schedules/close-booking", null)).StatusCode);
    }

    [Fact]
    public async Task Draft_schedules_are_not_closed()
    {
        using var factory = new CollabApiFactory();
        var s = await SeedAsync(factory);
        await SaveAsync(s, Windows((2, "09:00", "10:00")));
        Assert.Equal(0, (await CloseAsync(s)).MeetingsCreated);
        Assert.Equal("DRAFT", (await CollabApiFactory.ReadAsync<MeetingScheduleResponse>(await s.AsLecturer.GetAsync("/api/meeting-schedules/me"))).Status);
    }

    [Fact]
    public async Task Short_slots_create_meetings_of_the_database_minimum()
    {
        using var factory = new CollabApiFactory();
        var s = await SeedAsync(factory);
        await SaveAsync(s, Windows((2, "09:00", "09:30")), 10);
        await s.AsLecturer.PostAsync("/api/meeting-schedules/me/publish", null);
        await CloseAsync(s);
        var durations = await factory.QueryDbAsync(db => db.Set<MeetingSlot>().Select(x => x.DurationMinutes).Distinct().ToListAsync());
        Assert.Equal([(short)15], durations);
    }

    [Fact]
    public async Task Lecturer_moves_a_team_and_future_weeks_follow()
    {
        using var factory = new CollabApiFactory();
        var s = await SeedAsync(factory);
        var slots = (await PublishAsync(s, Windows((2, "09:00", "09:45")))).Slots;
        Assert.Equal(HttpStatusCode.OK, (await BookAsync(s, s.A, slots[0].Id)).StatusCode);
        await CloseAsync(s); // A: 09:00, B: 09:15, C: 09:30 – tuần 3–7, đóng sau hạn tuần 2

        factory.Clock.Now = ScheduleRules.Local(new DateOnly(2026, 9, 30), new TimeOnly(8, 0)); // tuần 4 đã qua, tuần 5 chưa
        var url = $"/api/meeting-schedules/me/bookings/{s.A.WorkspaceId}";

        var taken = await s.AsLecturer.PutAsJsonAsync(url, new { slotId = slots[1].Id });
        Assert.Equal((HttpStatusCode.Conflict, "slot-taken"), (taken.StatusCode, await CollabApiFactory.ErrorCodeAsync(taken)));

        await s.AsLecturer.PutAsJsonAsync("/api/meeting-schedules/me", new { windows = Windows((2, "09:00", "09:45"), (5, "14:00", "14:15")), location = Link });
        var free = (await CollabApiFactory.ReadAsync<MeetingScheduleResponse>(await s.AsLecturer.GetAsync("/api/meeting-schedules/me"))).Slots.Single(x => x.DayOfWeek == 5);

        var moved = await s.AsLecturer.PutAsJsonAsync(url, new { slotId = free.Id }); // sau hạn vẫn đổi được
        Assert.Equal(HttpStatusCode.OK, moved.StatusCode);

        var weeks = await WeeklyAsync(s, s.A);
        Assert.Equal(ScheduleRules.Local(new DateOnly(2026, 9, 29), new TimeOnly(9, 0)), weeks.Single(m => m.WeekNo == 4).StartsAt); // đã diễn ra, giữ nguyên
        Assert.Equal(ScheduleRules.Local(new DateOnly(2026, 10, 9), new TimeOnly(14, 0)), weeks.Single(m => m.WeekNo == 5).StartsAt);
        Assert.Equal(5, weeks.Count); // không sinh thêm tuần

        var booked = (await factory.EventsAsync("MeetingSlotBooked")).Select(e => JsonDocument.Parse(e.Payload).RootElement).Last();
        Assert.Equal("INSTRUCTOR", booked.GetProperty("source").GetString());
    }

    [Fact]
    public async Task Team_without_a_slot_gets_meetings_when_the_lecturer_places_it()
    {
        using var factory = new CollabApiFactory();
        var s = await SeedAsync(factory);
        await PublishAsync(s, Windows((2, "09:00", "09:30")));
        Assert.Equal(1, (await CloseAsync(s)).TeamsWithoutSlot);
        Assert.Empty(await WeeklyAsync(s, s.C));

        var added = await SaveAsync(s, Windows((2, "09:00", "09:45"))); // sau khi đóng vẫn thêm được khung (rule 25)
        var free = added.Slots.Single(x => x.State == "FREE");
        Assert.Equal(HttpStatusCode.OK, (await s.AsLecturer.PutAsJsonAsync($"/api/meeting-schedules/me/bookings/{s.C.WorkspaceId}", new { slotId = free.Id })).StatusCode);

        Assert.Equal(5, (await WeeklyAsync(s, s.C)).Count);
    }

    [Fact]
    public async Task Lecturer_cannot_touch_teams_of_others_or_move_before_publishing()
    {
        using var factory = new CollabApiFactory();
        var s = await SeedAsync(factory);
        var slot = (await SaveAsync(s, Windows((2, "09:00", "10:00")))).Slots[0].Id;

        var draft = await s.AsLecturer.PutAsJsonAsync($"/api/meeting-schedules/me/bookings/{s.A.WorkspaceId}", new { slotId = slot });
        Assert.Equal("booking-closed", await CollabApiFactory.ErrorCodeAsync(draft));

        var stranger = TestPerson.Instructor("Thầy Khác");
        await factory.SeedWorkspaceWithSemesterAsync("Nhóm Khác", "EXE201", SemesterId, null, TestPerson.Leader(), stranger);
        var denied = await factory.ClientFor(stranger).PutAsJsonAsync($"/api/meeting-schedules/me/bookings/{s.A.WorkspaceId}", new { slotId = slot });
        Assert.Equal((HttpStatusCode.Forbidden, "not-workspace-advisor"), (denied.StatusCode, await CollabApiFactory.ErrorCodeAsync(denied)));
    }

    [Fact]
    public async Task Lecturer_moves_or_cancels_a_single_week()
    {
        using var factory = new CollabApiFactory();
        var s = await SeedAsync(factory);
        await PublishAsync(s);
        await CloseAsync(s);
        var weeks = await WeeklyAsync(s, s.A);
        var (week3, week4) = (weeks[0], weeks[1]);

        await factory.WithDbAsync(db =>
        {
            db.Set<MeetingReminder>().Add(new MeetingReminder { MeetingId = week3.Id, OffsetMinutes = 1440, SentAt = factory.Clock.Now });
            return Task.CompletedTask;
        });
        var newTime = week3.StartsAt!.Value.AddDays(1);
        var moved = await s.AsLecturer.PostAsJsonAsync($"/api/meetings/{week3.Id}/reschedule", new { startsAt = newTime, reason = " Trùng lịch thi " });
        Assert.Equal(HttpStatusCode.OK, moved.StatusCode);
        Assert.Equal(newTime, (await CollabApiFactory.ReadAsync<MeetingDetailResponse>(moved)).StartsAt);
        Assert.Equal(week4.StartsAt, (await WeeklyAsync(s, s.A))[1].StartsAt); // tuần khác không đổi
        Assert.Equal(0, await factory.QueryDbAsync(db => db.Set<MeetingReminder>().CountAsync(r => r.MeetingId == week3.Id))); // nhắc lại theo giờ mới

        var scheduled = JsonDocument.Parse(Assert.Single(await factory.EventsAsync("MeetingScheduled")).Payload).RootElement;
        Assert.Equal(("Trùng lịch thi", 2), (scheduled.GetProperty("cancelReason").GetString(), scheduled.GetProperty("invitees").GetArrayLength()));

        Assert.Equal(HttpStatusCode.Forbidden, (await factory.ClientFor(s.A.Leader).PostAsJsonAsync($"/api/meetings/{week4.Id}/reschedule", new { startsAt = newTime, reason = "x" })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await s.AsLecturer.PostAsJsonAsync($"/api/meetings/{week4.Id}/reschedule", new { startsAt = newTime })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await s.AsLecturer.PostAsJsonAsync($"/api/meetings/{week4.Id}/reschedule", new { startsAt = factory.Clock.Now.AddDays(-1), reason = "x" })).StatusCode);

        Assert.Equal(HttpStatusCode.OK, (await s.AsLecturer.PostAsJsonAsync($"/api/meetings/{week4.Id}/cancel", new { reason = "Nghỉ lễ" })).StatusCode);
        var again = await s.AsLecturer.PostAsJsonAsync($"/api/meetings/{week4.Id}/reschedule", new { startsAt = newTime, reason = "x" });
        Assert.Equal((HttpStatusCode.Conflict, "meeting-not-open"), (again.StatusCode, await CollabApiFactory.ErrorCodeAsync(again)));

        Assert.Equal(0, (await CloseAsync(s)).MeetingsCreated); // tuần đã hủy không sinh lại
        Assert.Equal("CANCELLED", (await WeeklyAsync(s, s.A)).Single(m => m.WeekNo == 4).Status);
    }

    [Fact]
    public async Task Proposed_meetings_cannot_be_rescheduled_as_weekly()
    {
        using var factory = new CollabApiFactory();
        var s = await SeedAsync(factory);
        var created = await s.AsLecturer.PostAsJsonAsync($"/api/workspaces/{s.A.WorkspaceId}/meetings",
            new { title = "Họp thêm", slots = new[] { new { startsAt = InWeekOne.AddDays(2), durationMinutes = 30 } } });
        var meeting = await CollabApiFactory.ReadAsync<MeetingDetailResponse>(created);

        var response = await s.AsLecturer.PostAsJsonAsync($"/api/meetings/{meeting.Id}/reschedule", new { startsAt = InWeekOne.AddDays(3), reason = "x" });
        Assert.Equal((HttpStatusCode.Conflict, "meeting-not-weekly"), (response.StatusCode, await CollabApiFactory.ErrorCodeAsync(response)));
    }
}
