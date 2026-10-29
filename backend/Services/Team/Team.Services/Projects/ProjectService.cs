using System.Security.Claims;
using BuildingBlocks.Auth;
using BuildingBlocks.Errors;
using Team.Repositories.AuditLog;
using Team.Repositories.Projects;
using Team.Repositories.Teams;
using Team.Services.Teams;

namespace Team.Services.Projects;

public interface IProjectService
{
    Task<ProjectDetailResponse> GetForTeamAsync(Guid teamId, CancellationToken ct);
    Task<PagedResponse<ProjectDetailResponse>> SearchAsync(ProjectQuery query, CancellationToken ct);
    Task<IReadOnlyList<TeamCardResponse>> UnlinkedTeamsAsync(Guid semesterId, CancellationToken ct);
    Task<ProjectDetailResponse> LinkTeamAsync(Guid projectId, LinkTeamInput input, ClaimsPrincipal user, CancellationToken ct);
}

public sealed class ProjectService(IProjectRepository projects, ITeamRepository teams, TeamReader reader,
    IProjectSemesterClient semesters, AuditLogWriter audit) : IProjectService
{
    public async Task<ProjectDetailResponse> GetForTeamAsync(Guid teamId, CancellationToken ct)
    {
        var graph = await teams.GetGraphAsync(teamId, ct) ?? throw TeamErrors.TeamNotFound(teamId);
        if (graph.Team.Status == TeamStatuses.Disbanded || graph.Team.ProjectId is not { } projectId)
            throw new NotFoundException("đề tài của nhóm", teamId);
        return await DetailAsync(projectId, await SemesterCodesAsync(ct), await reader.LoadCatalogsAsync(ct), ct);
    }

    public async Task<PagedResponse<ProjectDetailResponse>> SearchAsync(ProjectQuery query, CancellationToken ct)
    {
        var (ids, total) = await projects.SearchAsync(new ProjectSearch(query.SemesterId, query.Search,
            query.Domain, query.PivotedOnly, query.Page, query.PageSize), ct);
        var codes = await SemesterCodesAsync(ct);
        var catalogs = await reader.LoadCatalogsAsync(ct);
        var items = new List<ProjectDetailResponse>(ids.Count);
        foreach (var id in ids) items.Add(await DetailAsync(id, codes, catalogs, ct));
        return new PagedResponse<ProjectDetailResponse>(items, query.Page, query.PageSize, total,
            (total + query.PageSize - 1) / query.PageSize);
    }

    public async Task<IReadOnlyList<TeamCardResponse>> UnlinkedTeamsAsync(Guid semesterId, CancellationToken ct)
    {
        if (semesterId == Guid.Empty) throw TeamErrors.Invalid("semesterId", "Chọn học kỳ.");
        var ids = await projects.UnlinkedTeamIdsAsync(semesterId, ct);
        var graphs = await teams.ListGraphsAsync(ids, ct);
        var catalogs = await reader.LoadCatalogsAsync(ct);
        return graphs.Select(g => TeamMapper.ToCard(g, catalogs)).ToList();
    }

    public async Task<ProjectDetailResponse> LinkTeamAsync(Guid projectId, LinkTeamInput input, ClaimsPrincipal user, CancellationToken ct)
    {
        var project = await projects.GetAsync(projectId, ct) ?? throw new NotFoundException("đề tài", projectId);
        var team = await projects.GetTeamForUpdateAsync(input.TeamId, ct) ?? throw TeamErrors.TeamNotFound(input.TeamId);
        var source = project.OriginCourseCode == "EXE101" ? await projects.SourceTeamAsync(project, ct) : null;
        if (source is null || !ProjectLineage.CanLink(project, source, team) ||
            await projects.AlreadyLinkedAsync(project.Id, source.Id, team.SemesterId, ct))
            throw new ConflictException("constraint-violation", "Không thể gắn đề tài",
                "Đề tài hoặc nhóm không phù hợp, hoặc đã được gắn trong học kỳ này.");

        var codes = await SemesterCodesAsync(ct);
        var catalogs = await reader.LoadCatalogsAsync(ct);
        team.ProjectId = project.Id;
        team.SourceTeamId = source.Id;
        projects.AddLinkedEvent(team.Id, project.Id, source.Id);
        audit.Add(team.Id, "Team", team.Id, "TEAM_LINKED_TO_PROJECT", user.ToAuditActor(),
            newValues: new { projectId = project.Id, sourceTeamId = source.Id });
        await projects.SaveAsync(ct);
        return await DetailAsync(project.Id, codes, catalogs, ct);
    }

    private async Task<ProjectDetailResponse> DetailAsync(Guid projectId, IReadOnlyDictionary<Guid, string> codes,
        Catalogs catalogs, CancellationToken ct)
    {
        var project = await projects.GetAsync(projectId, ct) ?? throw new NotFoundException("đề tài", projectId);
        var versions = await projects.VersionsAsync(projectId, ct);
        var teamIds = await projects.TeamIdsAsync(projectId, ct);
        var graphs = await teams.ListGraphsAsync(teamIds, ct);
        var advisors = await projects.AdvisorHistoryAsync(teamIds, ct);
        var byId = graphs.ToDictionary(g => g.Team.Id);
        var history = graphs.OrderBy(g => g.Team.CreatedAt).ThenBy(g => g.Team.Id).Select(g =>
        {
            double? kept = g.Team.SourceTeamId is { } sourceId && byId.TryGetValue(sourceId, out var source)
                ? ProjectLineage.KeptMemberPercent(source.Members.Select(m => m.StudentCode), g.Members.Select(m => m.StudentCode))
                : null;
            return new ProjectTeamResponse(TeamMapper.ToCard(g, catalogs), codes.GetValueOrDefault(g.Team.SemesterId),
                kept, advisors.GetValueOrDefault(g.Team.Id)?.InstructorName ?? g.LecturerName,
                advisors.GetValueOrDefault(g.Team.Id)?.MentorName ?? g.MentorName);
        }).ToList();
        return new ProjectDetailResponse(project.Id, project.Name, project.Description, project.CurrentVersion,
            project.OriginCourseCode, codes.GetValueOrDefault(project.OriginSemesterId),
            versions.Select(v => new ProjectVersionResponse(v.Version, v.Name, v.Description, v.ChangeType,
                v.Reason, codes.GetValueOrDefault(v.SemesterId), v.CourseCode, null, v.CreatedAt)).ToList(), history);
    }

    private async Task<IReadOnlyDictionary<Guid, string>> SemesterCodesAsync(CancellationToken ct) =>
        (await semesters.ListAsync(ct)).ToDictionary(s => s.Id, s => s.Code);
}

public static class ProjectLineage
{
    public static bool CanLink(Project project, TeamEntity source, TeamEntity target) =>
        project.Status == "ACTIVE" && project.OriginCourseCode == "EXE101" && source.CourseCode == "EXE101" &&
        source.ProjectId == project.Id && source.SemesterId == project.OriginSemesterId &&
        target.CourseCode == "EXE201" && target.IsInherited && target.Status != TeamStatuses.Disbanded &&
        target.ProjectId is null && target.SourceTeamId is null && target.SemesterId != source.SemesterId;

    public static double? KeptMemberPercent(IEnumerable<string> previousCodes, IEnumerable<string> currentCodes)
    {
        var previous = previousCodes.Where(c => !string.IsNullOrWhiteSpace(c)).ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (previous.Count == 0) return null;
        var current = currentCodes.ToHashSet(StringComparer.OrdinalIgnoreCase);
        return Math.Round(100d * previous.Count(current.Contains) / previous.Count, 2);
    }
}
