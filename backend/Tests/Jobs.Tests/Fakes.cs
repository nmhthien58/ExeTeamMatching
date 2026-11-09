using System.Net;
using BuildingBlocks.Messaging;
using ExeTeamMatching.Protos.Matching.V1;
using Grpc.Core;
using Hangfire;
using Hangfire.Common;
using Hangfire.States;
using Jobs.Host.Clients;

namespace Jobs.Tests;

/// <summary>Đồng hồ cố định.</summary>
public sealed class FixedClock(DateTimeOffset now) : TimeProvider
{
    public override DateTimeOffset GetUtcNow() => now;
}

/// <summary>Kafka giả: giữ lại các message đã phát.</summary>
public sealed class FakePublisher : IEventPublisher
{
    public List<(string Topic, string EventType, Guid Key, object Payload)> Published { get; } = [];

    public Task PublishAsync(string topic, string key, EventEnvelope envelope, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public Task PublishAsync<TPayload>(string topic, string eventType, Guid aggregateId, TPayload payload, CancellationToken cancellationToken = default)
    {
        Published.Add((topic, eventType, aggregateId, payload!));
        return Task.CompletedTask;
    }

    public IEnumerable<T> Of<T>() => Published.Select(p => p.Payload).OfType<T>();
}

/// <summary>Hangfire giả: giữ lại job được tạo kèm trạng thái (Enqueued / Scheduled).</summary>
public sealed class FakeJobClient : IBackgroundJobClient
{
    public List<(Job Job, IState State)> Created { get; } = [];

    public string Create(Job job, IState state)
    {
        Created.Add((job, state));
        return Created.Count.ToString();
    }

    public bool ChangeState(string jobId, IState state, string expectedState) => true;
}

public sealed class FakeAcademic : IAcademicClient
{
    public TeamRuleSettings? Rules { get; set; }

    public Task<TeamRuleSettings?> GetTeamRulesAsync(CancellationToken ct) => Task.FromResult(Rules);

    public Dictionary<string, RoundDto> OpenRounds { get; } = [];
    public List<Guid> Closed { get; } = [];

    public Task<RoundDto?> GetOpenRoundAsync(string courseCode, string roundType, CancellationToken ct) =>
        Task.FromResult(OpenRounds.GetValueOrDefault(courseCode));

    /// <summary>Học kỳ hiện hành; null = Bộ môn chưa đặt.</summary>
    public SemesterDto? CurrentSemester { get; set; }

    public Task<SemesterDto?> GetCurrentSemesterAsync(CancellationToken ct) => Task.FromResult(CurrentSemester);

    public DateTimeOffset? ClosedAt { get; init; }

    public DateTimeOffset? DeadlineAt { get; init; }

    public Task<RoundDto> CloseRoundAsync(Guid roundId, CancellationToken ct)
    {
        Closed.Add(roundId);
        return Task.FromResult(new RoundDto(roundId, Guid.NewGuid(), "EXE101", RoundTypes.TeamFormation, DeadlineAt ?? DateTimeOffset.UtcNow, "CLOSED", ClosedAt));
    }
}

public sealed class FakeTeam : ITeamClient
{
    public DeadlineReminderTargets Targets { get; set; } = new([], []);
    public List<AutoAssignApplyRequest> Applied { get; } = [];
    public List<Guid> Finalized { get; } = [];

    public Task<FinalizeRoundResult> FinalizeRoundAsync(Guid roundId, Guid semesterId, string courseCode, CancellationToken ct)
    {
        Finalized.Add(roundId);
        return Task.FromResult(new FinalizeRoundResult(10, 2));
    }

    public Task ApplyAutoAssignmentAsync(AutoAssignApplyRequest request, CancellationToken ct)
    {
        Applied.Add(request);
        return Task.CompletedTask;
    }

    public Task<ExpireInvitationsResult> ExpireOverdueInvitationsAsync(CancellationToken ct) => Task.FromResult(new ExpireInvitationsResult(1, 0));

    public Task<DeadlineReminderTargets> GetDeadlineReminderTargetsAsync(Guid semesterId, string courseCode, CancellationToken ct) =>
        Task.FromResult(Targets);

    public List<(string Kind, Guid SemesterId, string CourseCode)> AdvisorRuns { get; } = [];

    /// <summary>Lỗi ném ra khi gọi ghép cố vấn (giả lập 503 từ Team).</summary>
    public Exception? AdvisorMatchingError { get; set; }

    /// <summary>Lỗi chỉ của một môn (giả lập Team lỗi khi ghép môn đó).</summary>
    public string? FailingCourse { get; set; }

    public Task<AdvisorMatchingRunResult> RunAdvisorMatchingAsync(string kind, Guid semesterId, string courseCode, CancellationToken ct)
    {
        AdvisorRuns.Add((kind, semesterId, courseCode));
        if (FailingCourse == courseCode)
        {
            return Task.FromException<AdvisorMatchingRunResult>(new HttpRequestException("academic-unavailable", null, HttpStatusCode.ServiceUnavailable));
        }
        return AdvisorMatchingError is { } error
            ? Task.FromException<AdvisorMatchingRunResult>(error)
            : Task.FromResult(new AdvisorMatchingRunResult(Guid.NewGuid(), kind, courseCode, "SCHEDULED", "APPLIED", 8, 1));
    }
}

/// <summary>HTTP giả: ghi lại request, trả response cho trước.</summary>
public sealed class StubHandler(HttpStatusCode status, string body) : HttpMessageHandler
{
    public HttpRequestMessage? Request { get; private set; }
    public string? RequestBody { get; private set; }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        Request = request;
        RequestBody = request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken);
        return new HttpResponseMessage(status) { Content = new StringContent(body, System.Text.Encoding.UTF8, "application/json") };
    }
}

public sealed class FakeCollab : ICollabClient
{
    public Dictionary<int, List<UpcomingMeetingDto>> Upcoming { get; } = [];
    public List<(Guid MeetingId, int Offset)> Marked { get; } = [];
    public List<DueTaskDto> Due { get; } = [];

    public Task<IReadOnlyList<UpcomingMeetingDto>> GetUpcomingMeetingsAsync(int offsetMinutes, CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<UpcomingMeetingDto>>(Upcoming.GetValueOrDefault(offsetMinutes) ?? []);

    public Task MarkReminderSentAsync(Guid meetingId, int offsetMinutes, CancellationToken ct)
    {
        Marked.Add((meetingId, offsetMinutes));
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<DueTaskDto>> GetDueTasksAsync(int withinHours, bool includeOverdue, CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<DueTaskDto>>(Due);

    public int CloseSlotBookingCalls { get; private set; }
    public CloseSlotBookingResult CloseSlotBookingResult { get; set; } = new(0, 0, 0);

    public Task<CloseSlotBookingResult> CloseSlotBookingAsync(CancellationToken ct)
    {
        CloseSlotBookingCalls++;
        return Task.FromResult(CloseSlotBookingResult);
    }
}

/// <summary>Matching gRPC giả – trả phương án cho trước.</summary>
public sealed class FakeMatching(PlanAutoAssignmentResponse plan) : MatchingService.MatchingServiceClient
{
    public PlanAutoAssignmentRequest? LastRequest { get; private set; }

    public override AsyncUnaryCall<PlanAutoAssignmentResponse> PlanAutoAssignmentAsync(PlanAutoAssignmentRequest request, CallOptions options)
    {
        LastRequest = request;
        return new AsyncUnaryCall<PlanAutoAssignmentResponse>(Task.FromResult(plan), Task.FromResult(new Metadata()),
            () => Status.DefaultSuccess, () => [], () => { });
    }
}

/// <summary>Hangfire giả cho job định kỳ: giữ id, job và cron đã đăng ký.</summary>
public sealed class FakeRecurringJobManager : IRecurringJobManager
{
    public Dictionary<string, (Job Job, string Cron, RecurringJobOptions Options)> Jobs { get; } = [];

    public void AddOrUpdate(string recurringJobId, Job job, string cronExpression, RecurringJobOptions options) =>
        Jobs[recurringJobId] = (job, cronExpression, options);

    public void Trigger(string recurringJobId) { }

    public void RemoveIfExists(string recurringJobId) => Jobs.Remove(recurringJobId);
}
