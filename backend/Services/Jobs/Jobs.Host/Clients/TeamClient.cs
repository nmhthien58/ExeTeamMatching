using System.Net.Http.Json;

namespace Jobs.Host.Clients;

// Lời gọi nội bộ có token dịch vụ (docs/contracts/openapi, tag Internal). Lỗi HTTP ném HttpRequestException để Hangfire thử lại job.

public sealed record FinalizeRoundResult(int FinalizedCount, int NeedsReviewCount);

public sealed record ExpireInvitationsResult(int Invitations, int JoinRequests);

public sealed record DeadlineReminderTargets(IReadOnlyList<Guid> StudentIdsWithoutTeam, IReadOnlyList<Guid> TeamIdsUnderMinSize);

/// <summary>Body của <c>POST /internal/teams/auto-assign/apply</c> – phương án của Matching.PlanAutoAssignment.</summary>
public sealed record AutoAssignApplyRequest(Guid SemesterId, string CourseCode, IReadOnlyList<AutoAssignFill> Fills, IReadOnlyList<AutoAssignNewTeam> NewTeams);

public sealed record AutoAssignFill(Guid TeamId, IReadOnlyList<Guid> StudentIds);

public sealed record AutoAssignNewTeam(string Name, Guid LeaderStudentId, IReadOnlyList<Guid> StudentIds);

/// <summary>Loại cố vấn của <c>/internal/advisor-matching/{kind}</c>.</summary>
public static class AdvisorKinds
{
    public const string Lecturer = "LECTURER";
    public const string Mentor = "MENTOR";
}

/// <summary>Phần job cần của schema <c>AdvisorMatchingRun</c> (team.yaml).</summary>
public sealed record AdvisorMatchingRunResult(Guid Id, string AdvisorKind, string CourseCode, string Trigger, string Status, int MatchedCount, int UnmatchedCount);

public interface ITeamClient
{
    /// <summary>Chốt nhóm hợp lệ, còn lại NEEDS_REVIEW (rule 13).</summary>
    Task<FinalizeRoundResult> FinalizeRoundAsync(Guid roundId, Guid semesterId, string courseCode, CancellationToken ct);

    /// <summary>Áp dụng phương án ghép tự động (rule 14).</summary>
    Task ApplyAutoAssignmentAsync(AutoAssignApplyRequest request, CancellationToken ct);

    /// <summary>Lời mời / yêu cầu quá 72 giờ → hết hạn, Team phát InvitationExpired (rule 8).</summary>
    Task<ExpireInvitationsResult> ExpireOverdueInvitationsAsync(CancellationToken ct);

    /// <summary>Sinh viên chưa có nhóm và nhóm dưới 4 người của một đợt.</summary>
    Task<DeadlineReminderTargets> GetDeadlineReminderTargetsAsync(Guid semesterId, string courseCode, CancellationToken ct);

    /// <summary>Ghép cố vấn và áp dụng ngay (trigger SCHEDULED, rule 9 / 20). Gọi lại trả lần đã áp dụng, không tạo trùng.</summary>
    Task<AdvisorMatchingRunResult> RunAdvisorMatchingAsync(string kind, Guid semesterId, string courseCode, CancellationToken ct);
}

public sealed class TeamClient(HttpClient http) : ITeamClient
{
    public async Task<FinalizeRoundResult> FinalizeRoundAsync(Guid roundId, Guid semesterId, string courseCode, CancellationToken ct)
    {
        using var response = await http.PostAsJsonAsync("/internal/teams/finalize-round", new { roundId, semesterId, courseCode }, ct);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<FinalizeRoundResult>(ct))!;
    }

    public async Task ApplyAutoAssignmentAsync(AutoAssignApplyRequest request, CancellationToken ct)
    {
        using var response = await http.PostAsJsonAsync("/internal/teams/auto-assign/apply", request, ct);
        response.EnsureSuccessStatusCode();
    }

    public async Task<ExpireInvitationsResult> ExpireOverdueInvitationsAsync(CancellationToken ct)
    {
        using var response = await http.PostAsync("/internal/invitations/expire-overdue", null, ct);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<ExpireInvitationsResult>(ct))!;
    }

    public async Task<DeadlineReminderTargets> GetDeadlineReminderTargetsAsync(Guid semesterId, string courseCode, CancellationToken ct) =>
        (await http.GetFromJsonAsync<DeadlineReminderTargets>($"/internal/deadline-reminder-targets?semesterId={semesterId}&courseCode={courseCode}", ct))!;

    public async Task<AdvisorMatchingRunResult> RunAdvisorMatchingAsync(string kind, Guid semesterId, string courseCode, CancellationToken ct)
    {
        using var response = await http.PostAsJsonAsync($"/internal/advisor-matching/{kind}", new { semesterId, courseCode }, ct);
        if (!response.IsSuccessStatusCode)
        {
            // Giữ ProblemDetails (vd. 503 matching-unavailable) trong lỗi để thấy lý do trên /hangfire.
            var problem = await response.Content.ReadAsStringAsync(ct);
            throw new HttpRequestException(
                $"Team advisor-matching/{kind} trả {(int)response.StatusCode}: {problem}", null, response.StatusCode);
        }
        return (await response.Content.ReadFromJsonAsync<AdvisorMatchingRunResult>(ct))!;
    }
}
