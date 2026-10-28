using System.Net.Http.Json;
using Microsoft.Extensions.Caching.Memory;

namespace Collab.Services.Checkpoints;

public sealed record SemesterCheckpointConfig(DateOnly StartDate, IReadOnlyList<int>? CheckpointTargetWeeks);

public interface IAcademicSemesterClient
{
    Task<SemesterCheckpointConfig?> GetConfigAsync(Guid semesterId, CancellationToken ct);
}

public sealed class AcademicSemesterClient(HttpClient http, IMemoryCache? cache = null) : IAcademicSemesterClient
{
    private sealed record AcademicSemesterItem(Guid Id, DateOnly StartDate, bool IsCurrent, IReadOnlyList<int>? CheckpointTargetWeeks);

    public async Task<SemesterCheckpointConfig?> GetConfigAsync(Guid semesterId, CancellationToken ct)
    {
        try
        {
            if (cache is not null && cache.TryGetValue<SemesterCheckpointConfig>($"academic:semester:{semesterId}", out var cached))
            {
                return cached;
            }

            var semesters = await http.GetFromJsonAsync<List<AcademicSemesterItem>>("/api/semesters", ct);
            if (semesters is not null)
            {
                var matched = semesters.FirstOrDefault(s => s.Id == semesterId) ?? semesters.FirstOrDefault(s => s.IsCurrent);
                if (matched is not null)
                {
                    var config = new SemesterCheckpointConfig(matched.StartDate, matched.CheckpointTargetWeeks);
                    cache?.Set($"academic:semester:{semesterId}", config, TimeSpan.FromMinutes(10));
                    return config;
                }
            }
        }
        catch
        {
            // Fallback gracefully if Academic is unavailable
        }
        return null;
    }
}
