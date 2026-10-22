using System.Net.Http.Json;

namespace Jobs.Host.Clients;

// Lời gọi nội bộ có token dịch vụ (docs/contracts/openapi, tag Internal). Lỗi HTTP ném HttpRequestException để Hangfire thử lại job.

public sealed record PersonDto(string PersonType, Guid PersonId);

/// <summary>
/// Dòng của <c>GET /internal/meetings/upcoming</c> (MeetingDetail). <see cref="Invitees"/> là người nhận nhắc lịch
/// (payload MeetingReminder) – endpoint của module Meetings cần trả trường này.
/// </summary>
public sealed record UpcomingMeetingDto(
    Guid Id, Guid WorkspaceId, string Title, DateTimeOffset? StartsAt, string? Location, IReadOnlyList<PersonDto>? Invitees);

/// <summary>Dòng của <c>GET /internal/tasks/due</c> (TaskCard + người nhận).</summary>
public sealed record DueTaskDto(
    Guid Id,
    Guid WorkspaceId,
    string Kind,
    string Title,
    DateTimeOffset? DueAt,
    bool IsOverdue,
    string? SubmissionStatus,
    IReadOnlyList<Guid>? AssigneeStudentIds,
    IReadOnlyList<Guid>? MemberStudentIds,
    Guid? LecturerId);

public interface ICollabClient
{
    /// <summary>Họp đã chốt bắt đầu trong <paramref name="offsetMinutes"/> phút tới mà chưa gửi mốc nhắc này.</summary>
    Task<IReadOnlyList<UpcomingMeetingDto>> GetUpcomingMeetingsAsync(int offsetMinutes, CancellationToken ct);

    /// <summary>Đánh dấu đã gửi một mốc nhắc (idempotent).</summary>
    Task MarkReminderSentAsync(Guid meetingId, int offsetMinutes, CancellationToken ct);

    Task<IReadOnlyList<DueTaskDto>> GetDueTasksAsync(int withinHours, bool includeOverdue, CancellationToken ct);
}

public sealed class CollabClient(HttpClient http) : ICollabClient
{
    public async Task<IReadOnlyList<UpcomingMeetingDto>> GetUpcomingMeetingsAsync(int offsetMinutes, CancellationToken ct) =>
        await http.GetFromJsonAsync<List<UpcomingMeetingDto>>($"/internal/meetings/upcoming?offsetMinutes={offsetMinutes}", ct) ?? [];

    public async Task MarkReminderSentAsync(Guid meetingId, int offsetMinutes, CancellationToken ct)
    {
        using var response = await http.PostAsJsonAsync($"/internal/meetings/{meetingId}/reminders", new { offsetMinutes }, ct);
        response.EnsureSuccessStatusCode();
    }

    public async Task<IReadOnlyList<DueTaskDto>> GetDueTasksAsync(int withinHours, bool includeOverdue, CancellationToken ct) =>
        await http.GetFromJsonAsync<List<DueTaskDto>>(
            $"/internal/tasks/due?withinHours={withinHours}&includeOverdue={includeOverdue.ToString().ToLowerInvariant()}", ct) ?? [];
}
