using System.Security.Claims;
using BuildingBlocks.Errors;
using Collab.Repositories.Files;
using Collab.Repositories.Meetings;
using Collab.Repositories.Workspaces;
using Collab.Services.Files;
using Collab.Services.Tasks;
using Collab.Services.Workspaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Collab.Services.Meetings;

public interface IMeetingService
{
    /// <summary>Lịch họp C07, lọc theo trạng thái nếu có; kèm "Buổi mentor n/m".</summary>
    Task<MeetingListResponse> ListAsync(Guid workspaceId, string? status, WorkspaceAccess viewer, CancellationToken ct);

    Task<MeetingDetailResponse> CreateAsync(Guid workspaceId, MeetingInput input, WorkspaceAccess writer, CancellationToken ct);

    Task<MeetingDetailResponse> GetAsync(Guid meetingId, ClaimsPrincipal user, CancellationToken ct);

    Task<MeetingDetailResponse> AnswerAsync(Guid meetingId, MeetingAnswersRequest request, ClaimsPrincipal user, CancellationToken ct);

    Task<MeetingDetailResponse> AddSlotAsync(Guid meetingId, SlotInput input, ClaimsPrincipal user, CancellationToken ct);

    Task<MeetingDetailResponse> ConfirmAsync(Guid meetingId, ConfirmMeetingRequest request, ClaimsPrincipal user, CancellationToken ct);

    Task<MeetingDetailResponse> CancelAsync(Guid meetingId, CancelMeetingRequest request, ClaimsPrincipal user, CancellationToken ct);

    /// <summary>Giảng viên dời một tuần của họp hằng tuần kèm lý do; nhóm được báo.</summary>
    Task<MeetingDetailResponse> RescheduleAsync(Guid meetingId, RescheduleMeetingRequest request, ClaimsPrincipal user, CancellationToken ct);

    Task<MeetingDetailResponse> SaveMinutesAsync(Guid meetingId, MeetingMinutesRequest request, ClaimsPrincipal user, CancellationToken ct);

    Task<IReadOnlyList<TaskCardResponse>> CreateTasksAsync(Guid meetingId, MinuteTasksRequest request, ClaimsPrincipal user, CancellationToken ct);

    /// <summary>MeetingReminderJob: họp đã chốt bắt đầu trong <paramref name="offsetMinutes"/> phút tới mà chưa gửi mốc này.</summary>
    Task<IReadOnlyList<UpcomingMeetingResponse>> ListUpcomingAsync(int offsetMinutes, CancellationToken ct);

    /// <summary>Đánh dấu đã gửi một mốc nhắc; gọi lại không ghi thêm.</summary>
    Task MarkReminderSentAsync(Guid meetingId, int offsetMinutes, CancellationToken ct);
}

/// <summary>
/// Họp đề xuất: buổi mentor và họp phát sinh (BE-68, C07, C08, rule 25). Trưởng nhóm, giảng viên hướng dẫn hoặc mentor đề xuất 1–3 khung,
/// người được mời trả lời Có / Không từng khung, người tổ chức chốt một khung. Họp do mentor tổ chức tính là buổi mentor.
/// Họp hằng tuần EXE201 sinh từ lịch họp hằng tuần (BE-82), dùng chung bảng và chung các màn đọc, hủy, biên bản.
/// PROPOSED → SCHEDULED → DONE (ghi biên bản); PROPOSED hoặc SCHEDULED → CANCELLED. Sự kiện qua outbox cùng transaction.
/// </summary>
public sealed class MeetingService(
    IMeetingRepository meetings,
    IWorkspaceRepository workspaces,
    IWorkspaceLookup lookup,
    IWorkspaceAccessService access,
    ITaskService tasks,
    IMentorSessionTargetClient mentorTarget,
    TimeProvider clock) : IMeetingService
{
    public const short ReminderDay = 1440;
    public const short ReminderHour = 60;

    public async Task<MeetingListResponse> ListAsync(Guid workspaceId, string? status, WorkspaceAccess viewer, CancellationToken ct)
    {
        if (status is not (null or MeetingStatuses.Proposed or MeetingStatuses.Scheduled or MeetingStatuses.Cancelled or MeetingStatuses.Done))
        {
            throw MeetingErrors.Invalid("status", "Trạng thái là PROPOSED, SCHEDULED, CANCELLED hoặc DONE.");
        }
        var workspace = await workspaces.GetByIdAsync(workspaceId, ct) ?? throw new NotFoundException("workspace", workspaceId);
        var rows = await meetings.ListAsync(workspaceId, status, ct);
        var slots = await meetings.ListSlotsAsync(rows.Select(m => m.Id).ToList(), ct);
        var answered = (await meetings.ListResponsesAsync(slots.Select(s => s.Id).ToList(), ct))
            .Where(r => r.PersonType == viewer.PersonType && r.PersonId == viewer.PersonId).Select(r => r.SlotId).ToHashSet();
        var people = new WorkspacePeople(await lookup.ListMembersAsync(workspaceId, ct));
        var bySlot = slots.ToLookup(s => s.MeetingId);

        var cards = rows.Select(m => Card(m, workspace.TeamName, people, bySlot[m.Id].ToList(), viewer, bySlot[m.Id].Any(s => answered.Contains(s.Id))))
            .OrderBy(c => c.Status == MeetingStatuses.Proposed ? 0 : 1)
            .ThenBy(c => c.StartsAt ?? DateTimeOffset.MaxValue)
            .ToList();
        return new MeetingListResponse(
            await meetings.CountDoneMentorSessionsAsync(workspaceId, ct),
            await mentorTarget.GetExpectedSessionsAsync(workspace.SemesterId, ct),
            cards);
    }

    public async Task<MeetingDetailResponse> CreateAsync(Guid workspaceId, MeetingInput input, WorkspaceAccess writer, CancellationToken ct)
    {
        if (!(writer.IsLeader || writer.IsAdvisor))
        {
            throw MeetingErrors.NotOrganizer();
        }
        var inputs = input.Slots!;
        var now = clock.GetUtcNow();
        MeetingRules.RequireFuture(inputs, now);
        if (inputs.Select(s => s.StartsAt!.Value.ToUniversalTime()).Distinct().Count() != inputs.Count)
        {
            throw MeetingErrors.Invalid("slots", "Các khung giờ phải khác nhau.");
        }

        var people = new WorkspacePeople(await lookup.ListMembersAsync(workspaceId, ct));
        var meeting = new Meeting
        {
            Id = Guid.NewGuid(),
            WorkspaceId = workspaceId,
            Title = input.Title!.Trim(),
            Agenda = MeetingRules.Clean(input.Agenda),
            Location = MeetingRules.Clean(input.Location),
            OrganizerType = writer.PersonType,
            OrganizerId = writer.PersonId,
            OrganizerName = people.NameOf(writer.PersonType, writer.PersonId),
            OrganizerRole = writer.Role,
            IsMentorSession = writer.IsMentor,
            Status = MeetingStatuses.Proposed,
            CreatedAt = now,
            UpdatedAt = now,
        };
        var slots = inputs.Select(s => NewSlot(meeting.Id, s)).ToList();
        meetings.Add(meeting, slots);

        var invitees = await InviteesAsync(meeting, ct);
        if (slots.Count == 1)
        {
            await meetings.SaveConfirmedAsync(() =>
            {
                meeting.Status = MeetingStatuses.Scheduled;
                meeting.ConfirmedSlotId = slots[0].Id;
                meetings.AddEvent(MeetingEventTypes.MeetingScheduled, workspaceId, Event(meeting, slots, invitees));
            }, ct);
        }
        else
        {
            meetings.AddEvent(MeetingEventTypes.MeetingProposed, workspaceId, Event(meeting, slots, invitees));
            await meetings.SaveAsync(ct);
        }
        return await DetailAsync(meeting, writer, ct);
    }

    public async Task<MeetingDetailResponse> GetAsync(Guid meetingId, ClaimsPrincipal user, CancellationToken ct)
    {
        var meeting = await FindAsync(meetingId, ct);
        return await DetailAsync(meeting, await access.RequireAsync(meeting.WorkspaceId, user, ct), ct);
    }

    public async Task<MeetingDetailResponse> AnswerAsync(Guid meetingId, MeetingAnswersRequest request, ClaimsPrincipal user, CancellationToken ct)
    {
        var (meeting, viewer) = await WriterAsync(meetingId, user, ct);
        RequireOpen(meeting);
        if (IsOrganizer(meeting, viewer))
        {
            throw new ForbiddenException(title: "Người tổ chức không trả lời đề xuất của mình");
        }

        var slotIds = (await meetings.ListSlotsAsync([meetingId], ct)).Select(s => s.Id).ToHashSet();
        var answers = request.Answers!;
        if (answers.Any(a => !slotIds.Contains(a.SlotId)) || answers.Select(a => a.SlotId).Distinct().Count() != answers.Count)
        {
            throw MeetingErrors.Invalid("answers", "Khung giờ không thuộc cuộc họp này hoặc bị trùng.");
        }

        var now = clock.GetUtcNow();
        var saved = (await meetings.ListResponsesAsync(slotIds, ct))
            .Where(r => r.PersonType == viewer.PersonType && r.PersonId == viewer.PersonId).ToDictionary(r => r.SlotId);
        foreach (var answer in answers)
        {
            if (saved.TryGetValue(answer.SlotId, out var response))
            {
                response.Answer = answer.Answer!;
                response.AnsweredAt = now;
            }
            else
            {
                meetings.Add(new MeetingRespons
                {
                    SlotId = answer.SlotId, PersonType = viewer.PersonType, PersonId = viewer.PersonId, Answer = answer.Answer!, AnsweredAt = now,
                });
            }
        }
        await meetings.SaveAsync(ct);
        return await DetailAsync(meeting, viewer, ct);
    }

    /// <summary>Không khung nào đủ người thì thêm khung; câu trả lời cũ giữ nguyên, người được mời nhận lại <c>MeetingProposed</c>.</summary>
    public async Task<MeetingDetailResponse> AddSlotAsync(Guid meetingId, SlotInput input, ClaimsPrincipal user, CancellationToken ct)
    {
        var (meeting, viewer) = await OrganizerAsync(meetingId, user, ct);
        RequireOpen(meeting);
        MeetingRules.RequireFuture([input], clock.GetUtcNow());
        var slots = (await meetings.ListSlotsAsync([meetingId], ct)).ToList();
        if (slots.Count >= MeetingInputValidator.MaxSlots)
        {
            throw MeetingErrors.SlotLimit();
        }
        if (slots.Any(s => s.StartsAt == input.StartsAt!.Value.ToUniversalTime()))
        {
            throw MeetingErrors.Invalid("startsAt", "Đã có khung giờ này.");
        }

        var slot = NewSlot(meetingId, input);
        meetings.Add(slot);
        slots.Add(slot);
        meetings.AddEvent(MeetingEventTypes.MeetingProposed, meeting.WorkspaceId, Event(meeting, slots, await InviteesAsync(meeting, ct)));
        await meetings.SaveAsync(ct);
        return await DetailAsync(meeting, viewer, ct);
    }

    public async Task<MeetingDetailResponse> ConfirmAsync(Guid meetingId, ConfirmMeetingRequest request, ClaimsPrincipal user, CancellationToken ct)
    {
        var (meeting, viewer) = await OrganizerAsync(meetingId, user, ct);
        RequireOpen(meeting);
        var slots = await meetings.ListSlotsAsync([meetingId], ct);
        var slot = slots.FirstOrDefault(s => s.Id == request.SlotId) ?? throw MeetingErrors.Invalid("slotId", "Khung giờ không thuộc cuộc họp này.");
        if (slot.StartsAt <= clock.GetUtcNow())
        {
            throw MeetingErrors.Invalid("slotId", "Khung giờ này đã qua.");
        }

        meeting.Status = MeetingStatuses.Scheduled;
        meeting.ConfirmedSlotId = slot.Id;
        meetings.AddEvent(MeetingEventTypes.MeetingScheduled, meeting.WorkspaceId, Event(meeting, slots, await InviteesAsync(meeting, ct)));
        await meetings.SaveAsync(ct);
        return await DetailAsync(meeting, viewer, ct);
    }

    /// <remarks>CHECK <c>ck_meetings_confirmed</c>: họp đã hủy không giữ khung đã chốt. Hủy họp hằng tuần chỉ hủy tuần đó.</remarks>
    public async Task<MeetingDetailResponse> CancelAsync(Guid meetingId, CancelMeetingRequest request, ClaimsPrincipal user, CancellationToken ct)
    {
        var (meeting, viewer) = await OrganizerAsync(meetingId, user, ct);
        if (meeting.Status is not (MeetingStatuses.Proposed or MeetingStatuses.Scheduled))
        {
            throw MeetingErrors.NotOpen();
        }

        meeting.Status = MeetingStatuses.Cancelled;
        meeting.ConfirmedSlotId = null;
        meeting.CancelReason = request.Reason!.Trim();
        var slots = await meetings.ListSlotsAsync([meetingId], ct);
        meetings.AddEvent(MeetingEventTypes.MeetingCancelled, meeting.WorkspaceId, Event(meeting, slots, await InviteesAsync(meeting, ct)));
        await meetings.SaveAsync(ct);
        return await DetailAsync(meeting, viewer, ct);
    }

    /// <remarks>Bảng không có cột lý do dời nên lý do đi trong <c>cancelReason</c> của sự kiện. Mốc nhắc cũ được xóa để nhóm được nhắc lại theo giờ mới.</remarks>
    public async Task<MeetingDetailResponse> RescheduleAsync(Guid meetingId, RescheduleMeetingRequest request, ClaimsPrincipal user, CancellationToken ct)
    {
        var (meeting, viewer) = await OrganizerAsync(meetingId, user, ct);
        if (meeting.ScheduleBookingId is null)
        {
            throw MeetingScheduleErrors.NotWeekly();
        }
        if (meeting.Status != MeetingStatuses.Scheduled)
        {
            throw MeetingErrors.NotOpen();
        }
        var startsAt = request.StartsAt!.Value.ToUniversalTime();
        if (startsAt <= clock.GetUtcNow())
        {
            throw MeetingErrors.Invalid("startsAt", "Giờ mới phải ở tương lai.");
        }

        var slot = await meetings.FindSlotAsync(meeting.ConfirmedSlotId!.Value, ct) ?? throw new NotFoundException("khung giờ", meeting.ConfirmedSlotId);
        slot.StartsAt = startsAt;
        meeting.UpdatedAt = clock.GetUtcNow();
        await meetings.RemoveRemindersAsync(meetingId, ct);
        meetings.AddEvent(MeetingEventTypes.MeetingScheduled, meeting.WorkspaceId,
            Event(meeting, [slot], await InviteesAsync(meeting, ct)) with { CancelReason = request.Reason!.Trim() });
        await meetings.SaveAsync(ct);
        return await DetailAsync(meeting, viewer, ct);
    }

    /// <summary>Ghi hoặc sửa biên bản; lần đầu chuyển họp sang DONE (buổi mentor được tính từ đây).</summary>
    public async Task<MeetingDetailResponse> SaveMinutesAsync(Guid meetingId, MeetingMinutesRequest request, ClaimsPrincipal user, CancellationToken ct)
    {
        var (meeting, viewer) = await OrganizerAsync(meetingId, user, ct);
        if (meeting.Status is not (MeetingStatuses.Scheduled or MeetingStatuses.Done))
        {
            throw MeetingErrors.NotConfirmed();
        }

        var now = clock.GetUtcNow();
        var content = request.Content!.Trim();
        if (await meetings.FindMinutesAsync(meetingId, ct) is { } minutes)
        {
            minutes.Content = content;
            minutes.UpdatedAt = now;
        }
        else
        {
            meetings.Add(new MeetingMinutes
            {
                MeetingId = meetingId,
                Content = content,
                RecordedById = viewer.PersonId,
                RecordedByName = meeting.OrganizerName,
                RecordedAt = now,
                UpdatedAt = now,
            });
        }
        meeting.Status = MeetingStatuses.Done;
        await meetings.SaveAsync(ct);
        return await DetailAsync(meeting, viewer, ct);
    }

    public async Task<IReadOnlyList<TaskCardResponse>> CreateTasksAsync(Guid meetingId, MinuteTasksRequest request, ClaimsPrincipal user, CancellationToken ct)
    {
        var (meeting, viewer) = await OrganizerAsync(meetingId, user, ct);
        if (meeting.Status != MeetingStatuses.Done)
        {
            throw MeetingErrors.NotConfirmed();
        }
        return await tasks.CreateFromMeetingAsync(meeting.WorkspaceId, meetingId, request.Items!.Select(i => i.Title!).ToList(), viewer, ct);
    }

    public async Task<IReadOnlyList<UpcomingMeetingResponse>> ListUpcomingAsync(int offsetMinutes, CancellationToken ct)
    {
        var offset = MeetingRules.ReminderOffset(offsetMinutes);
        var now = clock.GetUtcNow();
        var rows = await meetings.ListUpcomingAsync(now, now.AddMinutes(offset), offset, ct);
        var members = await lookup.ListMembersAsync(rows.Select(r => r.Meeting.WorkspaceId).Distinct().ToList(), ct);
        return rows.Select(r => new UpcomingMeetingResponse(r.Meeting.Id, r.Meeting.WorkspaceId, r.Meeting.Title, r.StartsAt, r.Meeting.Location,
            members[r.Meeting.WorkspaceId].Where(m => m.LeftAt is null).Select(m => new MeetingInvitee(m.PersonType, m.PersonId)).ToList())).ToList();
    }

    public async Task MarkReminderSentAsync(Guid meetingId, int offsetMinutes, CancellationToken ct)
    {
        var offset = MeetingRules.ReminderOffset(offsetMinutes);
        _ = await FindAsync(meetingId, ct);
        if (!await meetings.ReminderSentAsync(meetingId, offset, ct))
        {
            meetings.Add(new MeetingReminder { MeetingId = meetingId, OffsetMinutes = offset, SentAt = clock.GetUtcNow() });
            await meetings.SaveAsync(ct);
        }
    }

    private static MeetingSlot NewSlot(Guid meetingId, SlotInput input) => new()
    {
        Id = Guid.NewGuid(),
        MeetingId = meetingId,
        StartsAt = input.StartsAt!.Value.ToUniversalTime(),
        DurationMinutes = (short)(input.DurationMinutes ?? 60),
    };

    private static bool IsOrganizer(Meeting meeting, WorkspaceAccess viewer) =>
        meeting.OrganizerType == viewer.PersonType && meeting.OrganizerId == viewer.PersonId;

    private static void RequireOpen(Meeting meeting)
    {
        if (meeting.Status != MeetingStatuses.Proposed)
        {
            throw MeetingErrors.NotOpen();
        }
    }

    private async Task<Meeting> FindAsync(Guid meetingId, CancellationToken ct) =>
        await meetings.FindAsync(meetingId, ct) ?? throw new NotFoundException("cuộc họp", meetingId);

    /// <summary>Thành viên, cố vấn của workspace; Admin chỉ đọc (rule 21).</summary>
    private async Task<(Meeting Meeting, WorkspaceAccess Viewer)> WriterAsync(Guid meetingId, ClaimsPrincipal user, CancellationToken ct)
    {
        var meeting = await FindAsync(meetingId, ct);
        var viewer = await access.RequireAsync(meeting.WorkspaceId, user, ct);
        WorkspaceMemberHandler.Check(viewer, WorkspaceAccessLevel.Writer);
        return (meeting, viewer);
    }

    private async Task<(Meeting Meeting, WorkspaceAccess Viewer)> OrganizerAsync(Guid meetingId, ClaimsPrincipal user, CancellationToken ct)
    {
        var (meeting, viewer) = await WriterAsync(meetingId, user, ct);
        return IsOrganizer(meeting, viewer) ? (meeting, viewer) : throw MeetingErrors.NotOrganizer();
    }

    /// <summary>Mọi người đang ở workspace trừ người tổ chức.</summary>
    private async Task<IReadOnlyList<MeetingInvitee>> InviteesAsync(Meeting meeting, CancellationToken ct) =>
        (await lookup.ListMembersAsync(meeting.WorkspaceId, ct))
            .Where(m => m.LeftAt is null && !(m.PersonType == meeting.OrganizerType && m.PersonId == meeting.OrganizerId))
            .Select(m => new MeetingInvitee(m.PersonType, m.PersonId)).ToList();

    private static MeetingEvent Event(Meeting m, IEnumerable<MeetingSlot> slots, IReadOnlyList<MeetingInvitee> invitees) => new(
        m.WorkspaceId, m.Id, m.Title, m.IsMentorSession,
        slots.Select(s => new MeetingEventSlot(s.Id, s.StartsAt, s.DurationMinutes)).ToList(), m.ConfirmedSlotId, m.CancelReason, invitees);

    private static MeetingCardResponse Card(Meeting m, string teamName, WorkspacePeople people, IReadOnlyList<MeetingSlot> slots, WorkspaceAccess viewer, bool answered) => new()
    {
        Id = m.Id,
        WorkspaceId = m.WorkspaceId,
        TeamName = teamName,
        Title = m.Title,
        Status = m.Status,
        Organizer = people.Ref(m.OrganizerType, m.OrganizerId, m.OrganizerName),
        IsMentorSession = m.IsMentorSession,
        StartsAt = slots.FirstOrDefault(s => s.Id == m.ConfirmedSlotId)?.StartsAt,
        SlotCount = slots.Count,
        Location = m.Location,
        MyAnswerPending = m.Status == MeetingStatuses.Proposed && !viewer.IsReadOnly && !IsOrganizer(m, viewer) && !answered,
        IsWeekly = m.ScheduleBookingId is not null,
        WeekNo = m.WeekNo,
    };

    private async Task<MeetingDetailResponse> DetailAsync(Meeting meeting, WorkspaceAccess viewer, CancellationToken ct)
    {
        var workspace = await workspaces.GetByIdAsync(meeting.WorkspaceId, ct) ?? throw new NotFoundException("workspace", meeting.WorkspaceId);
        var members = await lookup.ListMembersAsync(meeting.WorkspaceId, ct);
        var people = new WorkspacePeople(members);
        var slots = await meetings.ListSlotsAsync([meeting.Id], ct);
        var responses = (await meetings.ListResponsesAsync(slots.Select(s => s.Id).ToList(), ct)).ToLookup(r => r.SlotId);
        var inviteeCount = members.Count(m => m.LeftAt is null && !(m.PersonType == meeting.OrganizerType && m.PersonId == meeting.OrganizerId));
        var slotViews = slots.Select(s =>
        {
            var yes = responses[s.Id].Where(r => r.Answer == MeetingAnswers.Yes).ToList();
            var mine = responses[s.Id].FirstOrDefault(r => r.PersonType == viewer.PersonType && r.PersonId == viewer.PersonId)?.Answer;
            return new MeetingSlotResponse(s.Id, s.StartsAt, s.DurationMinutes, yes.Count, inviteeCount, mine,
                yes.Select(r => people.Ref(r.PersonType, r.PersonId)).ToList());
        }).ToList();

        var taskIds = (await meetings.ListSourceTaskIdsAsync(meeting.Id, ct)).ToHashSet();
        var createdTasks = taskIds.Count == 0
            ? []
            : (await tasks.ListAsync(meeting.WorkspaceId, new TaskQuery(), viewer, ct)).Where(t => taskIds.Contains(t.Id)).ToList();
        var minutes = await meetings.FindMinutesAsync(meeting.Id, ct);
        var card = Card(meeting, workspace.TeamName, people, slots, viewer, slotViews.Any(s => s.MyAnswer is not null));
        return new MeetingDetailResponse
        {
            Id = card.Id,
            WorkspaceId = card.WorkspaceId,
            TeamName = card.TeamName,
            Title = card.Title,
            Status = card.Status,
            Organizer = card.Organizer,
            IsMentorSession = card.IsMentorSession,
            StartsAt = card.StartsAt,
            SlotCount = card.SlotCount,
            Location = card.Location,
            MyAnswerPending = card.MyAnswerPending,
            IsWeekly = card.IsWeekly,
            WeekNo = card.WeekNo,
            Agenda = meeting.Agenda,
            Slots = slotViews,
            SuggestedSlotId = meeting.Status == MeetingStatuses.Proposed ? MeetingRules.SuggestedSlot(slotViews) : null,
            CancelReason = meeting.CancelReason,
            Minutes = minutes is null ? null : new MeetingMinutesResponse(minutes.Content, minutes.RecordedByName, minutes.RecordedAt),
            CreatedTasks = createdTasks,
        };
    }
}

public static class MeetingRules
{
    public static string? Clean(string? text) => string.IsNullOrWhiteSpace(text) ? null : text.Trim();

    public static void RequireFuture(IEnumerable<SlotInput> slots, DateTimeOffset now)
    {
        if (slots.Any(s => s.StartsAt <= now))
        {
            throw MeetingErrors.Invalid("slots", "Khung giờ phải ở tương lai.");
        }
    }

    /// <summary>Khung nhiều người chọn nhất, bằng nhau thì khung sớm hơn; chưa ai chọn thì null.</summary>
    public static Guid? SuggestedSlot(IReadOnlyList<MeetingSlotResponse> slots) =>
        slots.Where(s => s.YesCount > 0).OrderByDescending(s => s.YesCount).ThenBy(s => s.StartsAt).FirstOrDefault()?.Id;

    public static short ReminderOffset(int offsetMinutes) => offsetMinutes is MeetingService.ReminderDay or MeetingService.ReminderHour
        ? (short)offsetMinutes
        : throw MeetingErrors.Invalid("offsetMinutes", "Mốc nhắc là 60 hoặc 1440 phút.");
}

/// <summary>Lỗi của module Meetings (collab.yaml).</summary>
public static class MeetingErrors
{
    public static ForbiddenException NotOrganizer() => new("not-organizer", "Chỉ người tổ chức làm được việc này");

    public static ConflictException NotOpen() =>
        new("meeting-not-open", "Cuộc họp không còn mở", "Chỉ trả lời, thêm khung, chốt khi họp đang chờ chốt; họp đã hủy hoặc đã xong không sửa được.");

    public static ConflictException NotConfirmed() =>
        new("meeting-not-confirmed", "Cuộc họp chưa chốt", "Chỉ ghi biên bản cho họp đã chốt; chuyển dòng biên bản thành việc sau khi có biên bản.");

    public static ConflictException SlotLimit() =>
        new("meeting-slot-limit", "Đã đủ 3 khung giờ", $"Mỗi cuộc họp tối đa {MeetingInputValidator.MaxSlots} khung giờ.");

    public static RequestValidationException Invalid(string field, string message) =>
        new(new Dictionary<string, string[]> { [field] = [message] });
}

public static class MeetingsModule
{
    /// <summary>Module Meetings: họp đề xuất, biên bản – C07, C08 (BE-68); lịch họp hằng tuần – C09 (BE-82). Cần <see cref="TasksModule.AddTasksModule"/>.</summary>
    public static IServiceCollection AddMeetingsModule(this IServiceCollection services, IConfiguration configuration)
    {
        services.TryAddSingleton(TimeProvider.System);
        services.AddScoped<IMeetingRepository, MeetingRepository>();
        services.AddScoped<IMeetingService, MeetingService>();
        services.AddScoped<IMeetingScheduleRepository, MeetingScheduleRepository>();
        services.AddScoped<IMeetingScheduleService, MeetingScheduleService>();
        services.AddHttpClient<IScheduleSemesterClient, ScheduleSemesterClient>(client =>
        {
            client.BaseAddress = new Uri(configuration["Academic:BaseUrl"] ?? "http://localhost:5002");
            client.Timeout = TimeSpan.FromSeconds(3);
        });
        services.AddHttpClient<IMentorSessionTargetClient, MentorSessionTargetClient>(client =>
        {
            client.BaseAddress = new Uri(configuration["Academic:BaseUrl"] ?? "http://localhost:5002");
            client.Timeout = TimeSpan.FromSeconds(3);
        });
        return services;
    }
}
