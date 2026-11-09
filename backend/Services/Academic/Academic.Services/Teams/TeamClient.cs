using System.Net.Http.Json;
using System.Text.Json;
using BuildingBlocks.Observability;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Academic.Services.Teams;

/// <summary>Gọi Team Service qua REST nội bộ (token dịch vụ). Số nhóm đang mentor thuộc Team (rule 20).</summary>
public interface ITeamClient
{
    /// <summary>
    /// <c>POST /internal/mentors/team-counts</c>: số nhóm đang mentor của từng mentor trong <paramref name="semesterId"/>
    /// (nhóm chưa giải tán). Mentor không có nhóm trả 0. Team lỗi / không trả lời → <see cref="TeamUnavailableException"/>.
    /// </summary>
    Task<IReadOnlyDictionary<Guid, int>> GetMentorTeamCountsAsync(IReadOnlyCollection<Guid> mentorIds, Guid semesterId, CancellationToken ct);

    /// <summary>
    /// <c>GET /internal/students/{id}/team-status</c>: sinh viên đã có nhóm ở học kỳ hiện hành chưa.
    /// Team lỗi / không trả lời → <see cref="TeamUnavailableException"/>.
    /// </summary>
    Task<bool> HasTeamAsync(Guid studentId, CancellationToken ct) => Task.FromResult(false);
}

/// <summary>Team Service không trả lời hoặc trả lỗi.</summary>
public sealed class TeamUnavailableException(string message, Exception? inner = null) : Exception(message, inner);

public sealed class TeamClient(HttpClient http) : ITeamClient
{
    private sealed record MentorTeamCount(Guid MentorId, int CurrentTeamCount);

    private sealed record StudentTeamStatus(Guid StudentId, bool HasTeam);

    public async Task<bool> HasTeamAsync(Guid studentId, CancellationToken ct)
    {
        try
        {
            using var response = await http.GetAsync($"/internal/students/{studentId}/team-status", ct);
            if (!response.IsSuccessStatusCode)
            {
                throw new TeamUnavailableException($"Team trả {(int)response.StatusCode} cho /internal/students/{{id}}/team-status.");
            }
            return (await response.Content.ReadFromJsonAsync<StudentTeamStatus>(ct))?.HasTeam ?? false;
        }
        catch (Exception ex) when (ex is HttpRequestException or JsonException
            || (ex is TaskCanceledException && !ct.IsCancellationRequested))
        {
            throw new TeamUnavailableException("Không gọi được Team Service.", ex);
        }
    }

    public async Task<IReadOnlyDictionary<Guid, int>> GetMentorTeamCountsAsync(IReadOnlyCollection<Guid> mentorIds, Guid semesterId, CancellationToken ct)
    {
        if (mentorIds.Count == 0)
        {
            return new Dictionary<Guid, int>();
        }
        try
        {
            using var response = await http.PostAsJsonAsync("/internal/mentors/team-counts", new { mentorIds = mentorIds.Distinct(), semesterId }, ct);
            if (!response.IsSuccessStatusCode)
            {
                throw new TeamUnavailableException($"Team trả {(int)response.StatusCode} cho /internal/mentors/team-counts.");
            }
            var items = await response.Content.ReadFromJsonAsync<List<MentorTeamCount>>(ct)
                ?? throw new TeamUnavailableException("Team trả danh sách số nhóm rỗng.");
            return items.GroupBy(i => i.MentorId).ToDictionary(g => g.Key, g => g.First().CurrentTeamCount);
        }
        catch (Exception ex) when (ex is HttpRequestException or JsonException
            || (ex is TaskCanceledException && !ct.IsCancellationRequested))
        {
            throw new TeamUnavailableException("Không gọi được Team Service.", ex);
        }
    }
}

public static class TeamClientModule
{
    /// <summary>Client Team Service: <c>Team:BaseUrl</c> (Docker: <c>http://team-api:8080</c>), token dịch vụ, CorrelationId.</summary>
    public static IServiceCollection AddTeamClient(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddSingleton<ServiceTokenProvider>();
        services.AddTransient<ServiceTokenHandler>();
        services.AddHttpClient<ITeamClient, TeamClient>(http =>
            {
                http.BaseAddress = new Uri(configuration["Team:BaseUrl"] ?? "http://localhost:5003");
                http.Timeout = TimeSpan.FromSeconds(5);
            })
            .AddHttpMessageHandler<CorrelationIdHandler>()
            .AddHttpMessageHandler<ServiceTokenHandler>();
        return services;
    }
}
