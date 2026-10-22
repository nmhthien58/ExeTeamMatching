using System.Net;
using System.Net.Http.Json;

namespace Jobs.Host.Clients;

// Lời gọi nội bộ có token dịch vụ (docs/contracts/openapi, tag Internal). Lỗi HTTP ném HttpRequestException để Hangfire thử lại job.

public static class RoundTypes
{
    public const string TeamFormation = "TEAM_FORMATION";
    public const string ChangeRequest = "CHANGE_REQUEST";
}

/// <summary>Schema <c>Round</c> (academic.yaml).</summary>
public sealed record RoundDto(Guid Id, Guid SemesterId, string CourseCode, string RoundType, DateTimeOffset DeadlineAt, string Status);

public interface IAcademicClient
{
    /// <summary>Đợt đang mở của môn và loại đợt; null nếu không có (404).</summary>
    Task<RoundDto?> GetOpenRoundAsync(string courseCode, string roundType, CancellationToken ct);

    /// <summary>Khóa đợt khi tới hạn; Academic phát RoundClosed qua outbox.</summary>
    Task<RoundDto> CloseRoundAsync(Guid roundId, CancellationToken ct);
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

    public async Task<RoundDto> CloseRoundAsync(Guid roundId, CancellationToken ct)
    {
        using var response = await http.PostAsync($"/internal/registration-rounds/{roundId}/close", null, ct);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<RoundDto>(ct))!;
    }
}
