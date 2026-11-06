using System.Security.Claims;
using Collab.Repositories.Advisor;
using Collab.Repositories.Files;
using Collab.Repositories.Workspaces;
using Collab.Services.Files;
using Collab.Services.Meetings;
using Collab.Services.Messages;
using Collab.Services.Tasks;
using Collab.Services.Workspaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Collab.Services.Advisor;

/// <summary>Schema <c>AdvisorOverview</c> của collab.yaml (L01, M01).</summary>
public sealed record AdvisorOverviewResponse(
    int NeedsReplyCount,
    int OverdueDeliverableCount,
    int UpcomingMeetingCount,
    int PendingScheduleCount,
    IReadOnlyList<AdvisorQuestionResponse> NeedsReply,
    IReadOnlyList<TaskCardResponse> OverdueDeliverables,
    IReadOnlyList<MeetingCardResponse> UpcomingMeetings);

public interface IAdvisorOverviewService
{
    /// <summary>
    /// Việc cần cố vấn xử lý, gom mọi workspace mình là giảng viên hướng dẫn hoặc mentor:
    /// câu hỏi chưa trả lời, bài nộp quá hạn (chỉ giảng viên hướng dẫn), họp đã chốt chưa kết thúc, họp mình tổ chức đang chờ chốt.
    /// Mỗi mảng tối đa <see cref="AdvisorOverviewService.MaxItems"/> phần tử; số đếm là tổng.
    /// </summary>
    Task<AdvisorOverviewResponse> GetAsync(ClaimsPrincipal user, CancellationToken ct);
}

/// <summary>Tổng quan cố vấn (BE-86) – chỉ đọc, tái dùng logic của module Messages và Tasks.</summary>
public sealed class AdvisorOverviewService(
    IAdvisorOverviewRepository overview,
    IWorkspaceRepository workspaces,
    IWorkspaceLookup lookup,
    IWorkspaceAccessService access,
    IMessageService messages,
    IDeliverableService deliverables,
    TimeProvider clock) : IAdvisorOverviewService
{
    public const int MaxItems = 5;

    public async Task<AdvisorOverviewResponse> GetAsync(ClaimsPrincipal user, CancellationToken ct)
    {
        var people = access.PeopleOf(user);
        var advising = (await workspaces.ListForPeopleAsync(people, ct))
            .Where(w => w.Role is WorkspaceRoles.Instructor or WorkspaceRoles.Mentor)
            .Select(w => w.Workspace)
            .DistinctBy(w => w.Id)
            .ToList();
        if (advising.Count == 0)
        {
            return new AdvisorOverviewResponse(0, 0, 0, 0, [], [], []);
        }

        // Cần trả lời: câu hỏi OPEN, chờ lâu nhất trước (giống C02 "Cần trả lời").
        var questions = await messages.ListAdvisorQuestionsAsync(
            new AdvisorQuestionQuery { Status = "OPEN", Page = 1, PageSize = MaxItems }, user, ct);

        // Trễ hạn nộp: chỉ workspace mình là giảng viên hướng dẫn; mentor luôn 0 (rule 23).
        var overdue = await deliverables.ListOverdueForInstructorAsync(user, ct);

        var workspaceIds = advising.Select(w => w.Id).ToList();
        var now = clock.GetUtcNow();
        var scheduled = await overview.ListScheduledNotEndedAsync(workspaceIds, now, ct);
        var pending = await overview.CountProposedByOrganizersAsync(workspaceIds, people, ct);

        var shown = scheduled.Take(MaxItems).ToList();
        var teamNames = advising.ToDictionary(w => w.Id, w => w.TeamName);
        var members = await lookup.ListMembersAsync(shown.Select(r => r.Meeting.WorkspaceId).Distinct().ToList(), ct);
        var meetings = shown.Select(r => ToCard(r, teamNames[r.Meeting.WorkspaceId], new WorkspacePeople(members[r.Meeting.WorkspaceId]))).ToList();

        return new AdvisorOverviewResponse(
            questions.TotalCount,
            overdue.Count,
            scheduled.Count,
            pending,
            questions.Items,
            overdue.Take(MaxItems).ToList(),
            meetings);
    }

    /// <summary>Thẻ họp đã chốt: không còn khung chờ trả lời nên <c>myAnswerPending</c> luôn false.</summary>
    private static MeetingCardResponse ToCard(ScheduledMeetingRow row, string teamName, WorkspacePeople people) => new()
    {
        Id = row.Meeting.Id,
        WorkspaceId = row.Meeting.WorkspaceId,
        TeamName = teamName,
        Title = row.Meeting.Title,
        Status = row.Meeting.Status,
        Organizer = people.Ref(row.Meeting.OrganizerType, row.Meeting.OrganizerId, row.Meeting.OrganizerName),
        IsMentorSession = row.Meeting.IsMentorSession,
        StartsAt = row.ConfirmedSlot.StartsAt,
        SlotCount = row.SlotCount,
        Location = row.Meeting.Location,
        MyAnswerPending = false,
        IsWeekly = row.Meeting.ScheduleBookingId is not null,
        WeekNo = row.Meeting.WeekNo,
    };
}

public static class AdvisorModule
{
    /// <summary>Module Advisor: tổng quan cố vấn L01, M01 (BE-86). Cần module Messages, Tasks.</summary>
    public static IServiceCollection AddAdvisorModule(this IServiceCollection services)
    {
        services.TryAddSingleton(TimeProvider.System);
        services.AddScoped<IAdvisorOverviewRepository, AdvisorOverviewRepository>();
        services.AddScoped<IAdvisorOverviewService, AdvisorOverviewService>();
        return services;
    }
}
