using Collab.Repositories.Meetings;
using Collab.Services.Files;
using Collab.Services.Tasks;
using FluentValidation;

namespace Collab.Services.Meetings;

// Schema của collab.yaml (tag Meetings). JSON camelCase, thời gian UTC.

/// <summary>Schema <c>SlotInput</c>. <see cref="DurationMinutes"/> bỏ trống thì 60 phút.</summary>
public sealed record SlotInput(DateTimeOffset? StartsAt, int? DurationMinutes);

/// <summary>Schema <c>MeetingInput</c>: 1–3 khung giờ; một khung = họp đã chốt luôn.</summary>
public sealed record MeetingInput(string? Title, string? Agenda, string? Location, IReadOnlyList<SlotInput>? Slots);

public sealed record SlotAnswer(Guid SlotId, string? Answer);

/// <summary>Body của <c>PUT /api/meetings/{id}/responses</c>.</summary>
public sealed record MeetingAnswersRequest(IReadOnlyList<SlotAnswer>? Answers);

/// <summary>Body của <c>POST /api/meetings/{id}/confirm</c>.</summary>
public sealed record ConfirmMeetingRequest(Guid SlotId);

/// <summary>Body của <c>POST /api/meetings/{id}/cancel</c>.</summary>
public sealed record CancelMeetingRequest(string? Reason);

/// <summary>Body của <c>PUT /api/meetings/{id}/minutes</c>.</summary>
public sealed record MeetingMinutesRequest(string? Content);

/// <summary>Một dòng biên bản chuyển thành việc (schema <c>TaskInput</c>, hiện dùng tiêu đề – xem <c>ITaskService.CreateFromMeetingAsync</c>).</summary>
public sealed record MinuteTaskItem(string? Title);

/// <summary>Body của <c>POST /api/meetings/{id}/minutes/tasks</c>.</summary>
public sealed record MinuteTasksRequest(IReadOnlyList<MinuteTaskItem>? Items);

/// <summary>Body của <c>POST /internal/meetings/{id}/reminders</c>.</summary>
public sealed record MeetingReminderRequest(int OffsetMinutes);

/// <summary>Schema <c>MeetingCard</c>.</summary>
public record MeetingCardResponse
{
    public required Guid Id { get; init; }
    public required Guid WorkspaceId { get; init; }
    public required string TeamName { get; init; }
    public required string Title { get; init; }
    public required string Status { get; init; }
    public required PersonRef Organizer { get; init; }
    public required bool IsMentorSession { get; init; }
    public DateTimeOffset? StartsAt { get; init; }
    public required int SlotCount { get; init; }
    public string? Location { get; init; }
    public required bool MyAnswerPending { get; init; }
    public required bool IsWeekly { get; init; }
    public int? WeekNo { get; init; }
}

/// <summary>Khung giờ trong schema <c>MeetingDetail</c>.</summary>
public sealed record MeetingSlotResponse(
    Guid Id, DateTimeOffset StartsAt, int DurationMinutes, int YesCount, int InviteeCount, string? MyAnswer, IReadOnlyList<PersonRef> YesPeople);

/// <summary>Biên bản trong schema <c>MeetingDetail</c>.</summary>
public sealed record MeetingMinutesResponse(string Content, string RecordedByName, DateTimeOffset RecordedAt);

/// <summary>Schema <c>MeetingDetail</c>.</summary>
public sealed record MeetingDetailResponse : MeetingCardResponse
{
    public string? Agenda { get; init; }
    public required IReadOnlyList<MeetingSlotResponse> Slots { get; init; }
    public Guid? SuggestedSlotId { get; init; }
    public string? CancelReason { get; init; }
    public MeetingMinutesResponse? Minutes { get; init; }
    public required IReadOnlyList<TaskCardResponse> CreatedTasks { get; init; }
}

/// <summary>Kết quả <c>GET /api/workspaces/{id}/meetings</c>: lịch họp và "Buổi mentor n/m".</summary>
public sealed record MeetingListResponse(int MentorSessionsDone, int ExpectedMentorSessions, IReadOnlyList<MeetingCardResponse> Items);

/// <summary>Dòng của <c>GET /internal/meetings/upcoming</c> cho MeetingReminderJob: đủ trường của payload <c>MeetingReminder</c>.</summary>
public sealed record UpcomingMeetingResponse(
    Guid Id, Guid WorkspaceId, string Title, DateTimeOffset StartsAt, string? Location, IReadOnlyList<MeetingInvitee> Invitees);

// Payload sự kiện exe.collab (docs/contracts/events.md), key = workspaceId.

public static class MeetingEventTypes
{
    public const string MeetingProposed = "MeetingProposed";
    public const string MeetingScheduled = "MeetingScheduled";
    public const string MeetingCancelled = "MeetingCancelled";
}

public sealed record MeetingInvitee(string PersonType, Guid PersonId);

public sealed record MeetingEventSlot(Guid SlotId, DateTimeOffset StartsAt, int DurationMinutes);

public sealed record MeetingEvent(
    Guid WorkspaceId, Guid MeetingId, string Title, bool IsMentorSession, IReadOnlyList<MeetingEventSlot> Slots,
    Guid? ConfirmedSlotId, string? CancelReason, IReadOnlyList<MeetingInvitee> Invitees);

public sealed class SlotInputValidator : AbstractValidator<SlotInput>
{
    public SlotInputValidator()
    {
        RuleFor(x => x.StartsAt).NotNull().WithMessage("Chọn giờ bắt đầu.");
        RuleFor(x => x.DurationMinutes).InclusiveBetween(15, 240).WithMessage("Thời lượng từ 15 đến 240 phút.");
    }
}

public sealed class MeetingInputValidator : AbstractValidator<MeetingInput>
{
    public const int MaxSlots = 3;

    public MeetingInputValidator()
    {
        RuleFor(x => x.Title).NotEmpty().WithMessage("Nhập tiêu đề.").MaximumLength(200).WithMessage("Tiêu đề tối đa 200 ký tự.");
        RuleFor(x => x.Agenda).MaximumLength(2000).WithMessage("Nội dung tối đa 2000 ký tự.");
        RuleFor(x => x.Location).MaximumLength(500).WithMessage("Địa điểm tối đa 500 ký tự.");
        RuleFor(x => x.Slots).Must(s => s is { Count: >= 1 and <= MaxSlots }).WithMessage($"Đề xuất từ 1 đến {MaxSlots} khung giờ.");
        RuleForEach(x => x.Slots).SetValidator(new SlotInputValidator());
    }
}

public sealed class MeetingAnswersRequestValidator : AbstractValidator<MeetingAnswersRequest>
{
    public MeetingAnswersRequestValidator()
    {
        RuleFor(x => x.Answers).NotEmpty().WithMessage("Thiếu câu trả lời.");
        RuleForEach(x => x.Answers).ChildRules(answer =>
            answer.RuleFor(a => a.Answer).Must(a => a is MeetingAnswers.Yes or MeetingAnswers.No).WithMessage("Câu trả lời là YES hoặc NO."));
    }
}

public sealed class CancelMeetingRequestValidator : AbstractValidator<CancelMeetingRequest>
{
    public CancelMeetingRequestValidator()
    {
        RuleFor(x => x.Reason).NotEmpty().WithMessage("Nhập lý do hủy.").MaximumLength(500).WithMessage("Lý do tối đa 500 ký tự.");
    }
}

public sealed class MeetingMinutesRequestValidator : AbstractValidator<MeetingMinutesRequest>
{
    public MeetingMinutesRequestValidator()
    {
        RuleFor(x => x.Content).NotEmpty().WithMessage("Nhập nội dung biên bản.").MaximumLength(8000).WithMessage("Biên bản tối đa 8000 ký tự.");
    }
}

public sealed class MinuteTasksRequestValidator : AbstractValidator<MinuteTasksRequest>
{
    public MinuteTasksRequestValidator()
    {
        RuleFor(x => x.Items).Must(i => i is { Count: >= 1 and <= 20 }).WithMessage("Chuyển từ 1 đến 20 dòng biên bản.");
        RuleForEach(x => x.Items).ChildRules(item =>
            item.RuleFor(i => i.Title).NotEmpty().WithMessage("Nhập tên việc.").MaximumLength(200).WithMessage("Tên việc tối đa 200 ký tự."));
    }
}
