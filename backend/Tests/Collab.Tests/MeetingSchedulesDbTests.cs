using System.Net;
using System.Net.Http.Json;
using BuildingBlocks.Outbox;
using Collab.Repositories.Meetings;
using Collab.Repositories.Workspaces;
using Collab.Services.Meetings;
using Microsoft.EntityFrameworkCore;

namespace Collab.Tests;

/// <summary>
/// Lịch họp hằng tuần với collab_db thật (BE-82): chỉ mục duy nhất, CHECK và thứ tự ghi của EF, những thứ DB InMemory không kiểm tra được.
/// Bật bằng <c>EXE_INTEGRATION=1</c>; container <c>collab-api</c> phải tắt để sự kiện thử không lên Kafka. Dọn dữ liệu thử khi xong.
/// </summary>
public sealed class MeetingSchedulesDbTests : IAsyncLifetime
{
    private static bool Enabled => Environment.GetEnvironmentVariable("EXE_INTEGRATION") == "1";

    private static readonly DateOnly Start = new(2026, 9, 7);
    private const string Link = "https://meet.example.test/abc";

    private readonly CollabApiFactory _api = new() { RealDatabase = true };
    private readonly Guid _semesterId = Guid.NewGuid();
    private readonly List<Guid> _workspaces = [];
    private readonly TestPerson _lecturer = TestPerson.Instructor("Cô Hà");

    private async Task<(Guid WorkspaceId, TestPerson Leader)> TeamAsync(string name)
    {
        var leader = TestPerson.Leader();
        var id = await _api.SeedWorkspaceWithSemesterAsync(name, "EXE201", _semesterId, null, leader, _lecturer);
        _workspaces.Add(id);
        return (id, leader);
    }

    private async Task<MeetingScheduleResponse> SaveAsync(object windows, int? slotMinutes = null)
    {
        var response = await _api.ClientFor(_lecturer).PutAsJsonAsync("/api/meeting-schedules/me", new { windows, slotMinutes, location = Link });
        Assert.True(response.IsSuccessStatusCode, await response.Content.ReadAsStringAsync());
        return await CollabApiFactory.ReadAsync<MeetingScheduleResponse>(response);
    }

    private static object Windows(params (int Day, string Start, string End)[] windows) =>
        windows.Select(w => new { dayOfWeek = w.Day, startTime = w.Start, endTime = w.End }).ToArray();

    private void UseSemester()
    {
        _api.Clock.Now = ScheduleRules.Local(Start.AddDays(2), new TimeOnly(10, 0));
        _api.ScheduleSemesters.Default = new ScheduleSemester(_semesterId, Start, new DateOnly(2026, 10, 25), 2, 15);
    }

    [Fact]
    public async Task Teams_booking_the_same_slot_at_once_leave_exactly_one_winner()
    {
        if (!Enabled)
        {
            return;
        }

        UseSemester();
        var teams = new List<(Guid WorkspaceId, TestPerson Leader)>();
        for (var i = 0; i < 8; i++)
        {
            teams.Add(await TeamAsync($"Nhóm {i}"));
        }
        var slot = (await SaveAsync(Windows((2, "09:00", "09:30")))).Slots[0].Id;
        await _api.ClientFor(_lecturer).PostAsync("/api/meeting-schedules/me/publish", null);

        var responses = await Task.WhenAll(teams.Select(t =>
            _api.ClientFor(t.Leader).PutAsJsonAsync($"/api/workspaces/{t.WorkspaceId}/slot-booking", new { slotId = slot })));

        Assert.Equal(1, responses.Count(r => r.StatusCode == HttpStatusCode.OK));
        foreach (var refused in responses.Where(r => r.StatusCode != HttpStatusCode.OK))
        {
            Assert.Equal((HttpStatusCode.Conflict, "slot-taken"), (refused.StatusCode, await CollabApiFactory.ErrorCodeAsync(refused)));
        }
        Assert.Equal(1, await _api.QueryDbAsync(db => db.Set<SlotBooking>().CountAsync(b => b.SlotId == slot)));
    }

    [Fact]
    public async Task Whole_flow_satisfies_the_database_constraints()
    {
        if (!Enabled)
        {
            return;
        }

        UseSemester();
        var (a, leaderA) = await TeamAsync("Nhóm An");
        var (b, _) = await TeamAsync("Nhóm Bình");
        await SaveAsync(Windows((2, "09:00", "09:30")));
        await SaveAsync(Windows((2, "09:00", "10:00"), (4, "14:00", "14:30")), 10); // cắt lại, đổi khung – slot và khung cũ thay trong một lần lưu
        var schedule = await SaveAsync(Windows((2, "09:00", "09:30"), (4, "14:00", "14:30")), 15);
        Assert.Equal(4, schedule.Slots.Count);
        await _api.ClientFor(_lecturer).PostAsync("/api/meeting-schedules/me/publish", null);

        var book = await _api.ClientFor(leaderA).PutAsJsonAsync($"/api/workspaces/{a}/slot-booking", new { slotId = schedule.Slots[3].Id });
        Assert.Equal(HttpStatusCode.OK, book.StatusCode);
        var change = await _api.ClientFor(leaderA).PutAsJsonAsync($"/api/workspaces/{a}/slot-booking", new { slotId = schedule.Slots[0].Id });
        Assert.Equal(HttpStatusCode.OK, change.StatusCode);

        _api.Clock.Now = ScheduleRules.BookingDeadline(Start, 2).AddMinutes(1);
        var close = await _api.ClientFor(TestPerson.Service()).PostAsync("/internal/meeting-schedules/close-booking", null);
        Assert.True(close.IsSuccessStatusCode, await close.Content.ReadAsStringAsync());
        Assert.Equal((1, 0, 10), await ResultAsync(close));

        var weekly = await _api.QueryDbAsync(db => db.Set<Meeting>().Where(m => m.WorkspaceId == b && m.ScheduleBookingId != null).OrderBy(m => m.WeekNo).ToListAsync());
        Assert.Equal(5, weekly.Count);
        Assert.All(weekly, m => Assert.Equal(("SCHEDULED", "INSTRUCTOR"), (m.Status, m.OrganizerRole)));

        var repeat = await _api.ClientFor(TestPerson.Service()).PostAsync("/internal/meeting-schedules/close-booking", null);
        Assert.Equal((0, 0, 0), await ResultAsync(repeat));

        _api.Clock.Now = ScheduleRules.Local(new DateOnly(2026, 9, 21), new TimeOnly(8, 0));
        var moved = await _api.ClientFor(_lecturer).PutAsJsonAsync($"/api/meeting-schedules/me/bookings/{b}", new { slotId = schedule.Slots[2].Id });
        Assert.True(moved.IsSuccessStatusCode, await moved.Content.ReadAsStringAsync());

        var meetingId = weekly[1].Id;
        var reschedule = await _api.ClientFor(_lecturer).PostAsJsonAsync($"/api/meetings/{meetingId}/reschedule", new { startsAt = _api.Clock.Now.AddDays(10), reason = "Trùng lịch" });
        Assert.True(reschedule.IsSuccessStatusCode, await reschedule.Content.ReadAsStringAsync());
    }

    private static async Task<(int, int, int)> ResultAsync(HttpResponseMessage response)
    {
        var result = await CollabApiFactory.ReadAsync<CloseBookingResponse>(response);
        return (result.AutoBooked, result.TeamsWithoutSlot, result.MeetingsCreated);
    }

    public Task InitializeAsync() => Task.CompletedTask;

    public async Task DisposeAsync()
    {
        if (Enabled)
        {
            await _api.QueryDbAsync(async db =>
            {
                var schedules = await db.Set<MeetingSchedule>().Where(s => s.SemesterId == _semesterId).Select(s => s.Id).ToListAsync();
                await db.Set<OutboxMessage>().Where(m => _workspaces.Contains(m.AggregateId) || schedules.Contains(m.AggregateId)).ExecuteDeleteAsync();
                await db.Set<Meeting>().Where(m => _workspaces.Contains(m.WorkspaceId)).ExecuteUpdateAsync(s => s.SetProperty(m => m.ConfirmedSlotId, (Guid?)null).SetProperty(m => m.Status, "PROPOSED"));
                await db.Set<Meeting>().Where(m => _workspaces.Contains(m.WorkspaceId)).ExecuteDeleteAsync();
                await db.Set<MeetingSchedule>().Where(s => s.SemesterId == _semesterId).ExecuteDeleteAsync(); // khung, slot, booking xóa theo ON DELETE CASCADE
                return await db.Set<Workspace>().Where(w => _workspaces.Contains(w.Id)).ExecuteDeleteAsync();
            });
        }
        await _api.DisposeAsync();
    }
}
