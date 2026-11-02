using System.Text;
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

/// <summary>BE-51 → BE-55, BE-72: logic từng job với client, Kafka và Hangfire giả.</summary>
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
        Assert.Equal(typeof(AutoAssignUnmatchedJob), jobs.Created[^1].Job.Type);
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

        await new AutoAssignUnmatchedJob(matching, team, Options.Create(new JobsOptions()), NullLogger<AutoAssignUnmatchedJob>.Instance)
            .RunAsync(semesterId, "EXE201", Ct);

        Assert.Equal(Course.Exe201, matching.LastRequest!.Course);
        Assert.Equal(4, matching.LastRequest.MinSize);
        var applied = Assert.Single(team.Applied);
        Assert.Equal(semesterId, applied.SemesterId);
        Assert.Equal([studentA], Assert.Single(applied.Fills).StudentIds);
        Assert.Equal(leader, Assert.Single(applied.NewTeams).LeaderStudentId);
    }

    [Fact]
    public async Task Empty_plan_is_not_applied()
    {
        var team = new FakeTeam();

        await new AutoAssignUnmatchedJob(new FakeMatching(new PlanAutoAssignmentResponse()), team, Options.Create(new JobsOptions()),
            NullLogger<AutoAssignUnmatchedJob>.Instance).RunAsync(Guid.NewGuid(), "EXE101", Ct);

        Assert.Empty(team.Applied);
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
}
