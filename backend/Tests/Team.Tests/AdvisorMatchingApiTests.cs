using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Json;
using BuildingBlocks.Auth;
using BuildingBlocks.Outbox;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Team.Repositories.AdvisorMatching;
using Team.Repositories.AuditLog;
using Team.Repositories.LecturerAssignments;
using Team.Repositories.LecturerPreferences;
using Team.Repositories.MentorAssignments;
using Team.Repositories.Teams;
using Team.Repositories.TopicInterests;
using Team.Services.Academic;
using Team.Services.AdvisorAssignments;
using Team.Services.AdvisorMatching;
using Team.Services.Teams;
using Team.Services.TopicInterests;

namespace Team.Tests;

/// <summary>
/// Ghép giảng viên (BE-80, rule 9, 19): Job, Ghép sớm, W19, đổi tay với team_db thật, Academic giả và planner thật của Matching (BE-79).
/// Bật bằng <c>EXE_INTEGRATION=1</c> (chuỗi kết nối qua <c>EXE_TEAM_DB</c>).
/// </summary>
public sealed class AdvisorMatchingApiTests : IAsyncLifetime
{
    private static bool Enabled => Environment.GetEnvironmentVariable("EXE_INTEGRATION") == "1";

    private readonly TeamApiFactory _api = new();

    private FakeAcademic Academic => _api.Academic;

    private HttpClient Admin => _api.ClientFor(null, Roles.Admin);

    private HttpClient Job => _api.ClientFor(null, Roles.Service);

    [Fact]
    public async Task Job_run_applies_every_tier_in_one_run_and_a_second_call_creates_nothing()
    {
        if (!Enabled)
        {
            return;
        }

        var a = Academic.AddLecturer("GV An", maxExe101: 1, domains: [5]);
        var b = Academic.AddLecturer("GV Bình", domains: [7]);
        var c = Academic.AddLecturer("GV Chi", domains: [1]);
        Academic.AddLecturer("GV Đã Nghỉ", domains: [1], active: false);

        var mutual = await TeamAsync("A Hai Bên Chọn");
        var preferred = await TeamAsync("B Nguyện Vọng");
        var interested = await TeamAsync("C Được Quan Tâm");
        var byDomain = await TeamAsync("D Theo Chuyên Môn");
        await PreferencesAsync(mutual.Id, a);
        await PreferencesAsync(preferred.Id, a, b);
        var aOnMutual = await InterestAsync(mutual.Id, a);
        var bOnMutual = await InterestAsync(mutual.Id, b);
        await InterestAsync(interested.Id, b);

        await AssertProblemAsync(await Admin.PostAsJsonAsync("/internal/advisor-matching/LECTURER", JobInput()), 403, null);

        var run = await RunJobAsync();
        Assert.Equal(("LECTURER", "EXE101", "SCHEDULED", "APPLIED"), (run.AdvisorKind, run.CourseCode, run.Trigger, run.Status));
        Assert.Equal((4, 0), (run.MatchedCount, run.UnmatchedCount));
        Assert.NotNull(run.AppliedAt);

        var assigned = await AssignmentsAsync();
        Assert.Equal((a.Id, "MUTUAL", (short?)1), Row(assigned, mutual.Id));
        // A đã đủ 1 nhóm ở bậc Hai bên cùng chọn → nhóm B sang nguyện vọng 2.
        Assert.Equal((b.Id, "PREFERENCE", (short?)2), Row(assigned, preferred.Id));
        Assert.Equal((b.Id, "LECTURER_INTEREST", (short?)null), Row(assigned, interested.Id));
        Assert.Equal((c.Id, "DOMAIN", (short?)null), Row(assigned, byDomain.Id));
        Assert.All(assigned, x => Assert.Equal(("AUTO", run.Id, "ASSIGNED"), (x.Source, x.MatchingRunId, x.Status)));

        var sent = Academic.Lecturers.Count(l => l.IsActive);
        Assert.Equal(sent, _api.Matching.AdvisorRequests.Single().Advisors.Count);

        // Quan tâm cùng loại đóng: người được ghép THIS_ADVISOR_MATCHED, người khác OTHER_ADVISOR_MATCHED.
        var interests = await _api.QueryDbAsync(db => db.Set<TopicInterest>().AsNoTracking().Where(i => i.TeamId == mutual.Id).ToListAsync());
        Assert.Equal(("CLOSED", "THIS_ADVISOR_MATCHED"), Closed(interests, aOnMutual));
        Assert.Equal(("CLOSED", "OTHER_ADVISOR_MATCHED"), Closed(interests, bOnMutual));

        var events = await _api.EventsAsync(mutual.Id);
        var lecturerAssigned = Payload(events.Single(e => e.EventType == "LecturerAssigned"));
        Assert.Equal("exe.assignment", events.Single(e => e.EventType == "LecturerAssigned").Topic);
        Assert.Equal(a.Id, lecturerAssigned.GetProperty("lecturerId").GetGuid());
        Assert.Equal("GV An", lecturerAssigned.GetProperty("lecturerName").GetString());
        Assert.Equal(("MUTUAL", 1, "AUTO"), (lecturerAssigned.GetProperty("reason").GetString(),
            lecturerAssigned.GetProperty("preferenceRank").GetInt32(), lecturerAssigned.GetProperty("source").GetString()));
        Assert.Equal(run.Id, lecturerAssigned.GetProperty("matchingRunId").GetGuid());
        Assert.Equal(new[] { mutual.Leader!.Id }, lecturerAssigned.GetProperty("memberStudentIds").EnumerateArray().Select(x => x.GetGuid()));
        Assert.Equal(2, events.Count(e => e.EventType == "TopicInterestClosed"));

        var completed = Payload((await _api.EventsAsync(run.Id)).Single());
        Assert.Equal((run.Id, "LECTURER", "EXE101", "SCHEDULED", 4, 0), (completed.GetProperty("matchingRunId").GetGuid(),
            completed.GetProperty("advisorKind").GetString(), completed.GetProperty("courseCode").GetString(),
            completed.GetProperty("trigger").GetString(), completed.GetProperty("matchedCount").GetInt32(), completed.GetProperty("unmatchedCount").GetInt32()));

        var audit = await _api.QueryDbAsync(db => db.Set<AuditLogEntry>().AsNoTracking()
            .Where(x => x.TeamId == byDomain.Id && x.Action == LecturerAssignmentWriter.AuditAction).ToListAsync());
        Assert.Equal("System", Assert.Single(audit).ActorRole);

        // Nhóm đã có giảng viên → nguyện vọng khóa (rule 9).
        var locked = await _api.ClientFor(preferred.Leader!.Id)
            .PutAsJsonAsync($"/api/teams/{preferred.Id}/lecturer-preferences", new { lecturerIds = new[] { c.Id } });
        await AssertProblemAsync(locked, 409, "preferences-locked");

        // Job gọi lại (retry): trả đúng lần cũ, không thêm assignment hay sự kiện.
        var eventCount = await EventCountAsync();
        var again = await RunJobAsync();
        Assert.Equal(run.Id, again.Id);
        Assert.Equal(4, (await AssignmentsAsync()).Count);
        Assert.Equal(eventCount, await EventCountAsync());
        Assert.Single(await RunsAsync());
    }

    [Fact]
    public async Task Concurrent_job_calls_create_one_run()
    {
        if (!Enabled)
        {
            return;
        }

        Academic.AddLecturer("GV An", domains: [1]);
        await TeamAsync("Nhóm 1");
        await TeamAsync("Nhóm 2");

        var runs = await Task.WhenAll(Enumerable.Range(0, 4).Select(_ => RunJobAsync()));
        Assert.Single(runs.Select(r => r.Id).Distinct());
        Assert.Single(await RunsAsync());
        Assert.Equal(2, (await AssignmentsAsync()).Count);
    }

    [Fact]
    public async Task Teams_left_unmatched_when_every_lecturer_is_full()
    {
        if (!Enabled)
        {
            return;
        }

        var a = Academic.AddLecturer("GV An", maxExe101: 1, domains: [1]);
        var first = await TeamAsync("A Nhóm");
        var second = await TeamAsync("B Nhóm");
        await PreferencesAsync(first.Id, a);

        var run = await RunJobAsync();
        Assert.Equal((1, 1), (run.MatchedCount, run.UnmatchedCount));
        var left = run.Plan.Single(p => p.TeamId == second.Id);
        Assert.Equal(("B Nhóm", null, null), (left.TeamName, left.AdvisorId, left.Reason));
        Assert.Equal(1, Payload((await _api.EventsAsync(run.Id)).Single()).GetProperty("unmatchedCount").GetInt32());
    }

    [Fact]
    public async Task Exe201_team_goes_back_to_its_exe101_instructor()
    {
        if (!Enabled)
        {
            return;
        }

        var previous = Academic.AddLecturer("GV Kỳ Trước", domains: [9]);
        Academic.AddLecturer("GV Chuyên Môn", domains: [1]);
        var exe101 = await TeamAsync("Nhóm EXE101");
        await AssignDirectlyAsync(exe101.Id, previous);
        var students = Enumerable.Range(0, 4).Select(i => Academic.Enroll(specializationId: i % 2 + 1, course: "EXE201")).ToList();
        var exe201 = await TestData.CreateInheritedTeamAsync(_api, "Nhóm EXE201", students);
        await _api.QueryDbAsync(async db =>
        {
            var team = await db.Set<TeamEntity>().SingleAsync(t => t.Id == exe201);
            team.SourceTeamId = exe101.Id;
            return await db.SaveChangesAsync();
        });

        var run = await RunJobAsync("EXE201");
        Assert.Equal((previous.Id, "PREVIOUS_INSTRUCTOR"), (run.Plan.Single().AdvisorId, run.Plan.Single().Reason));
        Assert.Equal(previous.Id, _api.Matching.AdvisorRequests.Single().Teams.Single().PreviousInstructorId);
    }

    [Fact]
    public async Task Early_matching_previews_without_changes_then_confirm_skips_teams_matched_meanwhile()
    {
        if (!Enabled)
        {
            return;
        }

        var a = Academic.AddLecturer("GV An", domains: [1]);
        var b = Academic.AddLecturer("GV Bình", domains: [2]);
        var first = await TeamAsync("A Nhóm");
        var second = await TeamAsync("B Nhóm");
        await PreferencesAsync(first.Id, a);
        await PreferencesAsync(second.Id, a);

        await AssertProblemAsync(await _api.ClientFor(first.Leader!.Id).PostAsJsonAsync("/api/advisor-matching/LECTURER/preview", new { courseCode = "EXE101" }), 403, null);
        await AssertProblemAsync(await Admin.PostAsJsonAsync("/api/advisor-matching/LECTURER/preview", new { courseCode = "EXE999" }), 400, "validation");

        var preview = await PreviewAsync();
        Assert.Equal(("EARLY", "PREVIEW", 2, 0), (preview.Trigger, preview.Status, preview.MatchedCount, preview.UnmatchedCount));
        Assert.Null(preview.AppliedAt);
        Assert.All(preview.Plan, p => Assert.Equal((a.Id, "GV An", "PREFERENCE", 1), (p.AdvisorId, p.AdvisorName, p.Reason, p.PreferenceRank)));
        Assert.Empty(await AssignmentsAsync());
        Assert.Empty(await _api.EventsAsync(preview.Id));
        var stored = Assert.Single(await RunsAsync());
        Assert.NotNull(stored.RequestedByAccountId);

        // Trong lúc chờ, Admin đổi tay nhóm B sang GV Bình.
        Assert.Equal(HttpStatusCode.OK, (await ChangeInstructorAsync(second.Id, b.Id, "Nhóm xin đổi")).StatusCode);

        var confirmed = await ConfirmAsync(preview.Id);
        Assert.Equal(("APPLIED", 1, 0), (confirmed.Status, confirmed.MatchedCount, confirmed.UnmatchedCount));
        Assert.Equal(first.Id, Assert.Single(confirmed.Plan).TeamId);
        var assigned = await AssignmentsAsync();
        Assert.Equal((a.Id, "PREFERENCE", (short?)1), Row(assigned, first.Id));
        Assert.Equal((b.Id, "MANUAL", (short?)null), Row(assigned, second.Id));
        var completed = Payload((await _api.EventsAsync(preview.Id)).Single(e => e.EventType == "AdvisorMatchingCompleted"));
        Assert.Equal(("EARLY", 1), (completed.GetProperty("trigger").GetString(), completed.GetProperty("matchedCount").GetInt32()));

        await AssertProblemAsync(await Admin.PostAsync($"/api/advisor-matching/runs/{preview.Id}/confirm", null), 409, "run-not-preview");
        await AssertProblemAsync(await Admin.DeleteAsync($"/api/advisor-matching/runs/{preview.Id}"), 409, "run-not-preview");
        await AssertProblemAsync(await Admin.PostAsync($"/api/advisor-matching/runs/{Guid.NewGuid()}/confirm", null), 404, "not-found");
    }

    [Fact]
    public async Task Discarded_preview_cannot_be_confirmed()
    {
        if (!Enabled)
        {
            return;
        }

        Academic.AddLecturer("GV An", domains: [1]);
        var team = await TeamAsync("Nhóm Xem Trước");
        var preview = await PreviewAsync();

        await AssertProblemAsync(await _api.ClientFor(team.Leader!.Id).DeleteAsync($"/api/advisor-matching/runs/{preview.Id}"), 403, null);
        Assert.Equal(HttpStatusCode.NoContent, (await Admin.DeleteAsync($"/api/advisor-matching/runs/{preview.Id}")).StatusCode);
        Assert.Equal("DISCARDED", Assert.Single(await RunsAsync()).Status);
        await AssertProblemAsync(await Admin.PostAsync($"/api/advisor-matching/runs/{preview.Id}/confirm", null), 409, "run-not-preview");
        Assert.Empty(await AssignmentsAsync());
    }

    [Fact]
    public async Task Confirm_leaves_team_unmatched_when_its_lecturer_filled_up_meanwhile()
    {
        if (!Enabled)
        {
            return;
        }

        var a = Academic.AddLecturer("GV An", maxExe101: 1, domains: [1]);
        var first = await TeamAsync("A Nhóm");
        var other = await TeamAsync("B Nhóm");
        await PreferencesAsync(first.Id, a);
        await PreferencesAsync(other.Id, Academic.AddLecturer("GV Bình", maxExe101: 0));
        var preview = await PreviewAsync();
        Assert.Equal(a.Id, preview.Plan.Single(p => p.TeamId == first.Id).AdvisorId);

        // GV An nhận nhóm B bằng tay → đã đủ 1 nhóm.
        Assert.Equal(HttpStatusCode.OK, (await ChangeInstructorAsync(other.Id, a.Id, "Bộ môn phân công")).StatusCode);

        var confirmed = await ConfirmAsync(preview.Id);
        Assert.Equal((0, 1), (confirmed.MatchedCount, confirmed.UnmatchedCount));
        Assert.Null(Assert.Single(confirmed.Plan).AdvisorId);
        Assert.DoesNotContain(await AssignmentsAsync(), x => x.TeamId == first.Id);
    }

    [Fact]
    public async Task Admin_changes_instructor_with_note_capacity_check_and_audit()
    {
        if (!Enabled)
        {
            return;
        }

        var a = Academic.AddLecturer("GV An", maxExe101: 1);
        var b = Academic.AddLecturer("GV Bình");
        var locked = Academic.AddLecturer("GV Đã Khóa", active: false);
        var first = await TeamAsync("A Nhóm");
        var second = await TeamAsync("B Nhóm");
        var bInterest = await InterestAsync(first.Id, b);

        await AssertProblemAsync(await _api.ClientFor(first.Leader!.Id).PutAsJsonAsync($"/api/teams/{first.Id}/instructor", new { lecturerId = a.Id, note = "x" }), 403, null);
        await AssertProblemAsync(await ChangeInstructorAsync(first.Id, a.Id, "  "), 400, "validation");
        await AssertProblemAsync(await ChangeInstructorAsync(first.Id, a.Id, new string('x', 501)), 400, "validation");
        await AssertProblemAsync(await ChangeInstructorAsync(first.Id, Guid.NewGuid(), "Phân công"), 404, "not-found");
        await AssertProblemAsync(await ChangeInstructorAsync(first.Id, locked.Id, "Phân công"), 400, "validation");
        await AssertProblemAsync(await ChangeInstructorAsync(Guid.NewGuid(), a.Id, "Phân công"), 404, "not-found");

        var toA = await ChangeInstructorAsync(first.Id, a.Id, " Bộ môn phân công ");
        Assert.Equal(HttpStatusCode.OK, toA.StatusCode);
        var assignment = (await toA.Content.ReadFromJsonAsync<LecturerAssignmentResponse>())!;
        Assert.Equal((a.Id, "GV An", "ASSIGNED", "MANUAL", "MANUAL", "Bộ môn phân công"),
            (assignment.LecturerId, assignment.LecturerName, assignment.Status, assignment.Reason, assignment.Source, assignment.Note));

        // GV An tối đa 1 nhóm EXE101.
        await AssertProblemAsync(await ChangeInstructorAsync(second.Id, a.Id, "Phân công"), 409, "lecturer-full");

        // Đổi sang GV Bình: dòng cũ REVOKED, sự kiện có previousLecturerId, quan tâm của Bình đóng.
        Assert.Equal(HttpStatusCode.OK, (await ChangeInstructorAsync(first.Id, b.Id, "Nhóm xin đổi")).StatusCode);
        var rows = await _api.QueryDbAsync(db => db.Set<LecturerAssignment>().AsNoTracking().Where(x => x.TeamId == first.Id).OrderBy(x => x.AssignedAt).ToListAsync());
        Assert.Equal(new[] { ("REVOKED", a.Id), ("ASSIGNED", b.Id) }, rows.Select(x => (x.Status, x.LecturerId)));
        Assert.NotNull(rows[0].RevokedAt);
        Assert.All(rows, x => Assert.NotNull(x.AssignedByAccountId));

        var events = (await _api.EventsAsync(first.Id)).Where(e => e.EventType == "LecturerAssigned").Select(Payload).ToList();
        Assert.Equal(2, events.Count);
        Assert.False(events[0].TryGetProperty("previousLecturerId", out _));
        Assert.Equal(a.Id, events[1].GetProperty("previousLecturerId").GetGuid());
        Assert.All(events, e => Assert.Equal(("MANUAL", "MANUAL"), (e.GetProperty("reason").GetString(), e.GetProperty("source").GetString())));
        // Quan tâm của Bình đã đóng khi nhóm được giao cho An (OTHER_ADVISOR_MATCHED), không mở lại.
        var interest = await _api.QueryDbAsync(db => db.Set<TopicInterest>().AsNoTracking().SingleAsync(i => i.Id == bInterest));
        Assert.Equal(("CLOSED", "OTHER_ADVISOR_MATCHED"), (interest.Status, interest.ClosedReason));

        var audit = await _api.QueryDbAsync(db => db.Set<AuditLogEntry>().AsNoTracking()
            .Where(x => x.TeamId == first.Id && x.Action == LecturerAssignmentWriter.AuditAction).OrderBy(x => x.Id).ToListAsync());
        Assert.Equal(new[] { "Bộ môn phân công", "Nhóm xin đổi" }, audit.Select(x => x.Reason));
        Assert.All(audit, x => Assert.Equal("Admin", x.ActorRole));
        Assert.Contains("GV An", audit[1].OldValues);

        // Cùng giảng viên: trả dòng hiện tại, không thêm dòng.
        Assert.Equal(HttpStatusCode.OK, (await ChangeInstructorAsync(first.Id, b.Id, "Lặp lại")).StatusCode);
        Assert.Equal(2, await _api.QueryDbAsync(db => db.Set<LecturerAssignment>().CountAsync(x => x.TeamId == first.Id)));
    }

    [Fact]
    public async Task Admin_cannot_make_the_team_mentor_its_instructor()
    {
        if (!Enabled)
        {
            return;
        }

        var a = Academic.AddLecturer("GV An");
        var b = Academic.AddLecturer("GV Bình");
        var own = await TeamAsync("A Do An Mentor");
        var other = await TeamAsync("B Nhóm Khác");
        await MentorDirectlyAsync(own.Id, Academic.AddMentor("Mentor An", maxTeams: 3, lecturerId: a.Id));

        await AssertProblemAsync(await ChangeInstructorAsync(own.Id, a.Id, "Phân công"), 409, "instructor-is-mentor");
        Assert.Empty(await AssignmentsAsync());
        Assert.DoesNotContain(await _api.EventsAsync(own.Id), e => e.EventType == "LecturerAssigned");

        // Nhóm khác vẫn nhận An; nhóm A nhận giảng viên khác.
        Assert.Equal(HttpStatusCode.OK, (await ChangeInstructorAsync(other.Id, a.Id, "Phân công")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await ChangeInstructorAsync(own.Id, b.Id, "Phân công")).StatusCode);

        // Dữ liệu cũ đã trùng người: chọn lại đúng giảng viên đó cũng bị chặn, không tự gỡ mentor.
        var legacy = await TeamAsync("C Dữ Liệu Cũ");
        await AssignDirectlyAsync(legacy.Id, b);
        await MentorDirectlyAsync(legacy.Id, Academic.AddMentor("Mentor Bình", maxTeams: 3, lecturerId: b.Id));
        await AssertProblemAsync(await ChangeInstructorAsync(legacy.Id, b.Id, "Lặp lại"), 409, "instructor-is-mentor");
        Assert.Equal(2, await _api.QueryDbAsync(db => db.Set<MentorAssignment>().CountAsync(x => x.Status == "ASSIGNED" && (x.TeamId == own.Id || x.TeamId == legacy.Id))));
    }

    [Fact]
    public async Task Change_instructor_is_also_blocked_by_a_locked_mentor_and_allows_external_mentors()
    {
        if (!Enabled)
        {
            return;
        }

        var a = Academic.AddLecturer("GV An");
        var locked = await TeamAsync("A Mentor Đã Khóa");
        var external = await TeamAsync("B Mentor Ngoài");
        await MentorDirectlyAsync(locked.Id, Academic.AddMentor("Mentor An", maxTeams: 3, active: false, lecturerId: a.Id));
        await MentorDirectlyAsync(external.Id, Academic.AddMentor("Mentor Ngoài", maxTeams: 3));

        await AssertProblemAsync(await ChangeInstructorAsync(locked.Id, a.Id, "Phân công"), 409, "instructor-is-mentor");
        Assert.Equal(HttpStatusCode.OK, (await ChangeInstructorAsync(external.Id, a.Id, "Phân công")).StatusCode);
    }

    [Fact]
    public async Task Change_instructor_needs_academic_only_when_the_team_has_a_mentor()
    {
        if (!Enabled)
        {
            return;
        }

        var a = Academic.AddLecturer("GV An");
        var withMentor = await TeamAsync("A Có Mentor");
        var withoutMentor = await TeamAsync("B Chưa Có Mentor");
        await MentorDirectlyAsync(withMentor.Id, Academic.AddMentor("Mentor An", maxTeams: 3, lecturerId: a.Id));

        Academic.MentorsDown = true;
        await AssertProblemAsync(await ChangeInstructorAsync(withMentor.Id, a.Id, "Phân công"), 503, "academic-unavailable");
        Assert.Empty(await AssignmentsAsync());
        Assert.Equal(HttpStatusCode.OK, (await ChangeInstructorAsync(withoutMentor.Id, a.Id, "Phân công")).StatusCode);
    }

    [Fact]
    public async Task Concurrent_instructor_and_mentor_changes_cannot_pick_the_same_person()
    {
        if (!Enabled)
        {
            return;
        }

        var a = Academic.AddLecturer("GV An");
        var mentor = Academic.AddMentor("Mentor An", maxTeams: 20, lecturerId: a.Id);
        for (var i = 0; i < 10; i++)
        {
            var team = await TeamAsync($"Nhóm {i}");
            var results = await Task.WhenAll(
                ChangeInstructorAsync(team.Id, a.Id, "Phân công"),
                Admin.PutAsJsonAsync($"/api/teams/{team.Id}/mentor-assignment", new { mentorId = mentor.Id, note = "Phân công" }));

            Assert.Equal(new[] { HttpStatusCode.OK, HttpStatusCode.Conflict }, results.Select(r => r.StatusCode).Order());
        }
    }

    [Fact]
    public async Task Job_run_replans_team_matched_to_its_own_mentor_with_another_lecturer()
    {
        if (!Enabled)
        {
            return;
        }

        var a = Academic.AddLecturer("GV An", domains: [1]);
        var b = Academic.AddLecturer("GV Bình", domains: [1]);
        var own = await TeamAsync("A Do An Mentor");
        var other = await TeamAsync("B Nhóm Khác");
        await PreferencesAsync(own.Id, a, b);
        await InterestAsync(other.Id, a);
        await MentorDirectlyAsync(own.Id, Academic.AddMentor("Mentor An", maxTeams: 3, lecturerId: a.Id));

        var run = await RunJobAsync();

        Assert.Equal((2, 0), (run.MatchedCount, run.UnmatchedCount));
        var assigned = await AssignmentsAsync();
        // Bình vẫn là nguyện vọng 2 của nhóm A: lần tính lại không làm đổi thứ hạng.
        Assert.Equal((b.Id, "PREFERENCE", (short?)2), Row(assigned, own.Id));
        Assert.Equal((a.Id, "LECTURER_INTEREST", (short?)null), Row(assigned, other.Id));

        Assert.Equal(2, _api.Matching.AdvisorRequests.Count);
        var replan = _api.Matching.AdvisorRequests[1];
        Assert.Equal(own.Id, Assert.Single(replan.Teams).TeamId);
        Assert.Equal(b.Id, Assert.Single(replan.Advisors).AdvisorId);
    }

    [Fact]
    public async Task Job_run_leaves_team_unmatched_when_only_its_mentor_is_left()
    {
        if (!Enabled)
        {
            return;
        }

        var a = Academic.AddLecturer("GV An", domains: [1]);
        var own = await TeamAsync("A Do An Mentor");
        await MentorDirectlyAsync(own.Id, Academic.AddMentor("Mentor An", maxTeams: 3, lecturerId: a.Id));

        var run = await RunJobAsync();

        Assert.Equal((0, 1), (run.MatchedCount, run.UnmatchedCount));
        Assert.Null(Assert.Single(run.Plan).AdvisorId);
        Assert.Empty(await AssignmentsAsync());
    }

    [Fact]
    public async Task Early_matching_skips_lecturer_who_became_the_team_mentor_before_confirm()
    {
        if (!Enabled)
        {
            return;
        }

        var a = Academic.AddLecturer("GV An", domains: [1]);
        var team = await TeamAsync("A Nhóm");

        var preview = await PreviewAsync();
        Assert.Equal(a.Id, Assert.Single(preview.Plan).AdvisorId);

        // Trong lúc chờ xác nhận, An được đổi thành mentor của nhóm.
        await MentorDirectlyAsync(team.Id, Academic.AddMentor("Mentor An", maxTeams: 3, lecturerId: a.Id));
        var confirmed = await ConfirmAsync(preview.Id);

        Assert.Equal(("APPLIED", 0, 1), (confirmed.Status, confirmed.MatchedCount, confirmed.UnmatchedCount));
        Assert.Empty(await AssignmentsAsync());
    }

    [Fact]
    public async Task Failure_in_the_middle_rolls_back_the_whole_run()
    {
        if (!Enabled)
        {
            return;
        }

        var a = Academic.AddLecturer("GV An", domains: [1]);
        var first = await TeamAsync("A Nhóm");
        var second = await TeamAsync("B Nhóm");
        await InterestAsync(first.Id, a);
        await InterestAsync(second.Id, a);

        // Đóng quan tâm của nhóm A lưu được (SaveChanges giữa chừng), tới nhóm B thì lỗi.
        using var failing = _api.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<ITopicInterestService>();
            services.AddScoped<TopicInterestService>();
            services.AddScoped<ITopicInterestService>(sp => new FailingTopicInterests(sp.GetRequiredService<TopicInterestService>(), second.Id));
        }));
        using var job = failing.CreateClient();
        job.DefaultRequestHeaders.Authorization = Job.DefaultRequestHeaders.Authorization;

        await AssertProblemAsync(await job.PostAsJsonAsync("/internal/advisor-matching/LECTURER", JobInput()), 500, null);

        Assert.Empty(await AssignmentsAsync());
        Assert.Empty(await RunsAsync());
        Assert.Empty((await _api.EventsAsync(first.Id)).Where(e => e.Topic == "exe.assignment"));
        var interests = await _api.QueryDbAsync(db => db.Set<TopicInterest>().AsNoTracking().Where(i => i.TeamId == first.Id).ToListAsync());
        Assert.Equal("OPEN", Assert.Single(interests).Status);

        // Chạy lại bình thường sau khi lỗi được sửa.
        Assert.Equal(2, (await RunJobAsync()).MatchedCount);
    }

    [Fact]
    public async Task Dependency_outages_return_problem_details_and_store_nothing()
    {
        if (!Enabled)
        {
            return;
        }

        Academic.AddLecturer("GV An", domains: [1]);
        await TeamAsync("Nhóm");

        _api.Matching.AdvisorMatchingDown = true;
        await AssertProblemAsync(await Job.PostAsJsonAsync("/internal/advisor-matching/LECTURER", JobInput()), 503, "matching-unavailable");
        _api.Matching.AdvisorMatchingDown = false;

        Academic.LecturersDown = true;
        await AssertProblemAsync(await Admin.PostAsJsonAsync("/api/advisor-matching/LECTURER/preview", new { courseCode = "EXE101" }), 503, "academic-unavailable");
        Academic.LecturersDown = false;

        Assert.Empty(await RunsAsync());
        Assert.Empty(await AssignmentsAsync());
    }

    [Fact]
    public async Task Overview_lists_teams_with_advisor_preferences_interests_and_loads()
    {
        if (!Enabled)
        {
            return;
        }

        var a = Academic.AddLecturer("GV An", maxExe101: 3, domains: [1]);
        var b = Academic.AddLecturer("GV Bình", domains: [2]);
        var matched = await TeamAsync("A Đã Ghép");
        var waiting = await TeamAsync("B Chờ Ghép");
        await TeamAsync("C Không Tín Hiệu");
        await PreferencesAsync(waiting.Id, b, a);
        await InterestAsync(waiting.Id, a);
        await AssignDirectlyAsync(matched.Id, a);
        await PreviewAsync();

        await AssertProblemAsync(await _api.ClientFor(waiting.Leader!.Id).GetAsync("/api/advisor-matching/LECTURER/overview"), 403, null);

        var overview = await GetOverviewAsync("courseCode=EXE101");
        Assert.Equal((1, 2, 3), (overview.TeamsWithPreferences, overview.TeamsWithoutAdvisor, overview.Items.Count));
        Assert.Equal((1, 20, 3, 1), (overview.Page, overview.PageSize, overview.TotalCount, overview.TotalPages));
        Assert.Equal("PREVIEW", overview.LastRun!.Status);
        Assert.NotNull(overview.MatchingAt); // hạn đợt lập nhóm EXE101 đang mở của học kỳ
        var first = overview.Items[0];
        Assert.Equal(("A Đã Ghép", a.Id, "GV An", "LECTURER_INTEREST"), (first.Team.Name, first.AdvisorId, first.AdvisorName, first.Reason));
        Assert.Equal("GV An", first.Team.LecturerName);
        var row = overview.Items.Single(i => i.Team.Id == waiting.Id);
        Assert.Null(row.AdvisorId);
        Assert.Equal(new[] { b.Id, a.Id }, row.Preferences.Select(p => p.LecturerId));
        Assert.Equal("GV Bình", row.Preferences[0].FullName);
        Assert.Equal(2, row.Preferences[0].Domains.Single().Id);
        Assert.Equal(a.Id, Assert.Single(row.Interests).AdvisorId);
        Assert.Equal(new[] { ("GV An", 1, (int?)3), ("GV Bình", 0, (int?)null) }, overview.AdvisorLoads.Select(l => (l.FullName, l.AssignedCount, l.MaxTeams)));

        var waitingOnly = await GetOverviewAsync("courseCode=EXE101&withoutAdvisor=true&search=chờ&page=1&pageSize=1");
        Assert.Equal(waiting.Id, Assert.Single(waitingOnly.Items).Team.Id);
        Assert.Equal((1, 1), (waitingOnly.TotalCount, waitingOnly.TotalPages)); // đếm sau lọc withoutAdvisor + search
        var page2 = await GetOverviewAsync("withoutAdvisor=true&page=2&pageSize=1");
        Assert.Equal("C Không Tín Hiệu", Assert.Single(page2.Items).Team.Name);
        Assert.Equal((2, 1, 2, 2), (page2.Page, page2.PageSize, page2.TotalCount, page2.TotalPages)); // đếm sau lọc withoutAdvisor
        var beyond = await GetOverviewAsync("page=5&pageSize=2");
        Assert.Empty(beyond.Items);
        Assert.Equal((3, 2), (beyond.TotalCount, beyond.TotalPages));
        await AssertProblemAsync(await Admin.GetAsync("/api/advisor-matching/LECTURER/overview?pageSize=500"), 400, "validation");
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

    /// <summary>Đóng quan tâm như thật nhưng lỗi ở <paramref name="failTeamId"/> (kiểm tra rollback).</summary>
    private sealed class FailingTopicInterests(ITopicInterestService inner, Guid failTeamId) : ITopicInterestService
    {
        public Task CloseInterestsOnAssignmentAsync(Guid teamId, string advisorKind, Guid matchedAdvisorId, CancellationToken ct) =>
            teamId == failTeamId ? throw new InvalidOperationException("lỗi giả") : inner.CloseInterestsOnAssignmentAsync(teamId, advisorKind, matchedAdvisorId, ct);

        public Task CloseInterestsOnTeamDisbandedAsync(Guid teamId, CancellationToken ct) => inner.CloseInterestsOnTeamDisbandedAsync(teamId, ct);
        public Task<TopicRecommendationsResponse> GetRecommendationsAsync(string advisorKind, string? courseCode, int page, int pageSize, ClaimsPrincipal user, CancellationToken ct) =>
            inner.GetRecommendationsAsync(advisorKind, courseCode, page, pageSize, user, ct);
        public Task<IReadOnlyList<TopicInterestResponse>> GetMyInterestsAsync(string advisorKind, ClaimsPrincipal user, CancellationToken ct) => inner.GetMyInterestsAsync(advisorKind, user, ct);
        public Task<IReadOnlyList<TopicInterestResponse>> GetTeamInterestsAsync(Guid teamId, string? advisorKind, ClaimsPrincipal user, CancellationToken ct) =>
            inner.GetTeamInterestsAsync(teamId, advisorKind, user, ct);
        public Task<TopicInterestResponse> OpenInterestAsync(Guid teamId, OpenTopicInterestInput input, ClaimsPrincipal user, CancellationToken ct) => inner.OpenInterestAsync(teamId, input, user, ct);
        public Task WithdrawInterestAsync(Guid id, ClaimsPrincipal user, CancellationToken ct) => inner.WithdrawInterestAsync(id, user, ct);
    }

    private object JobInput(string courseCode = "EXE101") => new { semesterId = Academic.Semester.Id, courseCode };

    private Task<TeamDetailResponse> TeamAsync(string name) => TestData.CreateTeamAsync(_api, name, extraMembers: 0);

    private async Task<AdvisorMatchingRunResponse> RunJobAsync(string courseCode = "EXE101")
    {
        var response = await Job.PostAsJsonAsync("/internal/advisor-matching/LECTURER", JobInput(courseCode));
        Assert.True(response.StatusCode == HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        return (await response.Content.ReadFromJsonAsync<AdvisorMatchingRunResponse>())!;
    }

    private async Task<AdvisorMatchingRunResponse> PreviewAsync()
    {
        var response = await Admin.PostAsJsonAsync("/api/advisor-matching/LECTURER/preview", new { courseCode = "EXE101" });
        Assert.True(response.StatusCode == HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        return (await response.Content.ReadFromJsonAsync<AdvisorMatchingRunResponse>())!;
    }

    private async Task<AdvisorMatchingRunResponse> ConfirmAsync(Guid runId)
    {
        var response = await Admin.PostAsync($"/api/advisor-matching/runs/{runId}/confirm", null);
        Assert.True(response.StatusCode == HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        return (await response.Content.ReadFromJsonAsync<AdvisorMatchingRunResponse>())!;
    }

    private async Task<AdvisorMatchingOverviewResponse> GetOverviewAsync(string query)
    {
        var response = await Admin.GetAsync("/api/advisor-matching/LECTURER/overview?" + query);
        Assert.True(response.StatusCode == HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        return (await response.Content.ReadFromJsonAsync<AdvisorMatchingOverviewResponse>())!;
    }

    private Task<HttpResponseMessage> ChangeInstructorAsync(Guid teamId, Guid lecturerId, string note) =>
        Admin.PutAsJsonAsync($"/api/teams/{teamId}/instructor", new { lecturerId, note });

    private Task PreferencesAsync(Guid teamId, params LecturerSnapshot[] lecturers) => _api.QueryDbAsync(db =>
    {
        db.AddRange(lecturers.Select((l, i) => new LecturerPreference
        {
            TeamId = teamId, Rank = (short)(i + 1), LecturerId = l.Id, LecturerName = l.FullName, UpdatedByStudentId = Guid.NewGuid(),
            UpdatedAt = DateTimeOffset.UtcNow,
        }));
        return db.SaveChangesAsync();
    });

    private async Task<Guid> InterestAsync(Guid teamId, LecturerSnapshot lecturer)
    {
        var id = Guid.NewGuid();
        await _api.QueryDbAsync(db =>
        {
            db.Add(new TopicInterest { Id = id, TeamId = teamId, AdvisorKind = "LECTURER", AdvisorId = lecturer.Id, AdvisorName = lecturer.FullName });
            return db.SaveChangesAsync();
        });
        await Task.Delay(5); // created_at khác nhau để thứ tự quan tâm ổn định
        return id;
    }

    private Task AssignDirectlyAsync(Guid teamId, LecturerSnapshot lecturer) => _api.QueryDbAsync(db =>
    {
        db.Add(new LecturerAssignment
        {
            Id = Guid.NewGuid(), TeamId = teamId, LecturerId = lecturer.Id, LecturerName = lecturer.FullName, Reason = "LECTURER_INTEREST",
            Source = "AUTO", AssignedAt = DateTimeOffset.UtcNow,
        });
        return db.SaveChangesAsync();
    });

    private Task MentorDirectlyAsync(Guid teamId, MentorSnapshot mentor) => _api.QueryDbAsync(db =>
    {
        db.Add(new MentorAssignment
        {
            Id = Guid.NewGuid(), TeamId = teamId, MentorId = mentor.Id, MentorName = mentor.FullName, Reason = "MENTOR_INTEREST", Source = "AUTO",
            AssignedAt = DateTimeOffset.UtcNow,
        });
        return db.SaveChangesAsync();
    });

    private Task<List<LecturerAssignment>> AssignmentsAsync() => _api.QueryDbAsync(db =>
        (from x in db.Set<LecturerAssignment>().AsNoTracking()
         join t in db.Set<TeamEntity>() on x.TeamId equals t.Id
         where t.SemesterId == Academic.Semester.Id && x.Status == "ASSIGNED"
         select x).ToListAsync());

    private Task<List<AdvisorMatchingRun>> RunsAsync() =>
        _api.QueryDbAsync(db => db.Set<AdvisorMatchingRun>().AsNoTracking().Where(r => r.SemesterId == Academic.Semester.Id).ToListAsync());

    private Task<int> EventCountAsync() => _api.QueryDbAsync(async db =>
    {
        var teamIds = await db.Set<TeamEntity>().Where(t => t.SemesterId == Academic.Semester.Id).Select(t => t.Id).ToListAsync();
        var runIds = await db.Set<AdvisorMatchingRun>().Where(r => r.SemesterId == Academic.Semester.Id).Select(r => r.Id).ToListAsync();
        return await db.Set<OutboxMessage>().CountAsync(m => teamIds.Contains(m.AggregateId) || runIds.Contains(m.AggregateId));
    });

    private static (Guid, string, short?) Row(IEnumerable<LecturerAssignment> rows, Guid teamId)
    {
        var row = rows.Single(x => x.TeamId == teamId);
        return (row.LecturerId, row.Reason, row.PreferenceRank);
    }

    private static (string, string?) Closed(IEnumerable<TopicInterest> interests, Guid id)
    {
        var interest = interests.Single(i => i.Id == id);
        return (interest.Status, interest.ClosedReason);
    }

    private static JsonElement Payload(OutboxMessage message) => JsonDocument.Parse(message.Payload).RootElement;

    private static Task AssertProblemAsync(HttpResponseMessage response, int status, string? code) => TestData.AssertProblemAsync(response, status, code);
}
