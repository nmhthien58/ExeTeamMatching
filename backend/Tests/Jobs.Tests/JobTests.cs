using System.Net;
using System.Text;
using System.Text.Json;
using BuildingBlocks.Auth;
using BuildingBlocks.Messaging;
using ExeTeamMatching.Protos.Matching.V1;
using Hangfire.States;
using Jobs.Host.Auth;
using Jobs.Host.Clients;
using Jobs.Host.Configuration;
using Jobs.Host.Events;
using Jobs.Host.Jobs;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace Jobs.Tests;

/// <summary>BE-51 → BE-55, BE-72, BE-81 (giảng viên, mentor): logic từng job với client, Kafka và Hangfire giả.</summary>
public sealed class JobTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 22, 1, 0, 0, TimeSpan.Zero); // 08:00 giờ Việt Nam
    private static readonly CancellationToken Ct = CancellationToken.None;

    // ---------- DeadlineReminderJob (BE-52) ----------

    [Theory]
    [InlineData(71.5, 72)]
    [InlineData(48.5, 72)]
    [InlineData(47, null)]
    [InlineData(23, 24)]
    [InlineData(0.5, 24)]
    [InlineData(-1, null)]
    [InlineData(100, null)]
    public void Each_milestone_is_hit_once_by_daily_runs(double hoursLeft, int? expected) =>
        Assert.Equal(expected, DeadlineReminderJob.Milestone(TimeSpan.FromHours(hoursLeft)));

    [Fact]
    public async Task Deadline_reminder_publishes_targets_for_open_round_at_milestone()
    {
        var academic = new FakeAcademic();
        var round = new RoundDto(Guid.NewGuid(), Guid.NewGuid(), "EXE101", RoundTypes.TeamFormation, Now.AddHours(20), "OPEN");
        academic.OpenRounds["EXE101"] = round;
        var student = Guid.NewGuid();
        var team = new FakeTeam { Targets = new DeadlineReminderTargets([student], []) };
        var publisher = new FakePublisher();

        await new DeadlineReminderJob(academic, team, publisher, new FixedClock(Now), NullLogger<DeadlineReminderJob>.Instance).RunAsync(Ct);

        var published = Assert.Single(publisher.Published);
        Assert.Equal(Topics.Registration, published.Topic);
        Assert.Equal(round.Id, published.Key);
        var payload = Assert.IsType<DeadlineApproaching>(published.Payload);
        Assert.Equal(24, payload.HoursLeft);
        Assert.Equal([student], payload.StudentIdsWithoutTeam);
    }

    [Fact]
    public async Task Deadline_reminder_skips_when_nobody_needs_it()
    {
        var academic = new FakeAcademic();
        academic.OpenRounds["EXE201"] = new RoundDto(Guid.NewGuid(), Guid.NewGuid(), "EXE201", RoundTypes.TeamFormation, Now.AddHours(60), "OPEN");
        var publisher = new FakePublisher();

        await new DeadlineReminderJob(academic, new FakeTeam(), publisher, new FixedClock(Now), NullLogger<DeadlineReminderJob>.Instance).RunAsync(Ct);

        Assert.Empty(publisher.Published);
    }

    // ---------- RoundAutoCloseJob (BE-53) ----------

    [Fact]
    public async Task Round_opened_schedules_close_at_deadline()
    {
        var jobs = new FakeJobClient();
        var round = new RoundOpened(Guid.NewGuid(), Guid.NewGuid(), "EXE101", RoundTypes.TeamFormation, Now.AddDays(3));
        var envelope = EventEnvelope.Create(JobEventTypes.RoundOpened, round, "academic", null);

        var before = DateTime.UtcNow;
        await new RoundOpenedHandler(jobs, new FixedClock(Now), NullLogger<RoundOpenedHandler>.Instance).HandleAsync(envelope, Ct);

        var (job, state) = Assert.Single(jobs.Created);
        Assert.Equal(nameof(RoundAutoCloseJob.CloseAsync), job.Method.Name);
        Assert.Equal(round.RoundId, job.Args[0]);
        // Hangfire tính giờ chạy = giờ thật + khoảng chờ (3 ngày tính theo đồng hồ của handler).
        var scheduled = Assert.IsType<ScheduledState>(state);
        Assert.InRange(scheduled.EnqueueAt, before.AddDays(3), DateTime.UtcNow.AddDays(3));
    }

    [Fact]
    public async Task Close_then_finalize_then_auto_assign_for_team_formation_only()
    {
        var academic = new FakeAcademic();
        var team = new FakeTeam();
        var jobs = new FakeJobClient();
        var job = new RoundAutoCloseJob(academic, team, jobs, NullLogger<RoundAutoCloseJob>.Instance);
        var roundId = Guid.NewGuid();
        var semesterId = Guid.NewGuid();

        await job.CloseAsync(roundId, semesterId, "EXE201", RoundTypes.ChangeRequest, Ct);
        Assert.Empty(jobs.Created);

        await job.CloseAsync(roundId, semesterId, "EXE101", RoundTypes.TeamFormation, Ct);
        Assert.Equal(nameof(RoundAutoCloseJob.FinalizeAsync), Assert.Single(jobs.Created).Job.Method.Name);

        await job.FinalizeAsync(roundId, semesterId, "EXE101", Ct);
        Assert.Equal([roundId], team.Finalized);
        Assert.Equal([typeof(AutoAssignUnmatchedJob), typeof(LecturerMatchingJob)], jobs.Created[^2..].Select(c => c.Job.Type));
        Assert.Equal([roundId, roundId], academic.Closed);
    }

    // ---------- AutoAssignUnmatchedJob (BE-54) ----------

    [Fact]
    public async Task Auto_assign_applies_matching_plan_to_team()
    {
        var teamId = Guid.NewGuid();
        var studentA = Guid.NewGuid();
        var leader = Guid.NewGuid();
        var plan = new PlanAutoAssignmentResponse();
        plan.Fills.Add(new FillExistingTeam { TeamId = teamId.ToString(), StudentIds = { studentA.ToString() } });
        plan.NewTeams.Add(new NewTeamPlan
        {
            SuggestedName = "Auto-EXE101-01",
            LeaderStudentId = leader.ToString(),
            Members = { new MemberInfo { StudentId = leader.ToString(), SpecializationId = 1 } },
        });
        var matching = new FakeMatching(plan);
        var team = new FakeTeam();
        var semesterId = Guid.NewGuid();

        await new AutoAssignUnmatchedJob(matching, team, new FakeAcademic(), Options.Create(new JobsOptions()), NullLogger<AutoAssignUnmatchedJob>.Instance)
            .RunAsync(semesterId, "EXE201", Ct);

        Assert.Equal(Course.Exe201, matching.LastRequest!.Course);
        Assert.Equal(4, matching.LastRequest.MinSize);
        var applied = Assert.Single(team.Applied);
        Assert.Equal(semesterId, applied.SemesterId);
        Assert.Equal([studentA], Assert.Single(applied.Fills).StudentIds);
        Assert.Equal(leader, Assert.Single(applied.NewTeams).LeaderStudentId);
    }

    [Fact]
    public async Task Auto_assign_uses_w05_rules_when_academic_answers()
    {
        var matching = new FakeMatching(new PlanAutoAssignmentResponse());
        var academic = new FakeAcademic { Rules = new TeamRuleSettings(3, 5, 3) };

        await new AutoAssignUnmatchedJob(matching, new FakeTeam(), academic, Options.Create(new JobsOptions()), NullLogger<AutoAssignUnmatchedJob>.Instance)
            .RunAsync(Guid.NewGuid(), "EXE101", Ct);

        Assert.Equal((3, 5, 3), (matching.LastRequest!.MinSize, matching.LastRequest.MaxSize, matching.LastRequest.MinSpecializations));
    }

    [Fact]
    public async Task Empty_plan_is_not_applied()
    {
        var team = new FakeTeam();

        await new AutoAssignUnmatchedJob(new FakeMatching(new PlanAutoAssignmentResponse()), team, new FakeAcademic(), Options.Create(new JobsOptions()),
            NullLogger<AutoAssignUnmatchedJob>.Instance).RunAsync(Guid.NewGuid(), "EXE101", Ct);

        Assert.Empty(team.Applied);
    }

    // ---------- LecturerMatchingJob (BE-81) ----------

    [Theory]
    [InlineData("EXE101")]
    [InlineData("EXE201")]
    public async Task Lecturer_matching_runs_as_continuation_of_auto_assign(string courseCode)
    {
        var jobs = new FakeJobClient();
        var job = new RoundAutoCloseJob(new FakeAcademic(), new FakeTeam(), jobs, NullLogger<RoundAutoCloseJob>.Instance);
        var semesterId = Guid.NewGuid();

        await job.FinalizeAsync(Guid.NewGuid(), semesterId, courseCode, Ct);

        Assert.Equal(2, jobs.Created.Count);
        var (autoAssign, autoAssignState) = jobs.Created[0];
        Assert.Equal(typeof(AutoAssignUnmatchedJob), autoAssign.Type);
        Assert.IsType<EnqueuedState>(autoAssignState);

        var (lecturer, lecturerState) = jobs.Created[1];
        Assert.Equal(typeof(LecturerMatchingJob), lecturer.Type);
        Assert.Equal([semesterId, courseCode, CancellationToken.None], lecturer.Args);
        // Chờ job ghép sinh viên (id "1") thành công mới chạy.
        var awaiting = Assert.IsType<AwaitingState>(lecturerState);
        Assert.Equal("1", awaiting.ParentId);
        Assert.Equal(Hangfire.JobContinuationOptions.OnlyOnSucceededState, awaiting.Options);
    }

    [Theory]
    [InlineData("EXE101")]
    [InlineData("EXE201")]
    public async Task Change_request_round_never_runs_lecturer_matching(string courseCode)
    {
        var jobs = new FakeJobClient();
        var team = new FakeTeam();

        await new RoundAutoCloseJob(new FakeAcademic(), team, jobs, NullLogger<RoundAutoCloseJob>.Instance)
            .CloseAsync(Guid.NewGuid(), Guid.NewGuid(), courseCode, RoundTypes.ChangeRequest, Ct);

        Assert.DoesNotContain(jobs.Created, c => c.Job.Type == typeof(LecturerMatchingJob));
        Assert.Empty(team.Finalized);
        Assert.Empty(team.AdvisorRuns);
    }

    [Fact]
    public async Task Lecturer_matching_calls_team_for_lecturers_of_the_course()
    {
        var team = new FakeTeam();
        var semesterId = Guid.NewGuid();

        await new LecturerMatchingJob(team, NullLogger<LecturerMatchingJob>.Instance).RunAsync(semesterId, "EXE201", Ct);

        Assert.Equal([(AdvisorKinds.Lecturer, semesterId, "EXE201")], team.AdvisorRuns);
    }

    [Fact]
    public async Task Lecturer_matching_rethrows_team_errors_so_hangfire_retries()
    {
        var team = new FakeTeam { AdvisorMatchingError = new HttpRequestException("matching-unavailable", null, HttpStatusCode.ServiceUnavailable) };

        var error = await Assert.ThrowsAsync<HttpRequestException>(() =>
            new LecturerMatchingJob(team, NullLogger<LecturerMatchingJob>.Instance).RunAsync(Guid.NewGuid(), "EXE101", Ct));

        Assert.Equal(HttpStatusCode.ServiceUnavailable, error.StatusCode);
    }

    [Fact]
    public async Task Team_client_posts_semester_and_course_to_internal_advisor_matching()
    {
        var runId = Guid.NewGuid();
        var handler = new StubHandler(HttpStatusCode.OK,
            $$"""{"id":"{{runId}}","advisorKind":"LECTURER","courseCode":"EXE101","trigger":"SCHEDULED","status":"APPLIED","matchedCount":12,"unmatchedCount":2,"createdAt":"2026-10-22T01:00:00Z","appliedAt":"2026-10-22T01:00:00Z","plan":[]}""");
        var client = new TeamClient(new HttpClient(handler) { BaseAddress = new Uri("http://team") });
        var semesterId = Guid.NewGuid();

        var run = await client.RunAdvisorMatchingAsync(AdvisorKinds.Lecturer, semesterId, "EXE101", Ct);

        Assert.Equal(HttpMethod.Post, handler.Request!.Method);
        Assert.Equal("/internal/advisor-matching/LECTURER", handler.Request.RequestUri!.AbsolutePath);
        using var body = JsonDocument.Parse(handler.RequestBody!);
        Assert.Equal(["semesterId", "courseCode"], body.RootElement.EnumerateObject().Select(p => p.Name));
        Assert.Equal(semesterId, body.RootElement.GetProperty("semesterId").GetGuid());
        Assert.Equal("EXE101", body.RootElement.GetProperty("courseCode").GetString());
        Assert.Equal((runId, 12, 2), (run.Id, run.MatchedCount, run.UnmatchedCount));
    }

    [Theory]
    [InlineData("matching-unavailable")]
    [InlineData("academic-unavailable")]
    public async Task Team_client_throws_on_service_unavailable(string code)
    {
        var handler = new StubHandler(HttpStatusCode.ServiceUnavailable, $$"""{"status":503,"code":"{{code}}"}""");
        var client = new TeamClient(new HttpClient(handler) { BaseAddress = new Uri("http://team") });

        var error = await Assert.ThrowsAsync<HttpRequestException>(() =>
            client.RunAdvisorMatchingAsync(AdvisorKinds.Lecturer, Guid.NewGuid(), "EXE201", Ct));

        Assert.Equal(HttpStatusCode.ServiceUnavailable, error.StatusCode);
        Assert.Contains(code, error.Message);
    }

    // ---------- MentorMatchingJob (BE-81) ----------

    /// <summary>Học kỳ bắt đầu thứ Hai 07/09/2026; tuần 6 bắt đầu 12/10/2026 00:00 giờ Việt Nam = 11/10 17:00 UTC.</summary>
    private static SemesterDto Fa26(int? mentoringStartWeek = 6) =>
        new(Guid.NewGuid(), "FA26", new DateOnly(2026, 9, 7), new DateOnly(2026, 12, 26), mentoringStartWeek);

    private static MentorMatchingJob MentorJob(FakeAcademic academic, FakeTeam team, DateTimeOffset now) =>
        new(academic, team, Options.Create(new JobsOptions()), new FixedClock(now), NullLogger<MentorMatchingJob>.Instance);

    [Theory]
    [InlineData(6, "2026-10-12T00:00:00+07:00")]
    [InlineData(null, "2026-10-12T00:00:00+07:00")]
    [InlineData(4, "2026-09-28T00:00:00+07:00")]
    public void Mentoring_starts_at_midnight_of_the_start_week_in_the_job_time_zone(int? week, string expected) =>
        Assert.Equal(DateTimeOffset.Parse(expected), MentorMatchingJob.StartsAt(Fa26(week), new JobsOptions().ResolveTimeZone()));

    [Fact]
    public async Task Mentor_matching_waits_until_the_mentoring_start_week()
    {
        var academic = new FakeAcademic { CurrentSemester = Fa26() };
        var team = new FakeTeam();

        await MentorJob(academic, team, DateTimeOffset.Parse("2026-10-11T16:59:00Z")).CheckAsync(Ct);

        Assert.Empty(team.AdvisorRuns);
    }

    [Fact]
    public async Task Mentor_matching_runs_for_both_courses_from_the_start_week()
    {
        var semester = Fa26();
        var academic = new FakeAcademic { CurrentSemester = semester };
        var team = new FakeTeam();

        await MentorJob(academic, team, DateTimeOffset.Parse("2026-10-12T00:00:00Z")).CheckAsync(Ct);

        Assert.Equal([(AdvisorKinds.Mentor, semester.Id, "EXE101"), (AdvisorKinds.Mentor, semester.Id, "EXE201")], team.AdvisorRuns);
    }

    [Fact]
    public async Task Mentor_matching_reads_the_start_week_again_each_run()
    {
        // Admin dời tuần bắt đầu mentor sang tuần 8 sau khi job đã được lên lịch: tuần 6 không ghép.
        var academic = new FakeAcademic { CurrentSemester = Fa26(8) };
        var team = new FakeTeam();

        await MentorJob(academic, team, DateTimeOffset.Parse("2026-10-13T00:00:00Z")).CheckAsync(Ct);

        Assert.Empty(team.AdvisorRuns);
    }

    [Fact]
    public async Task Mentor_matching_does_nothing_without_current_semester_or_after_it_ended()
    {
        var team = new FakeTeam();

        await MentorJob(new FakeAcademic(), team, Now).CheckAsync(Ct);
        await MentorJob(new FakeAcademic { CurrentSemester = Fa26() }, team, DateTimeOffset.Parse("2026-12-27T01:00:00Z")).CheckAsync(Ct);

        Assert.Empty(team.AdvisorRuns);
    }

    [Fact]
    public async Task Mentor_matching_error_in_one_course_still_runs_the_other_and_rethrows_for_retry()
    {
        var academic = new FakeAcademic { CurrentSemester = Fa26() };
        var team = new FakeTeam { FailingCourse = "EXE101" };

        var error = await Assert.ThrowsAsync<HttpRequestException>(() =>
            MentorJob(academic, team, DateTimeOffset.Parse("2026-10-20T01:00:00Z")).CheckAsync(Ct));

        Assert.Equal(HttpStatusCode.ServiceUnavailable, error.StatusCode);
        Assert.Equal(["EXE101", "EXE201"], team.AdvisorRuns.Select(r => r.CourseCode));
    }

    [Fact]
    public async Task Mentor_matching_run_calls_team_for_mentors_of_the_course()
    {
        var team = new FakeTeam();
        var semesterId = Guid.NewGuid();

        await MentorJob(new FakeAcademic(), team, Now).RunAsync(semesterId, "EXE201", Ct);

        Assert.Equal([(AdvisorKinds.Mentor, semesterId, "EXE201")], team.AdvisorRuns);
    }

    [Fact]
    public void Mentor_matching_is_registered_as_a_recurring_job_with_a_fixed_id()
    {
        var manager = new FakeRecurringJobManager();
        var options = new JobsOptions();

        RecurringJobs.Register(manager, options);

        var (job, cron, recurring) = manager.Jobs[MentorMatchingJob.RecurringId];
        Assert.Equal((typeof(MentorMatchingJob), nameof(MentorMatchingJob.CheckAsync)), (job.Type, job.Method.Name));
        Assert.Equal(options.MentorMatchingCron, cron);
        Assert.Equal(options.ResolveTimeZone(), recurring.TimeZone);
    }

    [Fact]
    public async Task Academic_client_reads_current_semester_and_treats_404_as_none()
    {
        var handler = new StubHandler(HttpStatusCode.OK,
            """{"id":"5b0c3a52-6a0e-4e4a-9a5c-1d9f8b4f0e11","code":"FA26","name":"Fall 2026","startDate":"2026-09-07","endDate":"2026-12-26","isCurrent":true,"mentoringStartWeek":6,"expectedMentorSessions":3}""");
        var semester = await new AcademicClient(new HttpClient(handler) { BaseAddress = new Uri("http://academic") }).GetCurrentSemesterAsync(Ct);

        Assert.Equal("/api/semesters/current", handler.Request!.RequestUri!.AbsolutePath);
        Assert.Equal(("FA26", new DateOnly(2026, 9, 7), 6), (semester!.Code, semester.StartDate, semester.MentoringStartWeek));
        Assert.Null(await new AcademicClient(new HttpClient(new StubHandler(HttpStatusCode.NotFound, "{}")) { BaseAddress = new Uri("http://academic") })
            .GetCurrentSemesterAsync(Ct));
    }

    // ---------- MeetingReminderJob, TaskDueReminderJob (BE-72) ----------

    [Fact]
    public async Task Meeting_reminder_publishes_each_offset_then_marks_it_sent()
    {
        var collab = new FakeCollab();
        var meeting = new UpcomingMeetingDto(Guid.NewGuid(), Guid.NewGuid(), "Họp mentor", Now.AddMinutes(50), "Meet",
            [new PersonDto("MENTOR", Guid.NewGuid())]);
        collab.Upcoming[60] = [meeting, meeting with { Id = Guid.NewGuid(), StartsAt = null }];
        var publisher = new FakePublisher();

        await new MeetingReminderJob(collab, publisher, new FixedClock(Now), NullLogger<MeetingReminderJob>.Instance).RunAsync(Ct);

        var reminder = Assert.Single(publisher.Of<MeetingReminder>());
        Assert.Equal(60, reminder.OffsetMinutes);
        Assert.Single(reminder.Invitees);
        Assert.Equal([(meeting.Id, 60)], collab.Marked);
        Assert.Equal(Topics.Reminder, publisher.Published[0].Topic);
        Assert.Equal(meeting.WorkspaceId, publisher.Published[0].Key);
    }

    [Fact]
    public async Task Meeting_confirmed_inside_the_one_hour_window_gets_only_the_one_hour_reminder()
    {
        // Collaboration trả họp 45 phút nữa cho cả mốc 24 giờ lẫn 1 giờ (chưa gửi mốc nào).
        var collab = new FakeCollab();
        var soon = new UpcomingMeetingDto(Guid.NewGuid(), Guid.NewGuid(), "Chốt MVP", Now.AddMinutes(45), null, [new PersonDto("STUDENT", Guid.NewGuid())]);
        var tomorrow = soon with { Id = Guid.NewGuid(), StartsAt = Now.AddHours(20) };
        collab.Upcoming[1440] = [soon, tomorrow];
        collab.Upcoming[60] = [soon];
        var publisher = new FakePublisher();

        await new MeetingReminderJob(collab, publisher, new FixedClock(Now), NullLogger<MeetingReminderJob>.Instance).RunAsync(Ct);

        Assert.Equal([(tomorrow.Id, 1440), (soon.Id, 60)], publisher.Of<MeetingReminder>().Select(r => (r.MeetingId, r.OffsetMinutes)));
        Assert.Equal([(soon.Id, 1440), (tomorrow.Id, 1440), (soon.Id, 60)], collab.Marked);
    }

    [Fact]
    public async Task Task_due_reminder_routes_due_soon_and_first_day_overdue()
    {
        var assignee = Guid.NewGuid();
        var members = new[] { assignee, Guid.NewGuid() };
        var lecturer = Guid.NewGuid();
        DueTaskDto Task(string kind, double hours, string? submission, IReadOnlyList<Guid>? assignees = null) =>
            new(Guid.NewGuid(), Guid.NewGuid(), kind, $"{kind} {hours}", Now.AddHours(hours), hours < 0, submission, assignees ?? [], members, lecturer);

        var collab = new FakeCollab();
        var dueTask = Task("TASK", 5, null, [assignee]);
        var nobodyAssigned = Task("TASK", 5, null);
        var dueDeliverable = Task("DELIVERABLE", 10, "NOT_SUBMITTED");
        var submittedDeliverable = Task("DELIVERABLE", 10, "PENDING");
        var lateToday = Task("DELIVERABLE", -3, "NOT_SUBMITTED");
        var lateLongAgo = Task("DELIVERABLE", -30, "NOT_SUBMITTED");
        var overdueTask = Task("TASK", -2, null, [assignee]);
        collab.Due.AddRange([dueTask, nobodyAssigned, dueDeliverable, submittedDeliverable, lateToday, lateLongAgo, overdueTask]);
        var publisher = new FakePublisher();

        await new TaskDueReminderJob(collab, publisher, new FixedClock(Now), NullLogger<TaskDueReminderJob>.Instance).RunAsync(Ct);

        var dueSoon = publisher.Of<TaskDueSoon>().ToList();
        Assert.Equal([dueTask.Id, dueDeliverable.Id], dueSoon.Select(d => d.TaskId));
        Assert.Equal([assignee], dueSoon[0].AssigneeStudentIds);
        Assert.Equal(members, dueSoon[1].AssigneeStudentIds);
        var overdue = Assert.Single(publisher.Of<DeliverableOverdue>());
        Assert.Equal(lateToday.Id, overdue.TaskId);
        Assert.Equal(lecturer, overdue.LecturerId);
    }

    // ---------- Token dịch vụ (BE-51) ----------

    [Fact]
    public async Task Service_token_is_valid_for_internal_policy_and_cached()
    {
        var jwt = new JwtOptions { SigningKey = "test-only-signing-key-0123456789abcdef-xyz" };
        var provider = new ServiceTokenProvider(Options.Create(jwt), new FixedClock(DateTimeOffset.UtcNow));

        var token = provider.GetToken();
        var result = await new JsonWebTokenHandler().ValidateTokenAsync(token, new TokenValidationParameters
        {
            ValidIssuer = jwt.Issuer,
            ValidAudience = jwt.Audience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.SigningKey)),
        });

        Assert.True(result.IsValid);
        Assert.Contains(Roles.Service, result.ClaimsIdentity.FindAll(ClaimNames.Roles).Select(c => c.Value));
        Assert.Same(token, provider.GetToken());
    }

    [Fact]
    public void Time_zone_falls_back_to_utc_when_unknown() =>
        Assert.Equal(TimeZoneInfo.Utc, new JobsOptions { TimeZone = "Không/Có" }.ResolveTimeZone());

    [Fact]
    public async Task Slot_booking_close_job_calls_collaboration()
    {
        var collab = new FakeCollab { CloseSlotBookingResult = new CloseSlotBookingResult(2, 1, 20) };
        await new SlotBookingCloseJob(collab, NullLogger<SlotBookingCloseJob>.Instance).RunAsync(CancellationToken.None);
        Assert.Equal(1, collab.CloseSlotBookingCalls);
    }
}
