using Jobs.Host.Clients;

namespace Jobs.Host.Events;

// Payload sự kiện (docs/contracts/events.md). Job publish trực tiếp (không có DB nghiệp vụ nên không dùng Outbox).

/// <summary>exe.registration – Academic phát khi mở đợt; Job lên lịch đóng đợt.</summary>
public sealed record RoundOpened(Guid RoundId, Guid SemesterId, string CourseCode, string RoundType, DateTimeOffset DeadlineAt);

/// <summary>exe.registration, key = roundId.</summary>
public sealed record DeadlineApproaching(
    Guid RoundId,
    Guid SemesterId,
    string CourseCode,
    DateTimeOffset DeadlineAt,
    int HoursLeft,
    IReadOnlyList<Guid> StudentIdsWithoutTeam,
    IReadOnlyList<Guid> TeamIdsUnderMinSize);

/// <summary>exe.reminder, key = workspaceId. <see cref="OffsetMinutes"/>: 1440 | 60.</summary>
public sealed record MeetingReminder(
    Guid WorkspaceId, Guid MeetingId, string Title, DateTimeOffset StartsAt, string? Location, int OffsetMinutes, IReadOnlyList<PersonDto> Invitees);

/// <summary>exe.reminder, key = workspaceId – hạn trong 24 giờ.</summary>
public sealed record TaskDueSoon(Guid WorkspaceId, Guid TaskId, string Title, DateTimeOffset DueAt, IReadOnlyList<Guid> AssigneeStudentIds);

/// <summary>exe.reminder, key = workspaceId – gửi nhóm và giảng viên hướng dẫn.</summary>
public sealed record DeliverableOverdue(
    Guid WorkspaceId, Guid TaskId, string Title, DateTimeOffset DueAt, IReadOnlyList<Guid> MemberStudentIds, Guid? LecturerId);

public static class JobEventTypes
{
    public const string RoundOpened = nameof(Events.RoundOpened);
    public const string DeadlineApproaching = nameof(Events.DeadlineApproaching);
    public const string MeetingReminder = nameof(Events.MeetingReminder);
    public const string TaskDueSoon = nameof(Events.TaskDueSoon);
    public const string DeliverableOverdue = nameof(Events.DeliverableOverdue);
}
