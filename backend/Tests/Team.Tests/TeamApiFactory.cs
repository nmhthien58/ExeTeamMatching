using System.Net.Http.Headers;
using System.Text;
using BuildingBlocks.Auth;
using BuildingBlocks.Outbox;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using Team.Repositories.Context;
using Team.Services.Academic;
using Team.Services.Matching;

namespace Team.Tests;

/// <summary>
/// Team.Api trong bộ nhớ với team_db thật (cần trigger, xmin – xem <see cref="TeamDbIntegrationTests"/>), Academic và Matching giả.
/// Không chạy Outbox dispatcher: sự kiện nằm lại trong outbox để test đọc.
/// </summary>
public sealed class TeamApiFactory : WebApplicationFactory<Program>
{
    public const string SigningKey = "test-only-signing-key-0123456789abcdef-xyz";

    public FakeAcademic Academic { get; } = new();

    public FakeMatching Matching { get; } = new();

    public static string ConnectionString => Environment.GetEnvironmentVariable("EXE_TEAM_DB")
        ?? "Host=localhost;Port=5432;Database=team_db;Username=team_svc;Password=team_dev_pw";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("Jwt:SigningKey", SigningKey);
        builder.UseSetting("ConnectionStrings:TeamDb", ConnectionString);
        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IAcademicClient>();
            services.AddSingleton<IAcademicClient>(Academic);
            services.RemoveAll<IMatchingClient>();
            services.AddSingleton<IMatchingClient>(Matching);
            foreach (var hosted in services.Where(d => d.ImplementationType == typeof(OutboxDispatcher<TeamDbContext>)).ToList())
            {
                services.Remove(hosted);
            }
        });
    }

    public HttpClient ClientFor(Guid? studentId, params string[] roles)
    {
        var client = CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", Token(studentId, roles));
        return client;
    }

    public async Task<T> QueryDbAsync<T>(Func<TeamDbContext, Task<T>> query)
    {
        using var scope = Services.CreateScope();
        return await query(scope.ServiceProvider.GetRequiredService<TeamDbContext>());
    }

    public Task<List<OutboxMessage>> EventsAsync(Guid teamId) =>
        QueryDbAsync(db => db.Set<OutboxMessage>().Where(m => m.AggregateId == teamId).OrderBy(m => m.OccurredAt).ToListAsync());

    private static string Token(Guid? studentId, string[] roles)
    {
        var claims = new Dictionary<string, object>
        {
            [ClaimNames.Subject] = Guid.NewGuid().ToString(),
            [ClaimNames.Name] = "Người thử",
            [ClaimNames.Roles] = roles.Length == 0 ? [Roles.Student] : roles,
        };
        if (studentId is { } id)
        {
            claims[ClaimNames.StudentId] = id.ToString();
        }
        return new JsonWebTokenHandler().CreateToken(new SecurityTokenDescriptor
        {
            Issuer = "exe-identity",
            Audience = "exe-team-matching",
            Claims = claims,
            Expires = DateTime.UtcNow.AddMinutes(10),
            SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(SigningKey)), SecurityAlgorithms.HmacSha256),
        });
    }
}

/// <summary>Academic giả: một học kỳ, đợt lập nhóm mở theo môn, sinh viên đăng ký bằng <see cref="Enroll"/>.</summary>
public sealed class FakeAcademic : IAcademicClient
{
    private readonly Dictionary<Guid, StudentSnapshot> _students = new();

    public SemesterDto Semester { get; } = new(Guid.NewGuid(), "FA26", "Fall 2026");

    public HashSet<string> OpenCourses { get; } = ["EXE101", "EXE201"];

    /// <summary>false = Academic chưa có <c>GET /internal/students</c> (404), Team dùng read model của Matching.</summary>
    public bool RosterAvailable { get; set; }

    public StudentSnapshot Enroll(int specializationId = 1, string course = "EXE101", bool enrolled = true)
    {
        var id = Guid.NewGuid();
        var student = new StudentSnapshot(id, $"SE{Random.Shared.Next(100000, 999999)}", "Sinh viên " + id.ToString()[..4], specializationId, course, enrolled);
        _students[id] = student;
        return student;
    }

    public Task<SemesterDto?> GetCurrentSemesterAsync(CancellationToken ct) => Task.FromResult<SemesterDto?>(Semester);

    public Task<RoundDto?> GetOpenRoundAsync(string courseCode, string roundType, CancellationToken ct) =>
        Task.FromResult(OpenCourses.Contains(courseCode) && roundType == RoundTypes.TeamFormation
            ? new RoundDto(Guid.NewGuid(), Semester.Id, courseCode, roundType, DateTimeOffset.UtcNow.AddDays(7), "OPEN")
            : null);

    public Task<IReadOnlyList<StudentSnapshot>?> ListSemesterStudentsAsync(Guid semesterId, string courseCode, CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<StudentSnapshot>?>(RosterAvailable && semesterId == Semester.Id
            ? _students.Values.Where(s => s.CourseCode == courseCode).ToList()
            : null);

    public Task<IReadOnlyList<StudentSnapshot>> LookupStudentsAsync(Guid semesterId, IReadOnlyCollection<Guid> studentIds, CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<StudentSnapshot>>(studentIds.Where(_students.ContainsKey).Select(id => _students[id]).ToList());

    public Task<IReadOnlyList<CatalogItem>> GetDomainsAsync(CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<CatalogItem>>(Enumerable.Range(1, 18).Select(i => new CatalogItem(i, $"D{i:00}", $"Domain {i}", true)).ToList());

    public Task<IReadOnlyList<CatalogItem>> GetSpecializationsAsync(CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<CatalogItem>>(Enumerable.Range(1, 6).Select(i => new CatalogItem(i, $"S{i}", $"Chuyên ngành {i}", true)).ToList());
}
