using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace Collab.Services.Meetings;

public interface IMentorSessionTargetClient
{
    /// <summary>Số buổi mentor dự kiến của học kỳ ("Buổi mentor n/m"); Academic lỗi thì trả mặc định.</summary>
    Task<int> GetExpectedSessionsAsync(Guid semesterId, CancellationToken ct);
}

/// <summary>
/// Đọc <c>expectedMentorSessions</c> từ <c>GET /api/semesters</c> của Academic bằng token của người đang gọi (API này cần đăng nhập).
/// Con số chỉ để hiển thị nên Academic lỗi không làm hỏng lịch họp.
/// </summary>
public sealed class MentorSessionTargetClient(HttpClient http, IHttpContextAccessor context, ILogger<MentorSessionTargetClient> logger)
    : IMentorSessionTargetClient
{
    /// <summary>Mặc định của cột <c>semesters.expected_mentor_sessions</c>.</summary>
    public const int DefaultSessions = 3;

    private sealed record SemesterRow(Guid Id, int ExpectedMentorSessions);

    public async Task<int> GetExpectedSessionsAsync(Guid semesterId, CancellationToken ct)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, "/api/semesters");
            if (AuthenticationHeaderValue.TryParse(context.HttpContext?.Request.Headers.Authorization, out var bearer))
            {
                request.Headers.Authorization = bearer;
            }
            using var response = await http.SendAsync(request, ct);
            response.EnsureSuccessStatusCode();
            var semesters = await response.Content.ReadFromJsonAsync<List<SemesterRow>>(ct);
            return semesters?.FirstOrDefault(s => s.Id == semesterId)?.ExpectedMentorSessions ?? DefaultSessions;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "Không đọc được số buổi mentor của học kỳ {SemesterId}", semesterId);
            return DefaultSessions;
        }
    }
}
