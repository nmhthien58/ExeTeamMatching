using System.Net.Http.Json;
using Team.Services.Academic;

namespace Team.Services.Projects;

public interface IProjectSemesterClient
{
    Task<IReadOnlyList<SemesterDto>> ListAsync(CancellationToken ct);
}

public sealed class ProjectSemesterClient(HttpClient http) : IProjectSemesterClient
{
    public async Task<IReadOnlyList<SemesterDto>> ListAsync(CancellationToken ct) =>
        await http.GetFromJsonAsync<List<SemesterDto>>("/api/semesters", ct) ?? [];
}
