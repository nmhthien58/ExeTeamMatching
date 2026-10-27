using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.Caching.Memory;

namespace Team.Services.Academic;

// Lời gọi Academic theo academic.yaml (BE-22 của Nam). Endpoint /internal/* dùng token dịch vụ (ServiceTokenHandler).

public static class RoundTypes
{
    public const string TeamFormation = "TEAM_FORMATION";
    public const string ChangeRequest = "CHANGE_REQUEST";
}

/// <summary>Schema <c>Semester</c> (phần Team cần).</summary>
public sealed record SemesterDto(Guid Id, string Code, string Name);

/// <summary>Schema <c>Round</c>.</summary>
public sealed record RoundDto(Guid Id, Guid SemesterId, string CourseCode, string RoundType, DateTimeOffset DeadlineAt, string Status);

/// <summary>Schema <c>StudentSnapshot</c>.</summary>
public sealed record StudentSnapshot(Guid Id, string StudentCode, string FullName, int SpecializationId, string CourseCode, bool IsEnrolled);

/// <summary>Schema <c>CatalogItem</c> (domain, chuyên ngành).</summary>
public sealed record CatalogItem(int Id, string Code, string Name, bool IsActive);

public interface IAcademicClient
{
    /// <summary>Học kỳ hiện hành; null nếu chưa có (404).</summary>
    Task<SemesterDto?> GetCurrentSemesterAsync(CancellationToken ct);

    /// <summary>Đợt đang mở của môn và loại đợt; null nếu không có (404).</summary>
    Task<RoundDto?> GetOpenRoundAsync(string courseCode, string roundType, CancellationToken ct);

    /// <summary>Snapshot MSSV, tên, chuyên ngành; sinh viên không thuộc học kỳ không có trong kết quả hoặc <c>IsEnrolled = false</c>.</summary>
    Task<IReadOnlyList<StudentSnapshot>> LookupStudentsAsync(Guid semesterId, IReadOnlyCollection<Guid> studentIds, CancellationToken ct);

    /// <summary>Đổi danh sách MSSV sang snapshot (import mốc EXE101).</summary>
    Task<IReadOnlyList<StudentSnapshot>> LookupStudentsByCodeAsync(Guid? semesterId, IReadOnlyCollection<string> studentCodes, CancellationToken ct);

    /// <summary>Danh mục domain D01–D18 (cache 10 phút).</summary>
    Task<IReadOnlyList<CatalogItem>> GetDomainsAsync(CancellationToken ct);

    /// <summary>Danh mục chuyên ngành (cache 10 phút).</summary>
    Task<IReadOnlyList<CatalogItem>> GetSpecializationsAsync(CancellationToken ct);
}

public sealed class AcademicClient(HttpClient http, IMemoryCache cache) : IAcademicClient
{
    private static readonly TimeSpan CatalogTtl = TimeSpan.FromMinutes(10);

    public Task<SemesterDto?> GetCurrentSemesterAsync(CancellationToken ct) => GetOrNullAsync<SemesterDto>("/api/semesters/current", ct);

    public Task<RoundDto?> GetOpenRoundAsync(string courseCode, string roundType, CancellationToken ct) =>
        GetOrNullAsync<RoundDto>($"/internal/registration-rounds/open?courseCode={courseCode}&roundType={roundType}", ct);

    public async Task<IReadOnlyList<StudentSnapshot>> LookupStudentsAsync(Guid semesterId, IReadOnlyCollection<Guid> studentIds, CancellationToken ct)
    {
        using var response = await http.PostAsJsonAsync("/internal/students/lookup", new { semesterId, studentIds }, ct);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<List<StudentSnapshot>>(ct) ?? [];
    }

    public async Task<IReadOnlyList<StudentSnapshot>> LookupStudentsByCodeAsync(Guid? semesterId, IReadOnlyCollection<string> studentCodes, CancellationToken ct)
    {
        using var response = await http.PostAsJsonAsync("/internal/students/by-code", new { semesterId, studentCodes }, ct);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<List<StudentSnapshot>>(ct) ?? [];
    }

    public Task<IReadOnlyList<CatalogItem>> GetDomainsAsync(CancellationToken ct) => GetCatalogAsync("/api/domains", ct);

    public Task<IReadOnlyList<CatalogItem>> GetSpecializationsAsync(CancellationToken ct) => GetCatalogAsync("/api/specializations", ct);

    private async Task<IReadOnlyList<CatalogItem>> GetCatalogAsync(string path, CancellationToken ct) =>
        (await cache.GetOrCreateAsync("academic:" + path, async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = CatalogTtl;
            return (IReadOnlyList<CatalogItem>?)await http.GetFromJsonAsync<List<CatalogItem>>(path, ct) ?? [];
        }))!;

    private async Task<T?> GetOrNullAsync<T>(string path, CancellationToken ct) where T : class
    {
        using var response = await http.GetAsync(path, ct);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<T>(ct);
    }
}
