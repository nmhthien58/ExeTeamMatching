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
public sealed record SemesterDto(
    Guid Id,
    string Code,
    string Name,
    DateOnly? StartDate = null,
    int? MentoringStartWeek = null);

/// <summary>Schema <c>Round</c>.</summary>
public sealed record RoundDto(Guid Id, Guid SemesterId, string CourseCode, string RoundType, DateTimeOffset DeadlineAt, string Status, string? Name = null);

/// <summary>Schema <c>StudentSnapshot</c>.</summary>
public sealed record StudentSnapshot(Guid Id, string StudentCode, string FullName, int SpecializationId, string CourseCode, bool IsEnrolled);

/// <summary>Schema <c>CatalogItem</c> (domain, chuyên ngành).</summary>
public sealed record CatalogItem(int Id, string Code, string Name, bool IsActive);

/// <summary>Snapshot giảng viên từ Academic (academic.yaml).</summary>
public sealed record LecturerSnapshot(Guid Id, string FullName, string Email, IReadOnlyList<CatalogItem> Domains, int? MaxTeamsExe101, int? MaxTeamsExe201, bool IsActive);

/// <summary>
/// Snapshot mentor từ Academic (schema <c>Mentor</c>, BE-66 của Nam). <c>currentTeamCount</c> của Academic không đọc: số nhóm đang mentor
/// tính từ <c>mentor_assignments</c> của Team. <see cref="LecturerId"/> khác null = giảng viên kiêm mentor (rule 20).
/// </summary>
public sealed record MentorSnapshot(
    Guid Id, string FullName, string Email, IReadOnlyList<CatalogItem> Domains, int MaxTeams, int? PreferredTeams, bool IsActive,
    string? Organization = null, bool IsInternal = false, Guid? LecturerId = null);

public interface IAcademicClient
{
    /// <summary>Học kỳ hiện hành; null nếu chưa có (404).</summary>
    Task<SemesterDto?> GetCurrentSemesterAsync(CancellationToken ct);

    Task<IReadOnlyList<SemesterDto>> ListSemestersAsync(CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<SemesterDto>>([]);

    /// <summary>Đợt đang mở của môn và loại đợt; null nếu không có (404).</summary>
    Task<RoundDto?> GetOpenRoundAsync(string courseCode, string roundType, CancellationToken ct);

    Task<IReadOnlyList<RoundDto>> ListChangeRequestRoundsAsync(CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<RoundDto>>([]);

    /// <summary>Toàn bộ sinh viên của môn trong học kỳ (<c>GET /internal/students</c>, BE-22).</summary>
    Task<IReadOnlyList<StudentSnapshot>> ListSemesterStudentsAsync(Guid semesterId, string courseCode, CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<StudentSnapshot>>([]);

    /// <summary>Snapshot MSSV, tên, chuyên ngành; sinh viên không thuộc học kỳ không có trong kết quả hoặc <c>IsEnrolled = false</c>.</summary>
    Task<IReadOnlyList<StudentSnapshot>> LookupStudentsAsync(Guid semesterId, IReadOnlyCollection<Guid> studentIds, CancellationToken ct);

    /// <summary>Snapshot giảng viên từ Academic; null nếu chưa có hoặc 404.</summary>
    Task<LecturerSnapshot?> GetLecturerAsync(Guid lecturerId, CancellationToken ct) => Task.FromResult<LecturerSnapshot?>(null);

    /// <summary>
    /// Mọi giảng viên đang hoạt động (<c>GET /api/lecturers</c> với token dịch vụ – Academic chỉ trả giảng viên đang hoạt động
    /// cho người không phải Admin). Duyệt hết các trang; dùng cho ghép giảng viên (BE-80).
    /// </summary>
    Task<IReadOnlyList<LecturerSnapshot>> ListActiveLecturersAsync(CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<LecturerSnapshot>>([]);

    /// <summary>Snapshot mentor từ Academic (BE-66 của Nam); null nếu chưa có hoặc 404.</summary>
    Task<MentorSnapshot?> GetMentorAsync(Guid mentorId, CancellationToken ct) => Task.FromResult<MentorSnapshot?>(null);

    /// <summary>
    /// Mọi mentor của Academic (<c>GET /api/mentors</c> với token dịch vụ), duyệt hết các trang. API này trả cả mentor đang khóa:
    /// người gọi tự lọc <see cref="MentorSnapshot.IsActive"/>. Dùng cho ghép mentor (BE-80).
    /// </summary>
    Task<IReadOnlyList<MentorSnapshot>> ListMentorsAsync(CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<MentorSnapshot>>([]);

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

    /// <summary>pageSize tối đa của Academic (<c>LecturerQueryValidator</c>, <c>MentorQueryValidator</c>: 1–100).</summary>
    internal const int LecturerPageSize = 100;

    /// <summary>Chặn vòng lặp khi Academic trả totalPages sai.</summary>
    private const int MaxLecturerPages = 100;

    public Task<SemesterDto?> GetCurrentSemesterAsync(CancellationToken ct) => GetOrNullAsync<SemesterDto>("/api/semesters/current", ct);

    public async Task<IReadOnlyList<SemesterDto>> ListSemestersAsync(CancellationToken ct) =>
        await http.GetFromJsonAsync<List<SemesterDto>>("/api/semesters", ct) ?? [];

    public Task<RoundDto?> GetOpenRoundAsync(string courseCode, string roundType, CancellationToken ct) =>
        GetOrNullAsync<RoundDto>($"/internal/registration-rounds/open?courseCode={courseCode}&roundType={roundType}", ct);

    public async Task<IReadOnlyList<RoundDto>> ListChangeRequestRoundsAsync(CancellationToken ct) =>
        (await http.GetFromJsonAsync<List<RoundDto>>("/api/registration-rounds?courseCode=EXE201", ct) ?? [])
            .Where(r => r.RoundType == RoundTypes.ChangeRequest).ToList();

    public async Task<IReadOnlyList<StudentSnapshot>> ListSemesterStudentsAsync(Guid semesterId, string courseCode, CancellationToken ct) =>
        await http.GetFromJsonAsync<List<StudentSnapshot>>($"/internal/students?semesterId={semesterId}&courseCode={courseCode}", ct) ?? [];

    public async Task<IReadOnlyList<StudentSnapshot>> LookupStudentsAsync(Guid semesterId, IReadOnlyCollection<Guid> studentIds, CancellationToken ct)
    {
        using var response = await http.PostAsJsonAsync("/internal/students/lookup", new { semesterId, studentIds }, ct);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<List<StudentSnapshot>>(ct) ?? [];
    }

    public async Task<LecturerSnapshot?> GetLecturerAsync(Guid lecturerId, CancellationToken ct)
    {
        try
        {
            return await GetOrNullAsync<LecturerSnapshot>($"/internal/lecturers/{lecturerId}", ct);
        }
        catch (HttpRequestException)
        {
            return null;
        }
    }

    public async Task<IReadOnlyList<LecturerSnapshot>> ListActiveLecturersAsync(CancellationToken ct)
    {
        var lecturers = new List<LecturerSnapshot>();
        for (var page = 1; page <= MaxLecturerPages; page++)
        {
            var result = await http.GetFromJsonAsync<LecturerPage>($"/api/lecturers?page={page}&pageSize={LecturerPageSize}", ct)
                ?? throw new HttpRequestException("Academic trả danh sách giảng viên rỗng.");
            lecturers.AddRange(result.Items.Where(l => l.IsActive));
            if (result.Items.Count == 0 || page >= result.TotalPages)
            {
                break;
            }
        }
        return lecturers.DistinctBy(l => l.Id).ToList();
    }

    private sealed record LecturerPage(IReadOnlyList<LecturerSnapshot> Items, int Page, int PageSize, int TotalCount, int TotalPages);

    public async Task<IReadOnlyList<MentorSnapshot>> ListMentorsAsync(CancellationToken ct)
    {
        var mentors = new List<MentorSnapshot>();
        for (var page = 1; page <= MaxLecturerPages; page++)
        {
            var result = await http.GetFromJsonAsync<MentorPage>($"/api/mentors?page={page}&pageSize={LecturerPageSize}", ct)
                ?? throw new HttpRequestException("Academic trả danh sách mentor rỗng.");
            mentors.AddRange(result.Items);
            if (result.Items.Count == 0 || page >= result.TotalPages)
            {
                break;
            }
        }
        return mentors.DistinctBy(m => m.Id).ToList();
    }

    private sealed record MentorPage(IReadOnlyList<MentorSnapshot> Items, int Page, int PageSize, int TotalCount, int TotalPages);

    public async Task<MentorSnapshot?> GetMentorAsync(Guid mentorId, CancellationToken ct)
    {
        try
        {
            return await GetOrNullAsync<MentorSnapshot>($"/internal/mentors/{mentorId}", ct);
        }
        catch (HttpRequestException)
        {
            return null;
        }
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
