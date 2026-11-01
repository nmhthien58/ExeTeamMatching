using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using BuildingBlocks.Auth;
using BuildingBlocks.Outbox;
using Collab.Repositories.Context;
using Collab.Repositories.Workspaces;
using Collab.Services.Checkpoints;
using Collab.Services.Meetings;
using Collab.Services.Storage;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace Collab.Tests;

/// <summary>
/// Collab.Api chạy trong bộ nhớ: DB InMemory, kho file giả, không Kafka (consumer tắt, không chạy Outbox dispatcher –
/// sự kiện nằm lại trong bảng outbox để test đọc).
/// </summary>
public sealed class CollabApiFactory : WebApplicationFactory<Program>
{
    public const string SigningKey = "test-only-signing-key-0123456789abcdef-xyz";

    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { Converters = { new JsonStringEnumConverter() } };

    private readonly string _databaseName = "collab-" + Guid.NewGuid();

    public InMemoryObjectStorage Storage { get; } = new();

    public FakeAcademicSemesterClient AcademicSemesters { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("Jwt:SigningKey", SigningKey);
        builder.UseSetting("ConnectionStrings:CollabDb", "Host=unused");
        builder.UseSetting("Kafka:ConsumersEnabled", "false");
        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<DbContextOptions<CollabDbContext>>();
            services.AddDbContext<CollabDbContext>(options => options.UseInMemoryDatabase(_databaseName));
            services.RemoveAll<IObjectStorage>();
            services.AddSingleton<IObjectStorage>(Storage);
            services.RemoveAll<IAcademicSemesterClient>();
            services.AddSingleton<IAcademicSemesterClient>(AcademicSemesters);
            services.RemoveAll<IMentorSessionTargetClient>();
            services.AddSingleton<IMentorSessionTargetClient, FakeMentorSessionTargetClient>();
            foreach (var hosted in services.Where(d => d.ServiceType == typeof(IHostedService)
                         && (d.ImplementationType == typeof(OutboxDispatcher<CollabDbContext>) || d.ImplementationType == typeof(EnsureBucketHostedService))).ToList())
            {
                services.Remove(hosted);
            }
        });
    }

    public async Task WithDbAsync(Func<CollabDbContext, Task> action)
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CollabDbContext>();
        await action(db);
        await db.SaveChangesAsync();
    }

    public async Task<T> QueryDbAsync<T>(Func<CollabDbContext, Task<T>> query)
    {
        using var scope = Services.CreateScope();
        return await query(scope.ServiceProvider.GetRequiredService<CollabDbContext>());
    }

    /// <summary>Sự kiện outbox đã ghi, theo loại.</summary>
    public Task<List<OutboxMessage>> EventsAsync(string eventType) =>
        QueryDbAsync(db => db.Set<OutboxMessage>().Where(m => m.EventType == eventType).ToListAsync());

    /// <summary>Workspace kèm thành viên (vai trò trong workspace); trả id workspace.</summary>
    public async Task<Guid> SeedWorkspaceAsync(string teamName, string course, Guid? previousWorkspaceId, params TestPerson[] people)
    {
        var workspaceId = Guid.NewGuid();
        await WithDbAsync(db =>
        {
            db.Set<Workspace>().Add(new Workspace
            {
                Id = workspaceId,
                TeamId = Guid.NewGuid(),
                SemesterId = Guid.NewGuid(),
                CourseCode = course,
                TeamName = teamName,
                PreviousWorkspaceId = previousWorkspaceId,
                CreatedAt = DateTimeOffset.UtcNow,
            });
            db.Set<WorkspaceMember>().AddRange(people.Select(p => new WorkspaceMember
            {
                WorkspaceId = workspaceId,
                PersonType = p.PersonType,
                PersonId = p.Id,
                DisplayName = p.Name,
                Role = p.Role!,
                JoinedAt = DateTimeOffset.UtcNow,
            }));
            return Task.CompletedTask;
        });
        return workspaceId;
    }

    public async Task<Guid> SeedWorkspaceWithSemesterAsync(string teamName, string course, Guid semesterId, Guid? previousWorkspaceId, params TestPerson[] people)
    {
        var workspaceId = Guid.NewGuid();
        await WithDbAsync(db =>
        {
            db.Set<Workspace>().Add(new Workspace
            {
                Id = workspaceId,
                TeamId = Guid.NewGuid(),
                SemesterId = semesterId,
                CourseCode = course,
                TeamName = teamName,
                PreviousWorkspaceId = previousWorkspaceId,
                CreatedAt = DateTimeOffset.UtcNow,
            });
            db.Set<WorkspaceMember>().AddRange(people.Select(p => new WorkspaceMember
            {
                WorkspaceId = workspaceId,
                PersonType = p.PersonType,
                PersonId = p.Id,
                DisplayName = p.Name,
                Role = p.Role!,
                JoinedAt = DateTimeOffset.UtcNow,
            }));
            return Task.CompletedTask;
        });
        return workspaceId;
    }

    public HttpClient ClientFor(TestPerson person)
    {
        var client = CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", Token(person));
        return client;
    }

    /// <summary>JWT giống Identity phát: sub, name, roles và id theo vai trò.</summary>
    public static string Token(TestPerson person)
    {
        var claims = new Dictionary<string, object>
        {
            [ClaimNames.Subject] = Guid.NewGuid().ToString(),
            [ClaimNames.Name] = person.Name,
            [ClaimNames.Roles] = new[] { person.JwtRole },
        };
        if (person.ClaimName is { } claim)
        {
            claims[claim] = person.Id.ToString();
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

    public static async Task<T> ReadAsync<T>(HttpResponseMessage response) =>
        (await response.Content.ReadFromJsonAsync<T>(Json))!;

    /// <summary>Mã lỗi cuối của <c>type</c> trong ProblemDetails.</summary>
    public static async Task<string> ErrorCodeAsync(HttpResponseMessage response) =>
        (await ReadAsync<JsonElement>(response)).GetProperty("type").GetString()!.Split('/')[^1];
}

public sealed class FakeAcademicSemesterClient : IAcademicSemesterClient
{
    public Dictionary<Guid, SemesterCheckpointConfig> Configs { get; } = [];

    public Task<SemesterCheckpointConfig?> GetConfigAsync(Guid semesterId, CancellationToken ct) =>
        Task.FromResult(Configs.GetValueOrDefault(semesterId));
}

/// <summary>Số buổi mentor mặc định, không gọi Academic.</summary>
public sealed class FakeMentorSessionTargetClient : IMentorSessionTargetClient
{
    public Task<int> GetExpectedSessionsAsync(Guid semesterId, CancellationToken ct) => Task.FromResult(MentorSessionTargetClient.DefaultSessions);
}

/// <summary>Người dùng test: vai trò JWT, claim id, vai trò trong workspace.</summary>
public sealed record TestPerson(string Name, string JwtRole, string PersonType, string? Role)
{
    public Guid Id { get; } = Guid.NewGuid();

    public string? ClaimName => JwtRole switch
    {
        Roles.Student => ClaimNames.StudentId,
        Roles.Lecturer => ClaimNames.LecturerId,
        Roles.Mentor => ClaimNames.MentorId,
        _ => null,
    };

    public static TestPerson Leader(string name = "Trưởng nhóm") => new(name, Roles.Student, PersonTypes.Student, WorkspaceRoles.Leader);
    public static TestPerson Member(string name = "Thành viên") => new(name, Roles.Student, PersonTypes.Student, WorkspaceRoles.Member);
    public static TestPerson Instructor(string name = "Giảng viên") => new(name, Roles.Lecturer, PersonTypes.Lecturer, WorkspaceRoles.Instructor);
    public static TestPerson Mentor(string name = "Mentor") => new(name, Roles.Mentor, PersonTypes.Mentor, WorkspaceRoles.Mentor);
    public static TestPerson Admin() => new("Bộ môn", Roles.Admin, "ADMIN", null);
    public static TestPerson Service() => new("jobs", Roles.Service, "SERVICE", null);
}

/// <summary>Kho file S3 giả trong bộ nhớ.</summary>
public sealed class InMemoryObjectStorage : IObjectStorage
{
    public Dictionary<string, byte[]> Objects { get; } = [];

    public async Task PutAsync(string key, Stream content, string contentType, string downloadFileName, CancellationToken ct)
    {
        using var buffer = new MemoryStream();
        await content.CopyToAsync(buffer, ct);
        Objects[key] = buffer.ToArray();
    }

    public string GetDownloadUrl(string key) => $"http://storage.test/{key}?signed=1";

    public Task DeleteAsync(string key, CancellationToken ct)
    {
        Objects.Remove(key);
        return Task.CompletedTask;
    }

    public Task EnsureBucketAsync(CancellationToken ct) => Task.CompletedTask;
}
