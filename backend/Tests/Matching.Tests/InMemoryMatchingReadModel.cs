using Matching.Services.ReadModel;

namespace Matching.Tests;

/// <summary>Read model trong bộ nhớ, cùng hành vi với bản Redis, để test thuật toán và projector không cần Redis.</summary>
public sealed class InMemoryMatchingReadModel : IMatchingReadModel
{
    private readonly Dictionary<Guid, StudentRow> _students = [];
    private readonly Dictionary<Guid, TeamRow> _teams = [];
    private readonly Dictionary<Guid, ProjectRow> _projects = [];
    private readonly Dictionary<(AdvisorKind, Guid), AdvisorView> _advisors = [];

    public Task<StudentView?> GetStudentAsync(Guid studentId, CancellationToken ct) =>
        Task.FromResult(_students.GetValueOrDefault(studentId)?.ToView());

    public Task<IReadOnlyList<StudentView>> GetStudentsAsync(Guid semesterId, string courseCode, CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<StudentView>>(_students.Values
            .Where(s => s.SemesterId == semesterId && s.CourseCode == courseCode)
            .Select(s => s.ToView()).OfType<StudentView>().ToList());

    public Task<IReadOnlyList<StudentView>> GetStudentsByIdsAsync(IEnumerable<Guid> studentIds, CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<StudentView>>(studentIds.Distinct()
            .Select(id => _students.GetValueOrDefault(id)?.ToView()).OfType<StudentView>().ToList());

    public Task<TeamView?> GetTeamAsync(Guid teamId, CancellationToken ct) =>
        Task.FromResult(_teams.GetValueOrDefault(teamId) is { } t ? ToView(t) : null);

    public Task<IReadOnlyList<TeamView>> GetTeamsAsync(Guid semesterId, string? courseCode, CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<TeamView>>(_teams.Values
            .Where(t => t.SemesterId == semesterId && (courseCode is null || t.CourseCode == courseCode))
            .Select(ToView).OfType<TeamView>().ToList());

    public Task<AdvisorView?> GetAdvisorAsync(AdvisorKind kind, Guid advisorId, CancellationToken ct) =>
        Task.FromResult(_advisors.GetValueOrDefault((kind, advisorId)));

    public Task SaveStudentAsync(Guid studentId, Guid semesterId, string courseCode, string? fullName, int specializationId, CancellationToken ct)
    {
        var row = Student(studentId);
        if (row.SemesterId != Guid.Empty && row.SemesterId != semesterId
            && !(row.TeamId is { } teamId && _teams.TryGetValue(teamId, out var team) && team.SemesterId == semesterId))
        {
            row.TeamId = null;
        }
        row.SemesterId = semesterId;
        row.CourseCode = courseCode;
        row.FullName = fullName ?? row.FullName;
        row.SpecializationId = specializationId;
        return Task.CompletedTask;
    }

    public Task SaveStudentProfileAsync(Guid studentId, string? fullName, int specializationId, IReadOnlyDictionary<Skill, int> skills,
        bool isSeekingTeam, CancellationToken ct)
    {
        var row = Student(studentId);
        row.FullName = fullName ?? row.FullName;
        row.SpecializationId = specializationId;
        row.Skills = new Dictionary<Skill, int>(skills);
        row.IsSeekingTeam = isSeekingTeam;
        return Task.CompletedTask;
    }

    public Task SetStudentTeamAsync(Guid studentId, Guid teamId, bool isMember, CancellationToken ct)
    {
        var row = Student(studentId);
        if (isMember)
        {
            row.TeamId = teamId;
        }
        else if (row.TeamId == teamId)
        {
            row.TeamId = null;
        }
        return Task.CompletedTask;
    }

    public async Task SaveTeamAsync(Guid teamId, Guid semesterId, string courseCode, string name, Guid? projectId, CancellationToken ct)
    {
        var row = Team(teamId);
        row.SemesterId = semesterId;
        row.CourseCode = courseCode;
        row.Name = name;
        if (projectId is { } p)
        {
            await LinkTeamToProjectAsync(teamId, p, ct);
        }
    }

    public Task AddTeamMemberAsync(Guid teamId, Guid studentId, CancellationToken ct)
    {
        Team(teamId).Members.Add(studentId);
        return Task.CompletedTask;
    }

    public Task RemoveTeamMemberAsync(Guid teamId, Guid studentId, CancellationToken ct)
    {
        Team(teamId).Members.Remove(studentId);
        return Task.CompletedTask;
    }

    public Task SetTeamStatusAsync(Guid teamId, string status, CancellationToken ct)
    {
        Team(teamId).Status = status;
        return Task.CompletedTask;
    }

    public Task LinkTeamToProjectAsync(Guid teamId, Guid projectId, CancellationToken ct)
    {
        Team(teamId).ProjectId = projectId;
        Project(projectId).Teams.Add(teamId);
        return Task.CompletedTask;
    }

    public Task SaveProjectDomainsAsync(Guid projectId, IReadOnlyList<int> domainIds, CancellationToken ct)
    {
        Project(projectId).DomainIds = domainIds.Distinct().Order().ToList();
        return Task.CompletedTask;
    }

    public Task SetProjectExe101LecturerAsync(Guid projectId, Guid lecturerId, CancellationToken ct)
    {
        Project(projectId).Exe101LecturerId = lecturerId;
        return Task.CompletedTask;
    }

    public Task SetTeamAdvisorAsync(Guid teamId, AdvisorKind kind, Guid? advisorId, CancellationToken ct)
    {
        if (kind == AdvisorKind.Mentor)
        {
            Team(teamId).MentorId = advisorId;
        }
        else
        {
            Team(teamId).LecturerId = advisorId;
        }
        return Task.CompletedTask;
    }

    public Task SetInterestAsync(Guid teamId, AdvisorKind kind, Guid advisorId, bool isOpen, CancellationToken ct)
    {
        var set = kind == AdvisorKind.Mentor ? Team(teamId).MentorInterests : Team(teamId).LecturerInterests;
        if (isOpen)
        {
            set.Add(advisorId);
        }
        else
        {
            set.Remove(advisorId);
        }
        return Task.CompletedTask;
    }

    public Task SaveAdvisorAsync(AdvisorKind kind, Guid advisorId, IReadOnlyList<int>? domainIds, bool isActive, CancellationToken ct)
    {
        var domains = domainIds ?? _advisors.GetValueOrDefault((kind, advisorId))?.DomainIds ?? [];
        _advisors[(kind, advisorId)] = new AdvisorView(kind, advisorId, domains.Distinct().Order().ToList(), isActive);
        return Task.CompletedTask;
    }

    private StudentRow Student(Guid id) => _students.TryGetValue(id, out var row) ? row : _students[id] = new StudentRow { Id = id };

    private TeamRow Team(Guid id) => _teams.TryGetValue(id, out var row) ? row : _teams[id] = new TeamRow { Id = id };

    private ProjectRow Project(Guid id) => _projects.TryGetValue(id, out var row) ? row : _projects[id] = new ProjectRow();

    private TeamView? ToView(TeamRow t)
    {
        if (t.SemesterId is not { } semesterId)
        {
            return null;
        }
        var project = t.ProjectId is { } p ? _projects.GetValueOrDefault(p) : null;
        return new TeamView(t.Id, semesterId, t.CourseCode!, t.Name, t.Status, t.ProjectId, project?.DomainIds ?? [],
            t.Members.ToHashSet(), t.LecturerId, t.MentorId, t.LecturerInterests.ToHashSet(), t.MentorInterests.ToHashSet(),
            project?.Exe101LecturerId);
    }

    private sealed class StudentRow
    {
        public Guid Id { get; init; }
        public Guid? SemesterId { get; set; }
        public string? CourseCode { get; set; }
        public string? FullName { get; set; }
        public int SpecializationId { get; set; }
        public Dictionary<Skill, int> Skills { get; set; } = [];
        public bool IsSeekingTeam { get; set; }
        public Guid? TeamId { get; set; }

        public StudentView? ToView() => SemesterId is { } semesterId
            ? new StudentView(Id, semesterId, CourseCode!, FullName, SpecializationId, Skills, IsSeekingTeam, TeamId)
            : null;
    }

    private sealed class TeamRow
    {
        public Guid Id { get; init; }
        public Guid? SemesterId { get; set; }
        public string? CourseCode { get; set; }
        public string? Name { get; set; }
        public string Status { get; set; } = TeamStatuses.Open;
        public Guid? ProjectId { get; set; }
        public HashSet<Guid> Members { get; } = [];
        public Guid? LecturerId { get; set; }
        public Guid? MentorId { get; set; }
        public HashSet<Guid> LecturerInterests { get; } = [];
        public HashSet<Guid> MentorInterests { get; } = [];
    }

    private sealed class ProjectRow
    {
        public List<int> DomainIds { get; set; } = [];
        public Guid? Exe101LecturerId { get; set; }
        public HashSet<Guid> Teams { get; } = [];
    }
}
