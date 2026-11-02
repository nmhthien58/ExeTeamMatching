using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using BuildingBlocks.Auth;
using BuildingBlocks.Outbox;
using Microsoft.EntityFrameworkCore;
using Team.Repositories.AdvisorMatching;
using Team.Repositories.AuditLog;
using Team.Repositories.MentorAssignments;
using Team.Repositories.Teams;
using Team.Repositories.TopicInterests;
using Team.Services.Academic;
using Team.Services.AdvisorMatching;
using Team.Services.Teams;

namespace Team.Tests;

/// <summary>
/// Ghép mentor (BE-80, rule 19, 20): Job, Ghép sớm, W28, đổi / gỡ mentor tay với team_db thật, Academic giả và planner thật của Matching.
/// Sức chứa tính từ <c>mentor_assignments</c> chung hai môn trong học kỳ. Bật bằng <c>EXE_INTEGRATION=1</c> (chuỗi kết nối qua <c>EXE_TEAM_DB</c>).
/// </summary>
public sealed class MentorMatchingApiTests : IAsyncLifetime
{
    private static bool Enabled => Environment.GetEnvironmentVariable("EXE_INTEGRATION") == "1";

    private readonly TeamApiFactory _api = new();

    private FakeAcademic Academic => _api.Academic;

    private HttpClient Admin => _api.ClientFor(null, Roles.Admin);

    private HttpClient Job => _api.ClientFor(null, Roles.Service);

    [Fact]
    public async Task Job_run_fills_preferred_numbers_first_then_up_to_maximum_and_is_idempotent()
    {
        if (!Enabled)
        {
            return;
        }

        var lecturerId = Guid.NewGuid();
        var an = Academic.AddMentor("Mentor An", maxTeams: 3, preferredTeams: 1, domains: [5], lecturerId: lecturerId);
        var binh = Academic.AddMentor("Mentor Bình", maxTeams: 2, preferredTeams: 2, domains: [1]);
        Academic.AddMentor("Mentor Đã Khóa", maxTeams: 9, domains: [1], active: false);

        var interested = await TeamAsync("A Được Quan Tâm");
        var second = await TeamAsync("B Cũng Quan Tâm");
        var third = await TeamAsync("C Theo Chuyên Môn");
        var fourth = await TeamAsync("D Theo Chuyên Môn");
        var anInterest = await InterestAsync(interested.Id, an);
        var binhInterest = await InterestAsync(interested.Id, binh);
        await InterestAsync(second.Id, an);

        var run = await RunJobAsync();
        Assert.Equal(("MENTOR", "EXE101", "SCHEDULED", "APPLIED"), (run.AdvisorKind, run.CourseCode, run.Trigger, run.Status));
        Assert.Equal((4, 0), (run.MatchedCount, run.UnmatchedCount));

        // Matching chỉ nhận mentor đang hoạt động, không có nguyện vọng của nhóm (sinh viên không chọn mentor).
        var request = _api.Matching.AdvisorRequests.Single();
        Assert.Equal(new[] { an.Id, binh.Id }.Order(), request.Advisors.Select(a => a.AdvisorId).Order());
        Assert.All(request.Teams, t => Assert.Empty(t.PreferredAdvisorIds));

        var rows = await AssignmentsAsync();
        Assert.Equal(4, rows.Count);
        Assert.All(rows, x => Assert.Equal(("AUTO", run.Id, "ASSIGNED"), (x.Source, x.MatchingRunId, x.Status)));
        // An tới số mong muốn (1) trước, Bình 2; nhóm thứ tư mới vượt số mong muốn của An (tối đa 3).
        Assert.Equal(2, rows.Count(x => x.MentorId == an.Id));
        Assert.Equal(2, rows.Count(x => x.MentorId == binh.Id));
        Assert.Contains(rows, x => x.TeamId == interested.Id && x.Reason == "MENTOR_INTEREST");
        Assert.Equal(("MENTOR_INTEREST", an.Id), Row(rows, second.Id));

        var interests = await _api.QueryDbAsync(db => db.Set<TopicInterest>().AsNoTracking().Where(i => i.TeamId == interested.Id).ToListAsync());
        Assert.All(interests, i => Assert.Equal("CLOSED", i.Status));
        Assert.Contains(interests, i => i.Id == anInterest || i.Id == binhInterest);

        var assigned = (await _api.EventsAsync(second.Id)).Single(e => e.EventType == "MentorAssigned");
        Assert.Equal("exe.assignment", assigned.Topic);
        var payload = Payload(assigned);
        Assert.Equal((an.Id, "Mentor An", lecturerId, "MENTOR_INTEREST", "AUTO", run.Id),
            (payload.GetProperty("mentorId").GetGuid(), payload.GetProperty("mentorName").GetString(), payload.GetProperty("mentorLecturerId").GetGuid(),
                payload.GetProperty("reason").GetString(), payload.GetProperty("source").GetString(), payload.GetProperty("matchingRunId").GetGuid()));
        Assert.Equal(new[] { second.Leader!.Id }, payload.GetProperty("memberStudentIds").EnumerateArray().Select(x => x.GetGuid()));
        Assert.False(payload.TryGetProperty("replacedMentorId", out _));

        var completed = Payload((await _api.EventsAsync(run.Id)).Single());
        Assert.Equal(("MENTOR", 4, 0), (completed.GetProperty("advisorKind").GetString(), completed.GetProperty("matchedCount").GetInt32(),
            completed.GetProperty("unmatchedCount").GetInt32()));

        var audit = await _api.QueryDbAsync(db => db.Set<AuditLogEntry>().AsNoTracking()
            .Where(x => x.TeamId == third.Id && x.Action == MentorAssignmentWriter.AuditAction).ToListAsync());
        Assert.Equal("System", Assert.Single(audit).ActorRole);

        // Gọi lại: trả lần cũ, không ghép thêm.
        var again = await RunJobAsync();
        Assert.Equal(run.Id, again.Id);
        Assert.Equal(4, (await AssignmentsAsync()).Count);
        Assert.Single(_api.Matching.AdvisorRequests);
        Assert.Contains(rows, x => x.TeamId == fourth.Id);
    }

    [Fact]
    public async Task Capacity_counts_mentor_teams_of_both_courses_and_ignores_academic_current_count()
    {
        if (!Enabled)
        {
            return;
        }

        var an = Academic.AddMentor("Mentor An", maxTeams: 1, domains: [1]);
        var leader = Academic.Enroll(specializationId: 1, course: "EXE201");
        var created = await _api.ClientFor(leader.Id).PostAsJsonAsync("/api/teams", new TeamInput("Nhóm EXE201", null, null, [1], null));
        Assert.True(created.StatusCode == HttpStatusCode.Created, await created.Content.ReadAsStringAsync());
        var exe201 = (await created.Content.ReadFromJsonAsync<TeamDetailResponse>())!;
        Assert.Equal("EXE201", exe201.CourseCode);
        await AssignDirectlyAsync(exe201.Id, an);
        var exe101 = await TeamAsync("Nhóm EXE101");

        var run = await RunJobAsync();

        Assert.Equal((0, 1), (run.MatchedCount, run.UnmatchedCount));
        Assert.Equal(1, _api.Matching.AdvisorRequests.Single().Advisors.Single().CurrentTeamCount);
        Assert.DoesNotContain(await AssignmentsAsync(), x => x.TeamId == exe101.Id);
    }

    [Fact]
    public async Task Early_matching_previews_then_confirm_applies_only_teams_still_without_mentor()
    {
        if (!Enabled)
        {
            return;
        }

        var an = Academic.AddMentor("Mentor An", maxTeams: 5, domains: [1]);
        var first = await TeamAsync("A Nhóm");
        var second = await TeamAsync("B Nhóm");

        var preview = await PostRunAsync(Admin.PostAsJsonAsync("/api/advisor-matching/MENTOR/preview", new { courseCode = "EXE101" }));
        Assert.Equal(("EARLY", "PREVIEW", 2), (preview.Trigger, preview.Status, preview.MatchedCount));
        Assert.Empty(await AssignmentsAsync());

        await AssignDirectlyAsync(first.Id, an);
        var confirmed = await PostRunAsync(Admin.PostAsync($"/api/advisor-matching/runs/{preview.Id}/confirm", null));
        Assert.Equal(("APPLIED", 1), (confirmed.Status, confirmed.MatchedCount));
        Assert.Equal(("DOMAIN", an.Id), Row(await AssignmentsAsync(), second.Id));
    }

    [Fact]
    public async Task Admin_changes_mentor_with_warning_above_preferred_and_block_above_maximum()
    {
        if (!Enabled)
        {
            return;
        }

        var an = Academic.AddMentor("Mentor An", maxTeams: 2, preferredTeams: 1);
        var binh = Academic.AddMentor("Mentor Bình", maxTeams: 3);
        var locked = Academic.AddMentor("Mentor Đã Khóa", maxTeams: 3, active: false);
        var first = await TeamAsync("A Nhóm");
        var second = await TeamAsync("B Nhóm");
        var third = await TeamAsync("C Nhóm");
        var binhInterest = await InterestAsync(second.Id, binh);

        await AssertProblemAsync(await _api.ClientFor(first.Leader!.Id).PutAsJsonAsync($"/api/teams/{first.Id}/mentor-assignment", new { mentorId = an.Id, note = "x" }), 403, null);
        await AssertProblemAsync(await ChangeAsync(first.Id, an.Id, " "), 400, "validation");
        await AssertProblemAsync(await ChangeAsync(first.Id, Guid.NewGuid(), "Phân công"), 404, "not-found");
        await AssertProblemAsync(await ChangeAsync(first.Id, locked.Id, "Phân công"), 400, "validation");
        await AssertProblemAsync(await ChangeAsync(Guid.NewGuid(), an.Id, "Phân công"), 404, "not-found");

        var toAn = await ReadChangeAsync(await ChangeAsync(first.Id, an.Id, " Bộ môn phân công "));
        Assert.Empty(toAn.Warnings);
        Assert.Equal((an.Id, "Mentor An", "Startup X", "ASSIGNED", "MANUAL", "MANUAL"),
            (toAn.Assignment.MentorId, toAn.Assignment.MentorName, toAn.Assignment.MentorOrganization, toAn.Assignment.Status,
                toAn.Assignment.Reason, toAn.Assignment.Source));

        // An mong muốn 1 nhóm: nhóm thứ hai được nhưng có cảnh báo; nhóm thứ ba vượt tối đa 2 → 409.
        var aboveWanted = await ReadChangeAsync(await ChangeAsync(second.Id, an.Id, "Nhóm cần chuyên môn của An"));
        Assert.Equal(MatchingWarnings.AbovePreferred, Assert.Single(aboveWanted.Warnings).Code);
        await AssertProblemAsync(await ChangeAsync(third.Id, an.Id, "Phân công"), 409, "mentor-full");

        // Thay An bằng Bình ở nhóm B: dòng cũ ENDED, MentorAssigned có replacedMentorId, không phát MentorUnassigned.
        await ReadChangeAsync(await ChangeAsync(second.Id, binh.Id, "Nhóm xin đổi"));
        var rows = await _api.QueryDbAsync(db => db.Set<MentorAssignment>().AsNoTracking().Where(x => x.TeamId == second.Id).OrderBy(x => x.AssignedAt).ToListAsync());
        Assert.Equal(new[] { ("ENDED", an.Id), ("ASSIGNED", binh.Id) }, rows.Select(x => (x.Status, x.MentorId)));
        Assert.NotNull(rows[0].EndedAt);
        Assert.Equal("Nhóm xin đổi", rows[0].EndReason);
        Assert.All(rows, x => Assert.NotNull(x.AssignedByAccountId));

        var events = await _api.EventsAsync(second.Id);
        Assert.DoesNotContain(events, e => e.EventType == "MentorUnassigned");
        var replaced = events.Where(e => e.EventType == "MentorAssigned").Select(Payload).Last();
        Assert.Equal((binh.Id, an.Id), (replaced.GetProperty("mentorId").GetGuid(), replaced.GetProperty("replacedMentorId").GetGuid()));
        Assert.Equal("CLOSED", (await _api.QueryDbAsync(db => db.Set<TopicInterest>().AsNoTracking().SingleAsync(i => i.Id == binhInterest))).Status);

        var audit = await _api.QueryDbAsync(db => db.Set<AuditLogEntry>().AsNoTracking()
            .Where(x => x.TeamId == second.Id && x.Action == MentorAssignmentWriter.AuditAction).OrderBy(x => x.Id).ToListAsync());
        Assert.Equal(new[] { "Nhóm cần chuyên môn của An", "Nhóm xin đổi" }, audit.Select(x => x.Reason));
        Assert.Contains("Mentor An", audit[1].OldValues);

        // An còn 1 nhóm nên nhóm C nhận được An; cùng mentor lần nữa không thêm dòng.
        await ReadChangeAsync(await ChangeAsync(third.Id, an.Id, "Phân công"));
        await ReadChangeAsync(await ChangeAsync(third.Id, an.Id, "Lặp lại"));
        Assert.Equal(1, await _api.QueryDbAsync(db => db.Set<MentorAssignment>().CountAsync(x => x.TeamId == third.Id)));
    }

    [Fact]
    public async Task Admin_removes_mentor_and_history_stays()
    {
        if (!Enabled)
        {
            return;
        }

        var an = Academic.AddMentor("Mentor An", maxTeams: 2);
        var team = await TeamAsync("A Nhóm");
        await ReadChangeAsync(await ChangeAsync(team.Id, an.Id, "Phân công"));

        var removed = await Admin.SendAsync(new HttpRequestMessage(HttpMethod.Delete, $"/api/teams/{team.Id}/mentor-assignment")
        {
            Content = JsonContent.Create(new { reason = "Mentor bận" }),
        });
        Assert.Equal(HttpStatusCode.NoContent, removed.StatusCode);

        var row = await _api.QueryDbAsync(db => db.Set<MentorAssignment>().AsNoTracking().SingleAsync(x => x.TeamId == team.Id));
        Assert.Equal(("ENDED", "Mentor bận"), (row.Status, row.EndReason));
        var unassigned = Payload((await _api.EventsAsync(team.Id)).Single(e => e.EventType == "MentorUnassigned"));
        Assert.Equal((row.Id, an.Id, "Mentor bận"), (unassigned.GetProperty("assignmentId").GetGuid(), unassigned.GetProperty("mentorId").GetGuid(),
            unassigned.GetProperty("reason").GetString()));
        Assert.Single(await _api.QueryDbAsync(db => db.Set<AuditLogEntry>().AsNoTracking()
            .Where(x => x.TeamId == team.Id && x.Action == MentorAssignmentWriter.AuditRemoveAction).ToListAsync()));

        // Gỡ lần nữa (không có body): không còn gì để gỡ, vẫn 204.
        Assert.Equal(HttpStatusCode.NoContent, (await Admin.DeleteAsync($"/api/teams/{team.Id}/mentor-assignment")).StatusCode);
        Assert.Single((await _api.EventsAsync(team.Id)).Where(e => e.EventType == "MentorUnassigned"));
    }

    [Fact]
    public async Task Overview_lists_mentor_rows_interests_and_loads_against_preferred_and_maximum()
    {
        if (!Enabled)
        {
            return;
        }

        Academic.Semester = Academic.Semester with { StartDate = new DateOnly(2026, 9, 7), MentoringStartWeek = 6 };
        var an = Academic.AddMentor("Mentor An", maxTeams: 4, preferredTeams: 2, domains: [1]);
        Academic.AddMentor("Mentor Đã Khóa", maxTeams: 4, active: false);
        var matched = await TeamAsync("A Đã Ghép");
        var waiting = await TeamAsync("B Chờ Ghép");
        await AssignDirectlyAsync(matched.Id, an);
        await InterestAsync(waiting.Id, an);

        var response = await Admin.GetAsync("/api/advisor-matching/MENTOR/overview?courseCode=EXE101");
        Assert.True(response.StatusCode == HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        var overview = (await response.Content.ReadFromJsonAsync<AdvisorMatchingOverviewResponse>())!;

        Assert.Equal(DateTimeOffset.Parse("2026-10-12T00:00:00+07:00"), overview.MatchingAt);
        Assert.Equal((0, 1, 2), (overview.TeamsWithPreferences, overview.TeamsWithoutAdvisor, overview.Items.Count));
        var first = overview.Items[0];
        Assert.Equal(("A Đã Ghép", an.Id, "Mentor An", "MENTOR_INTEREST"), (first.Team.Name, first.AdvisorId, first.AdvisorName, first.Reason));
        var row = overview.Items[1];
        Assert.Null(row.AdvisorId);
        Assert.Empty(row.Preferences);
        Assert.Equal(("MENTOR", an.Id), (Assert.Single(row.Interests).AdvisorKind, row.Interests[0].AdvisorId));
        var load = Assert.Single(overview.AdvisorLoads);
        Assert.Equal(("Mentor An", 1, (int?)2, (int?)4), (load.FullName, load.AssignedCount, load.PreferredTeams, load.MaxTeams));

        var withoutMentor = (await (await Admin.GetAsync("/api/advisor-matching/MENTOR/overview?withoutAdvisor=true")).Content
            .ReadFromJsonAsync<AdvisorMatchingOverviewResponse>())!;
        Assert.Equal(waiting.Id, Assert.Single(withoutMentor.Items).Team.Id);
    }

    [Fact]
    public async Task Academic_outage_returns_503_and_stores_nothing()
    {
        if (!Enabled)
        {
            return;
        }

        Academic.AddMentor("Mentor An", maxTeams: 2);
        await TeamAsync("Nhóm");
        Academic.MentorsDown = true;

        await AssertProblemAsync(await Job.PostAsJsonAsync("/internal/advisor-matching/MENTOR", JobInput()), 503, "academic-unavailable");
        Assert.Empty(await _api.QueryDbAsync(db => db.Set<AdvisorMatchingRun>().AsNoTracking().Where(r => r.SemesterId == Academic.Semester.Id).ToListAsync()));
    }

    public Task InitializeAsync() => Task.CompletedTask;

    public async Task DisposeAsync()
    {
        if (Enabled)
        {
            await TestData.DeleteSemesterAsync(_api, Academic.Semester.Id);
        }
        await _api.DisposeAsync();
    }

    private object JobInput() => new { semesterId = Academic.Semester.Id, courseCode = "EXE101" };

    private Task<TeamDetailResponse> TeamAsync(string name) => TestData.CreateTeamAsync(_api, name, extraMembers: 0);

    private Task<AdvisorMatchingRunResponse> RunJobAsync() => PostRunAsync(Job.PostAsJsonAsync("/internal/advisor-matching/MENTOR", JobInput()));

    private static async Task<AdvisorMatchingRunResponse> PostRunAsync(Task<HttpResponseMessage> call)
    {
        var response = await call;
        Assert.True(response.StatusCode == HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        return (await response.Content.ReadFromJsonAsync<AdvisorMatchingRunResponse>())!;
    }

    private Task<HttpResponseMessage> ChangeAsync(Guid teamId, Guid mentorId, string note) =>
        Admin.PutAsJsonAsync($"/api/teams/{teamId}/mentor-assignment", new { mentorId, note });

    private static async Task<ChangeMentorResponse> ReadChangeAsync(HttpResponseMessage response)
    {
        Assert.True(response.StatusCode == HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        return (await response.Content.ReadFromJsonAsync<ChangeMentorResponse>())!;
    }

    private async Task<Guid> InterestAsync(Guid teamId, MentorSnapshot mentor)
    {
        var id = Guid.NewGuid();
        await _api.QueryDbAsync(db =>
        {
            db.Add(new TopicInterest { Id = id, TeamId = teamId, AdvisorKind = "MENTOR", AdvisorId = mentor.Id, AdvisorName = mentor.FullName });
            return db.SaveChangesAsync();
        });
        await Task.Delay(5);
        return id;
    }

    private Task AssignDirectlyAsync(Guid teamId, MentorSnapshot mentor) => _api.QueryDbAsync(db =>
    {
        db.Add(new MentorAssignment
        {
            Id = Guid.NewGuid(), TeamId = teamId, MentorId = mentor.Id, MentorName = mentor.FullName, Reason = "MENTOR_INTEREST", Source = "AUTO",
            AssignedAt = DateTimeOffset.UtcNow,
        });
        return db.SaveChangesAsync();
    });

    private Task<List<MentorAssignment>> AssignmentsAsync() => _api.QueryDbAsync(db =>
        (from x in db.Set<MentorAssignment>().AsNoTracking()
         join t in db.Set<TeamEntity>() on x.TeamId equals t.Id
         where t.SemesterId == Academic.Semester.Id && x.Status == "ASSIGNED"
         select x).ToListAsync());

    private static (string, Guid) Row(IEnumerable<MentorAssignment> rows, Guid teamId)
    {
        var row = rows.Single(x => x.TeamId == teamId);
        return (row.Reason, row.MentorId);
    }

    private static JsonElement Payload(OutboxMessage message) => JsonDocument.Parse(message.Payload).RootElement;

    private static Task AssertProblemAsync(HttpResponseMessage response, int status, string? code) => TestData.AssertProblemAsync(response, status, code);
}
