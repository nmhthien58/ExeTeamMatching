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
    public Dictionary<string, RoundDto> OpenRounds { get; } = [];
    public List<Guid> Closed { get; } = [];

    public Task<RoundDto?> GetOpenRoundAsync(string courseCode, string roundType, CancellationToken ct) =>
        Task.FromResult(OpenRounds.GetValueOrDefault(courseCode));

    public Task<RoundDto> CloseRoundAsync(Guid roundId, CancellationToken ct)
    {
        Closed.Add(roundId);
        return Task.FromResult(new RoundDto(roundId, Guid.NewGuid(), "EXE101", RoundTypes.TeamFormation, DateTimeOffset.UtcNow, "CLOSED"));
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
