using System.Security.Claims;
using BuildingBlocks.Auth;
using BuildingBlocks.Errors;
using Team.Repositories.AuditLog;
using Team.Repositories.BaselineImport;
using Team.Repositories.ChangeRequests;
using Team.Repositories.Members;
using Team.Repositories.Projects;
using Team.Repositories.Teams;
using Team.Services.Academic;
using Team.Services.Members;
using Team.Services.Teams;

namespace Team.Services.ChangeRequests;

public interface IChangeRequestService
{
    Task<PendingChangesResponse> GetPendingChangesAsync(Guid teamId, ClaimsPrincipal user, CancellationToken ct);
    Task<ChangeItemResponse> SetProjectChangeAsync(Guid teamId, ProjectChangeInput input, ClaimsPrincipal user, CancellationToken ct);
    Task DeleteProjectChangeAsync(Guid teamId, ClaimsPrincipal user, CancellationToken ct);
    Task<IReadOnlyList<ChangeRequestResponse>> ListForTeamAsync(Guid teamId, ClaimsPrincipal user, CancellationToken ct);
    Task<ChangeRequestResponse> SubmitAsync(Guid teamId, ChangeRequestInput input, ClaimsPrincipal user, CancellationToken ct);
    Task<PagedResponse<ChangeRequestResponse>> SearchAsync(ChangeRequestQuery query, CancellationToken ct);
    Task<ChangeRequestDetailResponse> GetDetailAsync(Guid id, ClaimsPrincipal user, CancellationToken ct);
    Task<ChangeRequestResponse> ApproveAsync(Guid id, ChangeRequestDecisionInput input, ClaimsPrincipal user, CancellationToken ct);
    Task<ChangeRequestResponse> RejectAsync(Guid id, ChangeRequestDecisionInput input, ClaimsPrincipal user, CancellationToken ct);
}

/// <summary>Luồng yêu cầu đổi nhóm EXE201 kế thừa (BE-45, rule 7).</summary>
public sealed class ChangeRequestService(
    IChangeRequestRepository changes, ITeamRepository teams, IAcademicClient academic,
    TeamReader reader, AuditLogWriter audit, TimeProvider clock) : IChangeRequestService
{
    public async Task<PendingChangesResponse> GetPendingChangesAsync(Guid teamId, ClaimsPrincipal user, CancellationToken ct)
    {
        var graph = await RequireTeamAccessAsync(teamId, user, ct);
        var baseline = await changes.ListBaselineMembersAsync(teamId, ct);
        var items = await changes.ListPendingItemsAsync(teamId, ct);
        var projected = ProjectMembers(graph.Members, items);
        var catalogs = await reader.LoadCatalogsAsync(ct);
        var project = graph.Project is { } p ? ToProjectSummary(p) : null;
        return new(baseline.Select(m => ToMember(m.StudentId, m.StudentCode, m.FullName, m.SpecializationId, m.Role, m.ImportedAt, catalogs)).ToList(),
            items.Select(i => ToItem(i, catalogs)).ToList(), project, projected.Count,
            projected.Select(m => m.SpecializationId).Distinct().Count());
    }

    public async Task<ChangeItemResponse> SetProjectChangeAsync(Guid teamId, ProjectChangeInput input, ClaimsPrincipal user, CancellationToken ct)
    {
        var graph = await RequireLeaderAsync(teamId, user, ct);
        EnsureInherited(graph.Team);
        await RequireChangeRequestRoundAsync(graph.Team, ct);
        if (graph.Team.ProjectId is null)
        {
            throw TeamErrors.Invalid("project", "Nhóm chưa có đề tài để thay đổi.");
        }
        var name = input.Name?.Trim() ?? string.Empty;
        if (name.Length is < 2 or > 200 || input.Description?.Length > 4000)
        {
            throw TeamErrors.Invalid("name", "Tên đề tài cần từ 2–200 ký tự; mô tả tối đa 4000 ký tự.");
        }
        if (await changes.HasPendingAsync(teamId, ct))
        {
            throw PendingRequestExists();
        }

        var item = await changes.GetPendingProjectChangeAsync(teamId, ct);
        var created = item is null;
        item ??= new ChangeRequestItem { Id = Guid.NewGuid(), TeamId = teamId, Action = "PROJECT_CHANGE", Source = "PROJECT" };
        (item.NewProjectName, item.NewProjectDescription) = (name, Clean(input.Description));
        if (created) changes.Add(item);
        audit.Add(teamId, "ChangeRequestItem", item.Id, "PROJECT_CHANGE_PENDING", user.ToAuditActor(),
            newValues: new { item.NewProjectName, item.NewProjectDescription });
        await changes.SaveChangesAsync(ct);
        return ToItem(item, await reader.LoadCatalogsAsync(ct));
    }

    public async Task DeleteProjectChangeAsync(Guid teamId, ClaimsPrincipal user, CancellationToken ct)
    {
        var graph = await RequireLeaderAsync(teamId, user, ct);
        EnsureInherited(graph.Team);
        await RequireChangeRequestRoundAsync(graph.Team, ct);
        if (await changes.HasPendingAsync(teamId, ct)) throw PendingRequestExists();
        var item = await changes.GetPendingProjectChangeAsync(teamId, ct) ?? throw new NotFoundException("thay đổi đề tài đang chờ");
        changes.Remove(item);
        audit.Add(teamId, "ChangeRequestItem", item.Id, "PROJECT_CHANGE_CANCELLED", user.ToAuditActor());
        await changes.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<ChangeRequestResponse>> ListForTeamAsync(Guid teamId, ClaimsPrincipal user, CancellationToken ct)
    {
        await RequireTeamAccessAsync(teamId, user, ct);
        var requests = await changes.ListByTeamAsync(teamId, ct);
        var graph = await teams.GetGraphAsync(teamId, ct) ?? throw TeamErrors.TeamNotFound(teamId);
        var card = TeamMapper.ToCard(graph, await reader.LoadCatalogsAsync(ct));
        var roundNames = await RoundNamesAsync(ct);
        return requests.Select(r => ToResponse(r, card, roundNames.GetValueOrDefault(r.RoundId))).ToList();
    }

    public async Task<ChangeRequestResponse> SubmitAsync(Guid teamId, ChangeRequestInput input, ClaimsPrincipal user, CancellationToken ct)
    {
        var graph = await RequireLeaderAsync(teamId, user, ct);
        EnsureInherited(graph.Team);
        var round = await RequireChangeRequestRoundAsync(graph.Team, ct);
        if (input.ItemIds is null || input.ItemIds.Count == 0 || input.ItemIds.Distinct().Count() != input.ItemIds.Count)
        {
            throw TeamErrors.Invalid("itemIds", "Chọn ít nhất một thay đổi duy nhất.");
        }
        var reason = input.Reason?.Trim() ?? string.Empty;
        if (reason.Length is < 10 or > 1000) throw TeamErrors.Invalid("reason", "Lý do cần từ 10–1000 ký tự.");
        if (await changes.HasPendingAsync(teamId, ct)) throw PendingRequestExists();

        var pending = await changes.ListPendingItemsAsync(teamId, ct);
        var selected = pending.Where(i => input.ItemIds.Contains(i.Id)).ToList();
        if (selected.Count != input.ItemIds.Count)
        {
            throw TeamErrors.Invalid("itemIds", "Có thay đổi không tồn tại, đã gửi hoặc không thuộc nhóm này.");
        }
        var baseline = await changes.ListBaselineMembersAsync(teamId, ct);
        if (baseline.Count == 0) throw TeamErrors.Invalid("teamId", "Nhóm chưa có mốc thành viên EXE101.");
        var projected = ProjectMembers(graph.Members, selected);
        if (projected.Count < TeamRules.MinSize)
        {
            throw TeamErrors.Invalid("itemIds", $"Sau thay đổi nhóm phải có ít nhất {TeamRules.MinSize} thành viên.");
        }
        if (projected.Count > TeamRules.MaxSize)
        {
            throw TeamErrors.Invalid("itemIds", $"Sau thay đổi nhóm không được quá {TeamRules.MaxSize} thành viên.");
        }

        var changedCount = selected.Count(i => i.Action is "ADD" or "REMOVE");
        var changedPercent = decimal.Round((decimal)changedCount * 100 / baseline.Count, 2, MidpointRounding.AwayFromZero);
        var request = new ChangeRequest
        {
            Id = Guid.NewGuid(), TeamId = teamId, RoundId = round.Id, RequestedByStudentId = user.GetStudentId()!.Value,
            Reason = reason, ChangedMemberCount = checked((short)changedCount), ChangedPercent = changedPercent,
            Status = "PENDING", CreatedAt = clock.GetUtcNow(), UpdatedAt = clock.GetUtcNow(),
        };
        foreach (var item in selected) item.ChangeRequestId = request.Id;
        changes.Add(request);
        audit.Add(teamId, "ChangeRequest", request.Id, "CHANGE_REQUEST_SUBMITTED", user.ToAuditActor(),
            newValues: new { request.ChangedMemberCount, request.ChangedPercent, includesProjectChange = selected.Any(i => i.Action == "PROJECT_CHANGE") }, reason: reason);
        changes.AddEvent("ChangeRequestSubmitted", request.Id, new
        {
            changeRequestId = request.Id, teamId, roundId = round.Id, changedMemberCount = request.ChangedMemberCount,
            changedPercent = request.ChangedPercent, includesProjectChange = selected.Any(i => i.Action == "PROJECT_CHANGE"),
        });
        await changes.SaveChangesAsync(ct);
        var card = TeamMapper.ToCard(graph, await reader.LoadCatalogsAsync(ct));
        return ToResponse(request, card, round.Name);
    }

    public async Task<PagedResponse<ChangeRequestResponse>> SearchAsync(ChangeRequestQuery query, CancellationToken ct)
    {
        var (items, total) = await changes.SearchAsync(query.Status, query.RoundId, query.Page, query.PageSize, ct);
        var graphs = await teams.ListGraphsAsync(items.Select(i => i.TeamId).Distinct().ToList(), ct);
        var catalogs = await reader.LoadCatalogsAsync(ct);
        var roundNames = await RoundNamesAsync(ct);
        var cards = graphs.ToDictionary(g => g.Team.Id, g => TeamMapper.ToCard(g, catalogs));
        var responses = items.Where(i => cards.ContainsKey(i.TeamId))
            .Select(i => ToResponse(i, cards[i.TeamId], roundNames.GetValueOrDefault(i.RoundId))).ToList();
        return new(responses, query.Page, query.PageSize, total, (total + query.PageSize - 1) / query.PageSize);
    }

    public async Task<ChangeRequestDetailResponse> GetDetailAsync(Guid id, ClaimsPrincipal user, CancellationToken ct)
    {
        var request = await changes.GetAsync(id, ct) ?? throw new NotFoundException("yêu cầu đổi nhóm", id);
        var graph = await RequireTeamAccessAsync(request.TeamId, user, ct);
        var items = await changes.ListItemsAsync(id, ct);
        var baseline = await changes.ListBaselineMembersAsync(request.TeamId, ct);
        var catalogs = await reader.LoadCatalogsAsync(ct);
        var roundNames = await RoundNamesAsync(ct);
        var before = baseline.Select(m => new TeamMember
        {
            Id = m.StudentId, TeamId = m.TeamId, SemesterId = graph.Team.SemesterId, StudentId = m.StudentId,
            StudentCode = m.StudentCode, FullName = m.FullName, SpecializationId = m.SpecializationId, Role = m.Role, JoinedAt = m.ImportedAt,
        }).ToList();
        var priorItems = await changes.ListApprovedItemsBeforeAsync(request.TeamId, request.CreatedAt, ct);
        var after = ProjectMembers(before, priorItems.Concat(items));
        var projectBefore = await ProjectBeforeAsync(graph, request, items, ct);
        var projectAfter = ProjectAfter(graph, request, items, projectBefore);
        return new(id, TeamMapper.ToCard(graph, catalogs), request.RoundId, roundNames.GetValueOrDefault(request.RoundId), request.Reason, request.ChangedMemberCount,
            request.ChangedPercent, request.Status, null, request.ReviewedAt, request.ReviewNote, request.CreatedAt,
            baseline.Select(m => ToMember(m.StudentId, m.StudentCode, m.FullName, m.SpecializationId, m.Role, m.ImportedAt, catalogs)).ToList(),
            after.Select(m => ToMember(m.StudentId, m.StudentCode, m.FullName, m.SpecializationId, m.Role, m.JoinedAt, catalogs)).ToList(),
            items.Select(i => ToItem(i, catalogs)).ToList(), projectBefore, projectAfter);
    }

    public Task<ChangeRequestResponse> ApproveAsync(Guid id, ChangeRequestDecisionInput input, ClaimsPrincipal user, CancellationToken ct) =>
        ReviewAsync(id, input, user, approve: true, ct);

    public Task<ChangeRequestResponse> RejectAsync(Guid id, ChangeRequestDecisionInput input, ClaimsPrincipal user, CancellationToken ct) =>
        ReviewAsync(id, input, user, approve: false, ct);

    private async Task<ChangeRequestResponse> ReviewAsync(Guid id, ChangeRequestDecisionInput input, ClaimsPrincipal user, bool approve, CancellationToken ct)
    {
        var request = await changes.GetAsync(id, ct) ?? throw new NotFoundException("yêu cầu đổi nhóm", id);
        if (request.Status != "PENDING") throw new ConflictException("request-not-pending", "Yêu cầu không còn chờ duyệt", "Chỉ xử lý yêu cầu đang chờ.");
        var graph = await teams.GetGraphAsync(request.TeamId, ct) ?? throw TeamErrors.TeamNotFound(request.TeamId);
        var items = await changes.ListItemsAsync(id, ct);
        var note = Clean(input.Note);
        if (!approve && note is null) throw TeamErrors.Invalid("note", "Ghi chú từ chối là bắt buộc.");
        if (note?.Length > 1000) throw TeamErrors.Invalid("note", "Ghi chú tối đa 1000 ký tự.");

        var currentMembers = await changes.ListMembersAsync(request.TeamId, ct);
        var memberStudentIds = currentMembers.Select(m => m.StudentId)
            .Concat(items.Where(i => i.StudentId is not null).Select(i => i.StudentId!.Value)).Distinct().ToList();
        var lecturerId = (await changes.GetAssignedLecturerAsync(request.TeamId, ct))?.LecturerId;
        if (approve)
        {
            var current = currentMembers;
            var projected = ProjectMembers(current, items);
            if (projected.Count < TeamRules.MinSize)
                throw TeamErrors.Invalid("itemIds", $"Sau thay đổi nhóm phải có ít nhất {TeamRules.MinSize} thành viên.");
            if (projected.Count > TeamRules.MaxSize)
                throw TeamErrors.Invalid("itemIds", $"Sau thay đổi nhóm không được quá {TeamRules.MaxSize} thành viên.");
            await ApplyApprovedItemsAsync(graph.Team, current, items, request, user, ct);
            (request.Status, request.ReviewedByAccountId, request.ReviewedAt, request.ReviewNote) =
                ("APPROVED", user.GetUserId(), clock.GetUtcNow(), note);
            audit.Add(request.TeamId, "ChangeRequest", request.Id, "CHANGE_REQUEST_APPROVED", user.ToAuditActor(),
                newValues: new { request.ChangedMemberCount, request.ChangedPercent }, reason: request.Reason);
            changes.AddEvent("ChangeRequestApproved", request.Id, new
            {
                changeRequestId = request.Id, teamId = request.TeamId, memberStudentIds, lecturerId, reviewNote = note,
            });
        }
        else
        {
            (request.Status, request.ReviewedByAccountId, request.ReviewedAt, request.ReviewNote) =
                ("REJECTED", user.GetUserId(), clock.GetUtcNow(), note);
            audit.Add(request.TeamId, "ChangeRequest", request.Id, "CHANGE_REQUEST_REJECTED", user.ToAuditActor(),
                newValues: new { request.ChangedMemberCount, request.ChangedPercent, reviewNote = note }, reason: request.Reason);
            changes.AddEvent("ChangeRequestRejected", request.Id, new
            {
                changeRequestId = request.Id, teamId = request.TeamId, memberStudentIds, lecturerId, reviewNote = note,
            });
        }
        await changes.SaveChangesAsync(ct);
        var card = TeamMapper.ToCard(await teams.GetGraphAsync(request.TeamId, ct) ?? throw TeamErrors.TeamNotFound(request.TeamId),
            await reader.LoadCatalogsAsync(ct));
        var roundNames = await RoundNamesAsync(ct);
        return ToResponse(request, card, roundNames.GetValueOrDefault(request.RoundId));
    }

    private async Task ApplyApprovedItemsAsync(TeamEntity team, List<TeamMember> current, IReadOnlyList<ChangeRequestItem> items,
        ChangeRequest request, ClaimsPrincipal user, CancellationToken ct)
    {
        var count = current.Count;
        var ordered = items.OrderBy(i => ActionPriority(i.Action)).ThenBy(i => i.CreatedAt).ToList();
        foreach (var item in ordered)
        {
            switch (item.Action)
            {
                case "ADD":
                {
                    if (item.StudentId is null || item.SpecializationId is null || item.StudentCode is null || item.FullName is null)
                        throw TeamErrors.Invalid("items", "Thay đổi thêm thành viên thiếu thông tin.");
                    if (await teams.IsStudentInTeamAsync(team.SemesterId, item.StudentId.Value, ct)) throw TeamErrors.AlreadyInTeam();
                    var member = new TeamMember
                    {
                        Id = Guid.NewGuid(), TeamId = team.Id, SemesterId = team.SemesterId, StudentId = item.StudentId.Value,
                        StudentCode = item.StudentCode, FullName = item.FullName, SpecializationId = item.SpecializationId.Value,
                        Role = MemberRoles.Member, JoinSource = "CHANGE_REQUEST", JoinedAt = clock.GetUtcNow(),
                    };
                    changes.Add(member);
                    current.Add(member);
                    count++;
                    audit.Add(team.Id, "TeamMember", member.StudentId, "MEMBER_JOINED", user.ToAuditActor(),
                        newValues: new { member.StudentCode, source = "CHANGE_REQUEST", memberCount = count });
                    teams.AddEvent(MemberEventTypes.MemberJoined, team.Id,
                        new MemberJoined(team.Id, team.SemesterId, member.StudentId, member.FullName, member.SpecializationId, "CHANGE_REQUEST", count));
                    break;
                }
                case "REMOVE":
                {
                    var member = current.FirstOrDefault(m => m.StudentId == item.StudentId) ?? throw TeamErrors.MemberNotFound(item.StudentId ?? Guid.Empty);
                    if (member.Role == MemberRoles.Leader) throw TeamErrors.LeaderMustTransfer();
                    changes.Remove(member);
                    current.Remove(member);
                    count--;
                    audit.Add(team.Id, "TeamMember", member.StudentId, "MEMBER_REMOVED", user.ToAuditActor(),
                        oldValues: new { member.StudentCode, member.Role }, newValues: new { memberCount = count }, reason: request.Reason);
                    teams.AddEvent(MemberEventTypes.MemberLeft, team.Id,
                        new MemberLeft(team.Id, team.SemesterId, member.StudentId, "CHANGE_REQUEST", count));
                    break;
                }
                case "SET_LEADER":
                {
                    var member = current.FirstOrDefault(m => m.StudentId == item.StudentId) ?? throw TeamErrors.MemberNotFound(item.StudentId ?? Guid.Empty);
                    var oldLeader = current.FirstOrDefault(m => m.Role == MemberRoles.Leader);
                    if (oldLeader is not null && oldLeader.StudentId != member.StudentId) oldLeader.Role = MemberRoles.Member;
                    member.Role = MemberRoles.Leader;
                    teams.AddEvent(MemberEventTypes.LeaderChanged, team.Id,
                        new LeaderChanged(team.Id, oldLeader?.StudentId ?? Guid.Empty, member.StudentId));
                    break;
                }
                case "PROJECT_CHANGE":
                    await ApplyProjectChangeAsync(team, item, request, user, ct);
                    break;
                default:
                    throw TeamErrors.Invalid("items", $"Loại thay đổi {item.Action} không được hỗ trợ.");
            }
        }
        team.Status = TeamRules.StatusAfterMemberChange(team.Status, count);
    }

    private async Task ApplyProjectChangeAsync(TeamEntity team, ChangeRequestItem item, ChangeRequest request, ClaimsPrincipal user, CancellationToken ct)
    {
        var project = await changes.GetProjectAsync(team.ProjectId, ct) ?? throw new NotFoundException("đề tài của nhóm");
        if (string.IsNullOrWhiteSpace(item.NewProjectName)) throw TeamErrors.Invalid("projectName", "Tên đề tài mới không hợp lệ.");
        project.Name = item.NewProjectName.Trim();
        project.Description = Clean(item.NewProjectDescription);
        project.CurrentVersion++;
        var actorId = user.GetUserId();
        changes.Add(new ProjectVersion
        {
            ProjectId = project.Id, Version = project.CurrentVersion, Name = project.Name, Description = project.Description,
            ChangeType = "PIVOT", Reason = request.Reason, SemesterId = team.SemesterId, CourseCode = team.CourseCode,
            ChangeRequestId = request.Id, ChangedByAccountId = actorId, CreatedAt = clock.GetUtcNow(),
        });
        teams.AddEvent(TeamEventTypes.ProjectChanged, team.Id,
            new ProjectChanged(project.Id, team.Id, project.CurrentVersion, "PIVOT", project.Name, request.Reason, request.Id));
        audit.Add(team.Id, "Project", project.Id, "PROJECT_PIVOT_APPROVED", user.ToAuditActor(),
            oldValues: new { version = project.CurrentVersion - 1 },
            newValues: new { project.Name, project.Description, project.CurrentVersion, request.Id }, reason: request.Reason);
    }

    private async Task<RoundDto> RequireChangeRequestRoundAsync(TeamEntity team, CancellationToken ct)
    {
        var round = await academic.GetOpenRoundAsync(team.CourseCode, RoundTypes.ChangeRequest, ct);
        if (round is null || round.SemesterId != team.SemesterId) throw TeamErrors.RoundClosed();
        return round;
    }

    private async Task<TeamGraph> RequireLeaderAsync(Guid teamId, ClaimsPrincipal user, CancellationToken ct)
    {
        var graph = await teams.GetGraphAsync(teamId, ct) ?? throw TeamErrors.TeamNotFound(teamId);
        var studentId = user.GetStudentId() ?? throw TeamErrors.StudentsOnly();
        var leader = graph.Members.FirstOrDefault(m => m.StudentId == studentId && m.Role == MemberRoles.Leader);
        if (leader is null) throw TeamErrors.NotLeader();
        return graph;
    }

    private async Task<TeamGraph> RequireTeamAccessAsync(Guid teamId, ClaimsPrincipal user, CancellationToken ct)
    {
        var graph = await teams.GetGraphAsync(teamId, ct) ?? throw TeamErrors.TeamNotFound(teamId);
        if (!user.IsInRole(Roles.Admin) && !graph.Members.Any(m => m.StudentId == user.GetStudentId())) throw TeamErrors.NotMember();
        return graph;
    }

    private static void EnsureInherited(TeamEntity team)
    {
        if (!team.IsInherited || team.CourseCode != "EXE201")
            throw new ConflictException("change-request-not-allowed", "Không thể đổi nhóm", "Yêu cầu đổi nhóm chỉ áp dụng cho nhóm EXE201 kế thừa.");
    }

    private static ConflictException PendingRequestExists() =>
        new("pending-exists", "Đã có yêu cầu đang chờ", "Nhóm chỉ có thể có một yêu cầu đổi nhóm đang chờ xử lý.");

    private static List<TeamMember> ProjectMembers(IEnumerable<TeamMember> members, IEnumerable<ChangeRequestItem> items)
    {
        var projected = members.Select(m => new TeamMember
        {
            Id = m.Id, TeamId = m.TeamId, SemesterId = m.SemesterId, StudentId = m.StudentId,
            StudentCode = m.StudentCode, FullName = m.FullName, SpecializationId = m.SpecializationId,
            Role = m.Role, JoinSource = m.JoinSource, JoinedAt = m.JoinedAt, Version = m.Version,
        }).ToDictionary(m => m.StudentId);
        foreach (var item in items.OrderBy(i => i.CreatedAt))
        {
            if (item.StudentId is not { } studentId) continue;
            if (item.Action == "ADD" && !projected.ContainsKey(studentId))
            {
                projected[studentId] = new TeamMember
                {
                    Id = studentId, TeamId = item.TeamId, StudentId = studentId, StudentCode = item.StudentCode ?? string.Empty,
                    FullName = item.FullName ?? string.Empty, SpecializationId = item.SpecializationId ?? 0,
                    Role = MemberRoles.Member, JoinedAt = item.CreatedAt,
                };
            }
            else if (item.Action == "REMOVE") projected.Remove(studentId);
            else if (item.Action == "SET_LEADER" && projected.TryGetValue(studentId, out var leader))
            {
                foreach (var member in projected.Values) member.Role = MemberRoles.Member;
                leader.Role = MemberRoles.Leader;
            }
        }
        return projected.Values.OrderByDescending(m => m.Role == MemberRoles.Leader).ThenBy(m => m.JoinedAt).ToList();
    }

    private async Task<ProjectSummary?> ProjectBeforeAsync(TeamGraph graph, ChangeRequest request, IReadOnlyList<ChangeRequestItem> items, CancellationToken ct)
    {
        var project = await changes.GetProjectAsync(graph.Team.ProjectId, ct);
        if (project is null) return null;
        if (request.Status == "APPROVED" && items.Any(i => i.Action == "PROJECT_CHANGE"))
        {
            var changed = await changes.GetProjectVersionForRequestAsync(request.Id, ct);
            if (changed is not null && changed.Version > 1)
            {
                var before = await changes.GetProjectVersionAsync(project.Id, (short)(changed.Version - 1), ct);
                if (before is not null) return new(project.Id, before.Name, before.Description, before.Version, before.CourseCode, null);
            }
        }
        return ToProjectSummary(project);
    }

    private static ProjectSummary? ProjectAfter(TeamGraph graph, ChangeRequest request, IReadOnlyList<ChangeRequestItem> items, ProjectSummary? before)
    {
        var change = items.FirstOrDefault(i => i.Action == "PROJECT_CHANGE");
        if (change is null || before is null) return null;
        var version = request.Status == "APPROVED" ? before.CurrentVersion + 1 : before.CurrentVersion;
        return new(before.Id, change.NewProjectName ?? before.Name, change.NewProjectDescription, version, before.OriginCourseCode, before.OriginSemesterCode);
    }

    private static MemberResponse ToMember(Guid id, string code, string name, int specializationId, string role, DateTimeOffset joinedAt, Catalogs catalogs) =>
        new(id, code, name, catalogs.Specialization(specializationId), role, null, null, joinedAt, null);

    private static ChangeItemResponse ToItem(ChangeRequestItem item, Catalogs catalogs) => new(item.Id, item.Action, item.Source,
        item.StudentId is { } studentId
            ? new StudentRef(studentId, item.StudentCode ?? string.Empty, item.FullName ?? string.Empty,
                catalogs.Specialization(item.SpecializationId ?? 0))
            : null,
        item.NewProjectName, item.NewProjectDescription, item.CreatedAt);

    private static ProjectSummary ToProjectSummary(Project project) =>
        new(project.Id, project.Name, project.Description, project.CurrentVersion, project.OriginCourseCode, null);

    private async Task<Dictionary<Guid, string>> RoundNamesAsync(CancellationToken ct) =>
        (await academic.ListChangeRequestRoundsAsync(ct)).Where(r => r.Name is not null)
            .ToDictionary(r => r.Id, r => r.Name!);

    private static ChangeRequestResponse ToResponse(ChangeRequest request, TeamCardResponse card, string? roundName) =>
        new(request.Id, card, request.RoundId, roundName, request.Reason, request.ChangedMemberCount, request.ChangedPercent,
            request.Status, null, request.ReviewedAt, request.ReviewNote, request.CreatedAt);

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static int ActionPriority(string action) => action switch
    {
        "SET_LEADER" => 0,
        "REMOVE" => 1,
        "ADD" => 2,
        "PROJECT_CHANGE" => 3,
        _ => 4
    };
}
