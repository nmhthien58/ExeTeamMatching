using System.Net;
using System.Net.Http.Json;

namespace Jobs.Host.Clients;

// Lời gọi nội bộ có token dịch vụ (docs/contracts/openapi, tag Internal). Lỗi HTTP ném HttpRequestException để Hangfire thử lại job.

public static class RoundTypes
{
    public const string TeamFormation = "TEAM_FORMATION";
    public const string ChangeRequest = "CHANGE_REQUEST";
}

/// <summary>Phần job cần của schema <c>Semester</c> (academic.yaml). <see cref="MentoringStartWeek"/> null = mặc định tuần 6.</summary>
public sealed record SemesterDto(Guid Id, string Code, DateOnly StartDate, DateOnly EndDate, int? MentoringStartWeek);

/// <summary>Schema <c>Round</c> (academic.yaml).</summary>
/// <summary><see cref="ClosedAt"/> trước <see cref="DeadlineAt"/> = Bộ môn kết thúc đợt sớm.</summary>
public sealed record RoundDto(Guid Id, Guid SemesterId, string CourseCode, string RoundType, DateTimeOffset DeadlineAt, string Status, DateTimeOffset? ClosedAt = null);

/// <summary>Schema <c>RuleSettings</c> (phần quy tắc lập nhóm).</summary>
public sealed record TeamRuleSettings(int MinTeamSize, int MaxTeamSize, int MinSpecializations);

public interface IAcademicClient
{
    /// <summary>Đợt đang mở của môn và loại đợt; null nếu không có (404).</summary>
    Task<RoundDto?> GetOpenRoundAsync(string courseCode, string roundType, CancellationToken ct);

    /// <summary>Khóa đợt khi tới hạn; Academic phát RoundClosed qua outbox.</summary>
    Task<RoundDto> CloseRoundAsync(Guid roundId, CancellationToken ct);

    /// <summary>Học kỳ hiện hành (<c>GET /api/semesters/current</c>); null khi Bộ môn chưa đặt (404).</summary>
    Task<SemesterDto?> GetCurrentSemesterAsync(CancellationToken ct);

    /// <summary>Quy tắc lập nhóm ở W05 (<c>GET /api/rule-settings</c>); null khi Academic không trả lời thì job dùng <c>JobsOptions</c>.</summary>
    Task<TeamRuleSettings?> GetTeamRulesAsync(CancellationToken ct) => Task.FromResult<TeamRuleSettings?>(null);
}

public sealed class AcademicClient(HttpClient http) : IAcademicClient
{
    public async Task<RoundDto?> GetOpenRoundAsync(string courseCode, string roundType, CancellationToken ct)
    {
        using var response = await http.GetAsync($"/internal/registration-rounds/open?courseCode={courseCode}&roundType={roundType}", ct);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<RoundDto>(ct);
    }

    public async Task<TeamRuleSettings?> GetTeamRulesAsync(CancellationToken ct)
    {
        try
        {
            return await http.GetFromJsonAsync<TeamRuleSettings>("/api/rule-settings", ct);
        }
        catch (HttpRequestException)
        {
            return null;
        }
    }

    public async Task<SemesterDto?> GetCurrentSemesterAsync(CancellationToken ct)
    {
        using var response = await http.GetAsync("/api/semesters/current", ct);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<SemesterDto>(ct);
    }

    public async Task<RoundDto> CloseRoundAsync(Guid roundId, CancellationToken ct)
    {
        using var response = await http.PostAsync($"/internal/registration-rounds/{roundId}/close", null, ct);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<RoundDto>(ct))!;
    }
}
