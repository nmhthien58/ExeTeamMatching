using System.Globalization;
using System.Text.Json;
using Microsoft.Extensions.Options;
using StackExchange.Redis;

namespace Matching.Services.ReadModel;

/// <summary>Cấu hình read model (section <c>Matching</c>).</summary>
public sealed class MatchingOptions
{
    public const string SectionName = "Matching";

    /// <summary>Tiền tố mọi key Redis của Matching.</summary>
    public string KeyPrefix { get; set; } = "matching:";

    /// <summary>Giữ dấu "đã xử lý" của sự kiện bao lâu (ngày) để chống xử lý trùng.</summary>
    public int ProcessedEventDays { get; set; } = 14;
}

/// <summary>
/// Read model trên Redis. Mỗi thực thể là một hash (chỉ ghi trường liên quan) cộng các set chỉ mục:
/// <list type="bullet">
/// <item><c>student:{id}</c>, <c>students:{semesterId}:{course}</c></item>
/// <item><c>team:{id}</c>, <c>team:{id}:members</c>, <c>team:{id}:interests:{kind}</c>, <c>teams:{semesterId}:{course}</c></item>
/// <item><c>project:{id}</c>, <c>project:{id}:teams</c></item>
/// <item><c>advisor:{kind}:{id}</c></item>
/// </list>
/// </summary>
public sealed class RedisMatchingReadModel(IConnectionMultiplexer redis, IOptions<MatchingOptions> options) : IMatchingReadModel
{
    private readonly string _prefix = options.Value.KeyPrefix;

    private IDatabase Db => redis.GetDatabase();

    // ---------- Key ----------

    private RedisKey StudentKey(Guid id) => $"{_prefix}student:{id}";
    private RedisKey StudentIndexKey(Guid semesterId, string course) => $"{_prefix}students:{semesterId}:{course}";
    private RedisKey TeamKey(Guid id) => $"{_prefix}team:{id}";
    private RedisKey TeamMembersKey(Guid id) => $"{_prefix}team:{id}:members";
    private RedisKey TeamInterestsKey(Guid id, AdvisorKind kind) => $"{_prefix}team:{id}:interests:{KindName(kind)}";
    private RedisKey TeamIndexKey(Guid semesterId, string course) => $"{_prefix}teams:{semesterId}:{course}";
    private RedisKey ProjectKey(Guid id) => $"{_prefix}project:{id}";
    private RedisKey ProjectTeamsKey(Guid id) => $"{_prefix}project:{id}:teams";
    private RedisKey AdvisorKey(AdvisorKind kind, Guid id) => $"{_prefix}advisor:{KindName(kind)}:{id}";

    private static string KindName(AdvisorKind kind) => kind == AdvisorKind.Mentor ? "mentor" : "lecturer";

    private static class F
    {
        public const string SemesterId = "semesterId";
        public const string CourseCode = "courseCode";
        public const string FullName = "fullName";
        public const string SpecializationId = "specializationId";
        public const string Skills = "skills";
        public const string Seeking = "seeking";
        public const string TeamId = "teamId";
        public const string Name = "name";
        public const string Status = "status";
        public const string ProjectId = "projectId";
        public const string DomainIds = "domainIds";
        public const string LecturerId = "lecturerId";
        public const string MentorId = "mentorId";
        public const string Exe101LecturerId = "exe101LecturerId";
        public const string Active = "active";
    }

    // ---------- Đọc ----------

    public async Task<StudentView?> GetStudentAsync(Guid studentId, CancellationToken ct) =>
        ToStudent(studentId, await Db.HashGetAllAsync(StudentKey(studentId)));

    public async Task<IReadOnlyList<StudentView>> GetStudentsAsync(Guid semesterId, string courseCode, CancellationToken ct)
    {
        var ids = await Db.SetMembersAsync(StudentIndexKey(semesterId, courseCode));
        return await GetStudentsByIdsAsync(ids.Select(v => Guid.Parse(v.ToString())), ct);
    }

    public async Task<IReadOnlyList<StudentView>> GetStudentsByIdsAsync(IEnumerable<Guid> studentIds, CancellationToken ct)
    {
        var db = Db;
        var ids = studentIds.Distinct().ToList();
        var hashes = await Task.WhenAll(ids.Select(id => db.HashGetAllAsync(StudentKey(id))));
        return ids.Select((id, i) => ToStudent(id, hashes[i])).OfType<StudentView>().ToList();
    }

    public async Task<TeamView?> GetTeamAsync(Guid teamId, CancellationToken ct)
    {
        var teams = await GetTeamsByIdsAsync([teamId]);
        return teams.FirstOrDefault();
    }

    public async Task<IReadOnlyList<TeamView>> GetTeamsAsync(Guid semesterId, string? courseCode, CancellationToken ct)
    {
        var courses = courseCode is null ? new[] { Courses.Exe101, Courses.Exe201 } : [courseCode];
        var db = Db;
        var sets = await Task.WhenAll(courses.Select(c => db.SetMembersAsync(TeamIndexKey(semesterId, c))));
        return await GetTeamsByIdsAsync(sets.SelectMany(s => s).Select(v => Guid.Parse(v.ToString())).Distinct().ToList());
    }

    private async Task<IReadOnlyList<TeamView>> GetTeamsByIdsAsync(IReadOnlyList<Guid> ids)
    {
        var db = Db;
        var hashes = Task.WhenAll(ids.Select(id => db.HashGetAllAsync(TeamKey(id))));
        var members = Task.WhenAll(ids.Select(id => db.SetMembersAsync(TeamMembersKey(id))));
        var lecturerInterests = Task.WhenAll(ids.Select(id => db.SetMembersAsync(TeamInterestsKey(id, AdvisorKind.Lecturer))));
        var mentorInterests = Task.WhenAll(ids.Select(id => db.SetMembersAsync(TeamInterestsKey(id, AdvisorKind.Mentor))));
        await Task.WhenAll(hashes, members, lecturerInterests, mentorInterests);

        var rows = hashes.Result.Select(ToDictionary).ToList();
        var projectIds = rows.Select(h => ParseGuid(h.GetValueOrDefault(F.ProjectId))).ToList();
        var exe101Lecturers = await Task.WhenAll(projectIds.Select(p => p is { } id
            ? db.HashGetAsync(ProjectKey(id), F.Exe101LecturerId)
            : Task.FromResult(RedisValue.Null)));

        var teams = new List<TeamView>(ids.Count);
        for (var i = 0; i < ids.Count; i++)
        {
            var hash = rows[i];
            if (!hash.ContainsKey(F.SemesterId))
            {
                continue; // TeamCreated chưa tới
            }

            teams.Add(new TeamView(
                ids[i],
                Guid.Parse(hash[F.SemesterId]),
                hash.GetValueOrDefault(F.CourseCode) ?? Courses.Exe101,
                hash.GetValueOrDefault(F.Name),
                hash.GetValueOrDefault(F.Status) ?? TeamStatuses.Open,
                projectIds[i],
                ParseIntList(hash.GetValueOrDefault(F.DomainIds)),
                ToGuidSet(members.Result[i]),
                ParseGuid(hash.GetValueOrDefault(F.LecturerId)),
                ParseGuid(hash.GetValueOrDefault(F.MentorId)),
                ToGuidSet(lecturerInterests.Result[i]),
                ToGuidSet(mentorInterests.Result[i]),
                ParseGuid(exe101Lecturers[i])));
        }
        return teams;
    }

    public async Task<AdvisorView?> GetAdvisorAsync(AdvisorKind kind, Guid advisorId, CancellationToken ct)
    {
        var hash = ToDictionary(await Db.HashGetAllAsync(AdvisorKey(kind, advisorId)));
        if (hash.Count == 0)
        {
            return null;
        }
        return new AdvisorView(kind, advisorId, ParseIntList(hash.GetValueOrDefault(F.DomainIds)), hash.GetValueOrDefault(F.Active) != "0");
    }

    // ---------- Ghi: sinh viên ----------

    public async Task SaveStudentAsync(Guid studentId, Guid semesterId, string courseCode, string? fullName, int specializationId, CancellationToken ct)
    {
        var db = Db;
        var entries = new List<HashEntry>
        {
            new(F.SemesterId, semesterId.ToString()),
            new(F.CourseCode, courseCode),
            new(F.SpecializationId, specializationId),
        };
        if (!string.IsNullOrWhiteSpace(fullName))
        {
            entries.Add(new HashEntry(F.FullName, fullName));
        }
        await db.HashSetAsync(StudentKey(studentId), entries.ToArray());
        await db.SetAddAsync(StudentIndexKey(semesterId, courseCode), studentId.ToString());
    }

    public Task SaveStudentProfileAsync(Guid studentId, string? fullName, int specializationId, IReadOnlyDictionary<Skill, int> skills,
        bool isSeekingTeam, CancellationToken ct)
    {
        var entries = new List<HashEntry>
        {
            new(F.SpecializationId, specializationId),
            new(F.Skills, JsonSerializer.Serialize(skills.ToDictionary(s => ((int)s.Key).ToString(CultureInfo.InvariantCulture), s => s.Value))),
            new(F.Seeking, isSeekingTeam ? "1" : "0"),
        };
        if (!string.IsNullOrWhiteSpace(fullName))
        {
            entries.Add(new HashEntry(F.FullName, fullName));
        }
        return Db.HashSetAsync(StudentKey(studentId), entries.ToArray());
    }

    public async Task SetStudentTeamAsync(Guid studentId, Guid teamId, bool isMember, CancellationToken ct)
    {
        if (isMember)
        {
            await Db.HashSetAsync(StudentKey(studentId), F.TeamId, teamId.ToString());
            return;
        }

        var transaction = Db.CreateTransaction();
        transaction.AddCondition(Condition.HashEqual(StudentKey(studentId), F.TeamId, teamId.ToString()));
        _ = transaction.HashDeleteAsync(StudentKey(studentId), F.TeamId);
        await transaction.ExecuteAsync();
    }

    // ---------- Ghi: nhóm ----------

    public async Task SaveTeamAsync(Guid teamId, Guid semesterId, string courseCode, string name, Guid? projectId, CancellationToken ct)
    {
        var db = Db;
        await db.HashSetAsync(TeamKey(teamId),
        [
            new HashEntry(F.SemesterId, semesterId.ToString()),
            new HashEntry(F.CourseCode, courseCode),
            new HashEntry(F.Name, name),
        ]);
        // Không ghi đè trạng thái đã chốt nếu TeamCreated tới lại.
        await db.HashSetAsync(TeamKey(teamId), F.Status, TeamStatuses.Open, When.NotExists);
        await db.SetAddAsync(TeamIndexKey(semesterId, courseCode), teamId.ToString());
        if (projectId is { } project)
        {
            await LinkTeamToProjectAsync(teamId, project, ct);
        }
    }

    public Task AddTeamMemberAsync(Guid teamId, Guid studentId, CancellationToken ct) =>
        Db.SetAddAsync(TeamMembersKey(teamId), studentId.ToString());

    public Task RemoveTeamMemberAsync(Guid teamId, Guid studentId, CancellationToken ct) =>
        Db.SetRemoveAsync(TeamMembersKey(teamId), studentId.ToString());

    public Task SetTeamStatusAsync(Guid teamId, string status, CancellationToken ct) =>
        Db.HashSetAsync(TeamKey(teamId), F.Status, status);

    public async Task LinkTeamToProjectAsync(Guid teamId, Guid projectId, CancellationToken ct)
    {
        var db = Db;
        await db.HashSetAsync(TeamKey(teamId), F.ProjectId, projectId.ToString());
        // Thêm vào set trước rồi mới đọc domain: nếu ProjectCreated tới đồng thời thì nó sẽ cập nhật nhóm này.
        await db.SetAddAsync(ProjectTeamsKey(projectId), teamId.ToString());
        var domains = await db.HashGetAsync(ProjectKey(projectId), F.DomainIds);
        if (domains.HasValue)
        {
            await db.HashSetAsync(TeamKey(teamId), F.DomainIds, domains);
        }
    }

    public async Task SaveProjectDomainsAsync(Guid projectId, IReadOnlyList<int> domainIds, CancellationToken ct)
    {
        var db = Db;
        var json = JsonSerializer.Serialize(domainIds.Distinct().Order());
        await db.HashSetAsync(ProjectKey(projectId), F.DomainIds, json);
        var teams = await db.SetMembersAsync(ProjectTeamsKey(projectId));
        await Task.WhenAll(teams.Select(t => db.HashSetAsync(TeamKey(Guid.Parse(t.ToString())), F.DomainIds, json)));
    }

    public Task SetProjectExe101LecturerAsync(Guid projectId, Guid lecturerId, CancellationToken ct) =>
        Db.HashSetAsync(ProjectKey(projectId), F.Exe101LecturerId, lecturerId.ToString());

    public Task SetTeamAdvisorAsync(Guid teamId, AdvisorKind kind, Guid? advisorId, CancellationToken ct)
    {
        var field = kind == AdvisorKind.Mentor ? F.MentorId : F.LecturerId;
        return advisorId is { } id
            ? Db.HashSetAsync(TeamKey(teamId), field, id.ToString())
            : Db.HashDeleteAsync(TeamKey(teamId), field);
    }

    public Task SetInterestAsync(Guid teamId, AdvisorKind kind, Guid advisorId, bool isOpen, CancellationToken ct) =>
        isOpen
            ? Db.SetAddAsync(TeamInterestsKey(teamId, kind), advisorId.ToString())
            : Db.SetRemoveAsync(TeamInterestsKey(teamId, kind), advisorId.ToString());

    // ---------- Ghi: cố vấn ----------

    public Task SaveAdvisorAsync(AdvisorKind kind, Guid advisorId, IReadOnlyList<int>? domainIds, bool isActive, CancellationToken ct)
    {
        var entries = new List<HashEntry> { new(F.Active, isActive ? "1" : "0") };
        if (domainIds is not null)
        {
            entries.Add(new HashEntry(F.DomainIds, JsonSerializer.Serialize(domainIds.Distinct().Order())));
        }
        return Db.HashSetAsync(AdvisorKey(kind, advisorId), entries.ToArray());
    }

    // ---------- Chuyển đổi ----------

    private static StudentView? ToStudent(Guid id, HashEntry[] entries)
    {
        var hash = ToDictionary(entries);
        if (!hash.TryGetValue(F.SemesterId, out var semester))
        {
            return null; // StudentImported chưa tới
        }

        var skills = new Dictionary<Skill, int>();
        if (hash.GetValueOrDefault(F.Skills) is { } json)
        {
            foreach (var (key, level) in JsonSerializer.Deserialize<Dictionary<string, int>>(json) ?? [])
            {
                skills[(Skill)int.Parse(key, CultureInfo.InvariantCulture)] = level;
            }
        }

        return new StudentView(
            id,
            Guid.Parse(semester),
            hash.GetValueOrDefault(F.CourseCode) ?? Courses.Exe101,
            hash.GetValueOrDefault(F.FullName),
            int.TryParse(hash.GetValueOrDefault(F.SpecializationId), out var spec) ? spec : 0,
            skills,
            hash.GetValueOrDefault(F.Seeking) == "1",
            ParseGuid(hash.GetValueOrDefault(F.TeamId)));
    }

    private static Dictionary<string, string> ToDictionary(HashEntry[] entries) =>
        entries.ToDictionary(e => e.Name.ToString(), e => e.Value.ToString());

    private static HashSet<Guid> ToGuidSet(RedisValue[] values) =>
        values.Select(v => Guid.Parse(v.ToString())).ToHashSet();

    private static Guid? ParseGuid(string? value) => Guid.TryParse(value, out var id) ? id : null;

    private static Guid? ParseGuid(RedisValue value) => value.HasValue ? ParseGuid(value.ToString()) : null;

    private static IReadOnlyList<int> ParseIntList(string? json) =>
        string.IsNullOrEmpty(json) ? [] : JsonSerializer.Deserialize<List<int>>(json) ?? [];
}
