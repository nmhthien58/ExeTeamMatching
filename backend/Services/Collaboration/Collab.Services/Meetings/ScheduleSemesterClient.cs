using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace Collab.Services.Meetings;

/// <summary>Phần cài đặt học kỳ cần cho lịch họp hằng tuần.</summary>
public sealed record ScheduleSemester(Guid Id, DateOnly StartDate, DateOnly EndDate, int SlotBookingDeadlineWeek, int MeetingSlotMinutes);

public interface IScheduleSemesterClient
{
    /// <summary>Cài đặt học kỳ; null khi Academic lỗi hoặc không có học kỳ này.</summary>
    Task<ScheduleSemester?> GetAsync(Guid semesterId, CancellationToken ct);
}

/// <summary>
/// Đọc <c>GET /api/semesters</c> của Academic bằng token của người gọi. Với <c>close-booking</c> đó là token dịch vụ của Job,
/// API này chỉ cần đăng nhập nên dùng được.
/// </summary>
public sealed class ScheduleSemesterClient(HttpClient http, IHttpContextAccessor context, ILogger<ScheduleSemesterClient> logger) : IScheduleSemesterClient
{
    public async Task<ScheduleSemester?> GetAsync(Guid semesterId, CancellationToken ct)
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
            var semesters = await response.Content.ReadFromJsonAsync<List<ScheduleSemester>>(ct);
            return semesters?.FirstOrDefault(s => s.Id == semesterId);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "Không đọc được cài đặt học kỳ {SemesterId}", semesterId);
            return null;
        }
    }
}
