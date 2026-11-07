using System.Security.Claims;
using BuildingBlocks.Auth;
using BuildingBlocks.Errors;
using Team.Repositories.AuditLog;
using Team.Repositories.Members;
using Team.Repositories.Projects;
using Team.Repositories.Teams;
using Team.Services.Academic;

namespace Team.Services.Teams;

public interface ITeamService
{
    /// <summary>S13 – sinh viên tạo nhóm và làm trưởng nhóm; chỉ khi đợt lập nhóm của môn đang mở (rule 4, 6).</summary>
    Task<TeamDetailResponse> CreateAsync(TeamInput input, ClaimsPrincipal user, CancellationToken ct);

    /// <summary>
    /// S16 (trưởng nhóm, trong đợt lập nhóm) / W16 (Admin, mọi lúc). <c>projectName</c>, <c>ideaDescription</c> null = giữ nguyên;
    /// nhóm EXE201 kế thừa không đổi tên đề tài ở đây (rule 26).
    /// </summary>
    Task<TeamDetailResponse> UpdateAsync(Guid teamId, TeamInput input, ClaimsPrincipal user, CancellationToken ct);

    /// <summary>S08, S15, L05, W16.</summary>
    Task<TeamDetailResponse> GetAsync(Guid teamId, ClaimsPrincipal user, CancellationToken ct);

    /// <summary>S15 – nhóm của sinh viên đang đăng nhập; 404 nếu chưa có.</summary>
    Task<TeamDetailResponse> GetMineAsync(ClaimsPrincipal user, CancellationToken ct);

    /// <summary>S01 – trang chủ sinh viên.</summary>
    Task<TeamHomeResponse> GetHomeAsync(ClaimsPrincipal user, CancellationToken ct);

    /// <summary>
    /// S05 (sinh viên: môn và học kỳ của mình, không thấy nhóm đủ người, 409 ngoài đợt lập nhóm – rule 18) / W15 (Admin, giảng viên).
    /// </summary>
    Task<PagedResponse<TeamCardResponse>> SearchAsync(TeamQuery query, ClaimsPrincipal user, CancellationToken ct);
}

/// <summary>Module Teams của Team Service (BE-25, BE-26, BE-27).</summary>
public sealed class TeamService(ITeamRepository teams, AuditLogWriter audit, IAcademicClient academic, RoundGuard rounds, TeamReader reader, TimeProvider clock) : ITeamService
{
    private sealed record DomainChoice(int Id, string Code, bool IsPrimary);

    public async Task<TeamDetailResponse> CreateAsync(TeamInput input, ClaimsPrincipal user, CancellationToken ct)
    {
        var studentId = user.GetStudentId() ?? throw TeamErrors.StudentsOnly();
        var (semester, me) = await rounds.ResolveStudentAsync(studentId, ct);
        await rounds.RequireTeamFormationAsync(me.CourseCode, semester.Id, ct);
        if (await teams.IsStudentInTeamAsync(semester.Id, studentId, ct))
        {
            throw TeamErrors.AlreadyInTeam();
        }

        await RequireTeamQuotaAsync(semester.Id, me.CourseCode, ct);
        var name = input.Name.Trim();
        await RequireUniqueNameAsync(semester.Id, me.CourseCode, name, null, ct);
        var domains = await ResolveDomainsAsync(input, ct);
        var actor = user.ToAuditActor();

        var team = new TeamEntity { Id = Guid.NewGuid(), SemesterId = semester.Id, CourseCode = me.CourseCode, Name = name };
        teams.Add(team);
        teams.Add(new TeamMember
        {
            TeamId = team.Id, SemesterId = semester.Id, StudentId = studentId, StudentCode = me.StudentCode, FullName = me.FullName,
            SpecializationId = me.SpecializationId, Role = MemberRoles.Leader, JoinSource = "CREATOR",
        });
        foreach (var d in domains)
        {
            teams.Add(new TeamDomain { TeamId = team.Id, DomainId = d.Id, DomainCode = d.Code, IsPrimary = d.IsPrimary });
        }
        var project = AddProjectIfRequested(team, input, actor, domains);

        audit.Add(team.Id, "Team", team.Id, "TEAM_CREATED", actor,
            newValues: new { name, projectName = project?.Name, domains = domains.Select(d => d.Code) });
        teams.AddEvent(TeamEventTypes.TeamCreated, team.Id, new TeamCreated(team.Id, semester.Id, team.CourseCode, name, studentId, me.FullName,
            team.ProjectId, IsAutoAssigned: false, IsInherited: false, SourceTeamId: null));
        await teams.SaveChangesAsync(ct);
        return await GetAsync(team.Id, user, ct);
    }

    public async Task<TeamDetailResponse> UpdateAsync(Guid teamId, TeamInput input, ClaimsPrincipal user, CancellationToken ct)
    {
        var team = await teams.GetAsync(teamId, ct);
        if (team is null || team.Status == TeamStatuses.Disbanded)
        {
            throw TeamErrors.TeamNotFound(teamId);
        }

        if (!user.IsInRole(Roles.Admin))
        {
            var graph = await teams.GetGraphAsync(teamId, ct);
            var studentId = user.GetStudentId();
            if (graph!.Members.All(m => m.StudentId != studentId || m.Role != MemberRoles.Leader))
            {
                throw TeamErrors.NotLeader();
            }
            if (team.Status is not (TeamStatuses.Draft or TeamStatuses.Active))
            {
                throw TeamErrors.RoundClosed();
            }
            await rounds.RequireTeamFormationAsync(team.CourseCode, team.SemesterId, ct);
        }

        var name = input.Name.Trim();
        await RequireUniqueNameAsync(team.SemesterId, team.CourseCode, name, team.Id, ct);
        var domains = await ResolveDomainsAsync(input, ct);
        var actor = user.ToAuditActor();
        var current = await teams.ListDomainsAsync(teamId, ct);
        var project = team.ProjectId is { } projectId ? await teams.GetProjectAsync(projectId, ct) : null;
        var before = new { name = team.Name, projectName = project?.Name, ideaDescription = project?.Description, domains = current.Select(d => d.DomainCode) };

        team.Name = name;
        ReplaceDomains(team.Id, current, domains);
        if (project is null)
        {
            project = AddProjectIfRequested(team, input, actor, domains);
        }
        else
        {
            EditProject(team, project, input, actor);
        }

        audit.Add(team.Id, "Team", team.Id, "TEAM_UPDATED", actor, before,
            new { name, projectName = project?.Name, ideaDescription = project?.Description, domains = domains.Select(d => d.Code) });
        await teams.SaveChangesAsync(ct);
        return await GetAsync(team.Id, user, ct);
    }

    public async Task<TeamDetailResponse> GetAsync(Guid teamId, ClaimsPrincipal user, CancellationToken ct)
    {
        var graph = await teams.GetGraphAsync(teamId, ct);
        if (graph is null || (graph.Team.Status == TeamStatuses.Disbanded && !user.IsInRole(Roles.Admin)))
        {
            throw TeamErrors.TeamNotFound(teamId);
        }

        var viewer = user.GetStudentId();
        var extras = await teams.GetDetailExtrasAsync(teamId, viewer, clock.GetUtcNow(), ct);
        return TeamMapper.ToDetail(graph, await reader.LoadCatalogsAsync(ct), extras, TeamMapper.Relation(graph, viewer, extras));
    }

    public async Task<TeamDetailResponse> GetMineAsync(ClaimsPrincipal user, CancellationToken ct)
    {
        var studentId = user.GetStudentId() ?? throw TeamErrors.StudentsOnly();
        var teamId = await teams.FindTeamIdOfStudentAsync(studentId, ct) ?? throw TeamErrors.NoTeam();
        return await GetAsync(teamId, user, ct);
    }

    public async Task<TeamHomeResponse> GetHomeAsync(ClaimsPrincipal user, CancellationToken ct)
    {
        var studentId = user.GetStudentId() ?? throw TeamErrors.StudentsOnly();
        var now = clock.GetUtcNow();
        var invitations = await teams.CountPendingInvitationsAsync(studentId, now, ct);
        var graph = await teams.FindTeamIdOfStudentAsync(studentId, ct) is { } teamId ? await teams.GetGraphAsync(teamId, ct) : null;

        if (graph is null)
        {
            var (semester, me) = await rounds.ResolveStudentAsync(studentId, ct);
            var phase = await rounds.PhaseAsync(me.CourseCode, semester.Id, ct);
            var action = TeamRules.NextAction(new NextActionInput(false, false, phase, null, 0, 0, false, false, false, false));
            return new TeamHomeResponse(null, false, invitations, 0, action, phase, 0, false);
        }

        var team = graph.Team;
        var isLeader = graph.Members.Any(m => m.StudentId == studentId && m.Role == MemberRoles.Leader);
        var stats = await teams.GetHomeStatsAsync(team.Id, now, ct);
        var teamPhase = await rounds.PhaseAsync(team.CourseCode, team.SemesterId, ct);
        var nextAction = TeamRules.NextAction(new NextActionInput(true, isLeader, teamPhase, team.Status, graph.Members.Count,
            graph.Members.Select(m => m.SpecializationId).Distinct().Count(), stats.HasLecturer, stats.HasLecturerPreferences,
            stats.HasUnsubmittedChanges, stats.HasPendingChangeRequest, await academic.GetTeamRulesAsync(ct)));
        return new TeamHomeResponse(TeamMapper.ToCard(graph, await reader.LoadCatalogsAsync(ct)), isLeader, invitations,
            isLeader ? stats.PendingJoinRequests : 0, nextAction, teamPhase, stats.InterestedLecturers, team.IsAutoAssigned);
    }

    public async Task<PagedResponse<TeamCardResponse>> SearchAsync(TeamQuery query, ClaimsPrincipal user, CancellationToken ct)
    {
        Guid semesterId;
        string? courseCode;
        var studentView = user.IsStudentOnly();
        if (studentView)
        {
            var studentId = user.GetStudentId() ?? throw TeamErrors.StudentsOnly();
            var (semester, me) = await rounds.ResolveStudentAsync(studentId, ct);
            await rounds.RequireTeamFormationAsync(me.CourseCode, semester.Id, ct);
            (semesterId, courseCode) = (semester.Id, me.CourseCode);
        }
        else
        {
            semesterId = query.SemesterId ?? (await academic.GetCurrentSemesterAsync(ct))?.Id ?? throw new NotFoundException("học kỳ hiện hành");
            courseCode = query.CourseCode;
        }

        var rules = await academic.GetTeamRulesAsync(ct);
        var (ids, total) = await teams.SearchAsync(new TeamSearch(semesterId, courseCode, query.Search, query.Domain, query.MissingSpecializationId,
            query.MinOpenSlots, query.Status, query.InvalidOnly, HideFullAndDisbanded: studentView, rules.MaxSize, rules.MinSize,
            rules.MinSpecializations, query.Sort, query.Page, query.PageSize), ct);
        var catalogs = await reader.LoadCatalogsAsync(ct);
        var cards = (await teams.ListGraphsAsync(ids, ct)).Select(g => TeamMapper.ToCard(g, catalogs)).ToList();
        return new PagedResponse<TeamCardResponse>(cards, query.Page, query.PageSize, total, (total + query.PageSize - 1) / query.PageSize);
    }

    /// <summary>BE-89: chặn tạo nhóm khi số nhóm của môn trong kỳ (trừ đã giải tán) đạt floor(số sinh viên × hệ số ở W05).</summary>
    private async Task RequireTeamQuotaAsync(Guid semesterId, string courseCode, CancellationToken ct)
    {
        var rules = await academic.GetTeamRulesAsync(ct);
        var students = await academic.ListSemesterStudentsAsync(semesterId, courseCode, ct);
        if (rules.MaxTeams(students.Count) is { } maxTeams && await teams.CountActiveTeamsAsync(semesterId, courseCode, ct) >= maxTeams)
        {
            throw TeamErrors.TeamLimitReached(maxTeams);
        }
    }

    private async Task RequireUniqueNameAsync(Guid semesterId, string courseCode, string name, Guid? exceptTeamId, CancellationToken ct)
    {
        if (await teams.IsNameTakenAsync(semesterId, courseCode, name, exceptTeamId, ct))
        {
            throw TeamErrors.Invalid("name", "Tên nhóm đã có trong học kỳ.");
        }
    }

    /// <summary>Rule 12: domain chọn từ danh mục; domain chính mặc định là domain đầu tiên.</summary>
    private async Task<List<DomainChoice>> ResolveDomainsAsync(TeamInput input, CancellationToken ct)
    {
        var catalog = (await academic.GetDomainsAsync(ct)).Where(d => d.IsActive).ToDictionary(d => d.Id);
        var primary = input.PrimaryDomainId ?? input.DomainIds[0];
        return input.DomainIds.Select(id => catalog.TryGetValue(id, out var d)
            ? new DomainChoice(id, d.Code, id == primary)
            : throw TeamErrors.Invalid("domainIds", $"Domain {id} không có trong danh mục.")).ToList();
    }

    private void ReplaceDomains(Guid teamId, List<TeamDomain> current, List<DomainChoice> wanted)
    {
        foreach (var old in current.Where(c => wanted.All(w => w.Id != c.DomainId)))
        {
            teams.Remove(old);
        }
        foreach (var w in wanted)
        {
            if (current.FirstOrDefault(c => c.DomainId == w.Id) is { } existing)
            {
                existing.IsPrimary = w.IsPrimary;
            }
            else
            {
                teams.Add(new TeamDomain { TeamId = teamId, DomainId = w.Id, DomainCode = w.Code, IsPrimary = w.IsPrimary });
            }
        }
    }

    /// <summary>Đề tài tạo cùng nhóm khi có tên đề tài (rule 26 – tối thiểu; lịch sử, kế thừa, PIVOT ở module Projects).</summary>
    private Project? AddProjectIfRequested(TeamEntity team, TeamInput input, AuditActor actor, List<DomainChoice> domains)
    {
        if (string.IsNullOrWhiteSpace(input.ProjectName))
        {
            return string.IsNullOrWhiteSpace(input.IdeaDescription) ? null : throw TeamErrors.Invalid("projectName", "Nhập tên đề tài cho ý tưởng.");
        }

        var project = new Project
        {
            Id = Guid.NewGuid(), Name = input.ProjectName.Trim(), Description = Blank(input.IdeaDescription),
            OriginSemesterId = team.SemesterId, OriginCourseCode = team.CourseCode,
        };
        teams.Add(project);
        teams.Add(NewVersion(project, team, "CREATED", actor));
        team.ProjectId = project.Id;
        teams.AddEvent(TeamEventTypes.ProjectCreated, team.Id,
            new ProjectCreated(project.Id, team.Id, project.Name, domains.Select(d => d.Id).ToList(), team.SemesterId, team.CourseCode));
        return project;
    }

    private void EditProject(TeamEntity team, Project project, TeamInput input, AuditActor actor)
    {
        var name = input.ProjectName?.Trim() ?? project.Name;
        var description = input.IdeaDescription is null ? project.Description : Blank(input.IdeaDescription);
        if (name == project.Name && description == project.Description)
        {
            return;
        }
        if (team.IsInherited && name != project.Name)
        {
            throw TeamErrors.Invalid("projectName", "Đổi đề tài của nhóm kế thừa phải qua yêu cầu đổi nhóm.");
        }

        project.Name = name;
        project.Description = description;
        project.CurrentVersion++;
        teams.Add(NewVersion(project, team, "EDITED", actor));
        teams.AddEvent(TeamEventTypes.ProjectChanged, team.Id,
            new ProjectChanged(project.Id, team.Id, project.CurrentVersion, "EDITED", name, Reason: null, ChangeRequestId: null));
    }

    private static ProjectVersion NewVersion(Project project, TeamEntity team, string changeType, AuditActor actor) => new()
    {
        ProjectId = project.Id, Version = project.CurrentVersion, Name = project.Name, Description = project.Description, ChangeType = changeType,
        SemesterId = team.SemesterId, CourseCode = team.CourseCode, ChangedByAccountId = actor.AccountId,
    };

    private static string? Blank(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
