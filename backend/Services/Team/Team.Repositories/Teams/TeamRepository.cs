using BuildingBlocks.Messaging;
using BuildingBlocks.Outbox;
using Microsoft.EntityFrameworkCore;
using Team.Repositories.ChangeRequests;
using Team.Repositories.Context;
using Team.Repositories.Invitations;
using Team.Repositories.JoinRequests;
using Team.Repositories.LecturerAssignments;
using Team.Repositories.LecturerPreferences;
using Team.Repositories.Members;
using Team.Repositories.MentorAssignments;
using Team.Repositories.Posts;
using Team.Repositories.Projects;
using Team.Repositories.TopicInterests;

namespace Team.Repositories.Teams;

/// <summary>Bộ lọc danh sách nhóm (S05, W15). <see cref="MaxSize"/> = sĩ số tối đa (rule 1) để tính số chỗ còn trống.</summary>
public sealed record TeamSearch(
    Guid SemesterId, string? CourseCode, string? Search, string? DomainCode, int? MissingSpecializationId, int? MinOpenSlots,
    string? Status, bool InvalidOnly, bool HideFullAndDisbanded, int MaxSize, int MinSize, int MinSpecializations,
    string Sort, int Page, int PageSize);

/// <summary>Nhóm kèm dữ liệu để vẽ thẻ / chi tiết.</summary>
public sealed record TeamGraph(
    TeamEntity Team, IReadOnlyList<TeamMember> Members, IReadOnlyList<TeamDomain> Domains, Project? Project,
    string? LecturerName, string? MentorName);

/// <summary>Quan hệ của người xem với nhóm và dữ liệu phụ của chi tiết nhóm (S08, S15).</summary>
public sealed record TeamDetailExtras(
    RecruitmentPost? OpenPost, IReadOnlyList<int> PostSpecializationIds, bool HasPendingChanges, bool ViewerInvited, bool ViewerRequested);

/// <summary>Số liệu trang chủ sinh viên (S01).</summary>
public sealed record TeamHomeStats(
    int PendingJoinRequests, int InterestedLecturers, bool HasLecturer, bool HasLecturerPreferences,
    bool HasUnsubmittedChanges, bool HasPendingChangeRequest);

public interface ITeamRepository
{
    /// <summary>Nhóm để sửa (được theo dõi).</summary>
    Task<TeamEntity?> GetAsync(Guid teamId, CancellationToken ct);

    Task<TeamGraph?> GetGraphAsync(Guid teamId, CancellationToken ct);

    /// <summary>Theo đúng thứ tự <paramref name="teamIds"/>.</summary>
    Task<List<TeamGraph>> ListGraphsAsync(IReadOnlyList<Guid> teamIds, CancellationToken ct);

    Task<(List<Guid> Ids, int TotalCount)> SearchAsync(TeamSearch search, CancellationToken ct);

    /// <summary>Nhóm mới nhất (chưa giải tán) của sinh viên.</summary>
    Task<Guid?> FindTeamIdOfStudentAsync(Guid studentId, CancellationToken ct);

    /// <summary>Rule 4: sinh viên đã có nhóm trong học kỳ.</summary>
    Task<bool> IsStudentInTeamAsync(Guid semesterId, Guid studentId, CancellationToken ct);

    Task<bool> IsNameTakenAsync(Guid semesterId, string courseCode, string name, Guid? exceptTeamId, CancellationToken ct);

    Task<List<TeamDomain>> ListDomainsAsync(Guid teamId, CancellationToken ct);

    /// <summary>Đề tài để sửa (được theo dõi).</summary>
    Task<Project?> GetProjectAsync(Guid projectId, CancellationToken ct);

    Task<TeamDetailExtras> GetDetailExtrasAsync(Guid teamId, Guid? viewerStudentId, DateTimeOffset now, CancellationToken ct);

    Task<TeamHomeStats> GetHomeStatsAsync(Guid teamId, DateTimeOffset now, CancellationToken ct);

    /// <summary>Lời mời đang chờ gửi tới sinh viên (chưa hết hạn).</summary>
    Task<int> CountPendingInvitationsAsync(Guid studentId, DateTimeOffset now, CancellationToken ct);

    void Add(object entity);

    void Remove(object entity);

    /// <summary>Ghi sự kiện exe.team vào outbox (key = teamId), cùng transaction với <see cref="SaveChangesAsync"/>.</summary>
    void AddEvent<TPayload>(string eventType, Guid teamId, TPayload payload);

    Task SaveChangesAsync(CancellationToken ct);
}

public sealed class TeamRepository(TeamDbContext db) : ITeamRepository
{
    private const string Pending = "PENDING";

    public Task<TeamEntity?> GetAsync(Guid teamId, CancellationToken ct) =>
        db.Set<TeamEntity>().FirstOrDefaultAsync(t => t.Id == teamId, ct);

    public async Task<TeamGraph?> GetGraphAsync(Guid teamId, CancellationToken ct) =>
        (await ListGraphsAsync([teamId], ct)).FirstOrDefault();

    public async Task<List<TeamGraph>> ListGraphsAsync(IReadOnlyList<Guid> teamIds, CancellationToken ct)
    {
        var teams = await db.Set<TeamEntity>().AsNoTracking().Where(t => teamIds.Contains(t.Id)).ToListAsync(ct);
        var members = (await db.Set<TeamMember>().AsNoTracking().Where(m => teamIds.Contains(m.TeamId)).ToListAsync(ct)).ToLookup(m => m.TeamId);
        var domains = (await db.Set<TeamDomain>().AsNoTracking().Where(d => teamIds.Contains(d.TeamId)).ToListAsync(ct)).ToLookup(d => d.TeamId);
        var projectIds = teams.Where(t => t.ProjectId is not null).Select(t => t.ProjectId!.Value).ToList();
        var projects = await db.Set<Project>().AsNoTracking().Where(p => projectIds.Contains(p.Id)).ToDictionaryAsync(p => p.Id, ct);
        var lecturers = await db.Set<LecturerAssignment>().AsNoTracking()
            .Where(a => teamIds.Contains(a.TeamId) && a.Status == "ASSIGNED").ToDictionaryAsync(a => a.TeamId, a => a.LecturerName, ct);
        var mentors = await db.Set<MentorAssignment>().AsNoTracking()
            .Where(a => teamIds.Contains(a.TeamId) && a.Status == "ASSIGNED").ToDictionaryAsync(a => a.TeamId, a => a.MentorName, ct);

        var byId = teams.ToDictionary(t => t.Id);
        return teamIds.Where(byId.ContainsKey).Select(id => new TeamGraph(
            byId[id],
            members[id].OrderByDescending(m => m.Role == "LEADER").ThenBy(m => m.JoinedAt).ToList(),
            domains[id].OrderByDescending(d => d.IsPrimary).ThenBy(d => d.DomainCode).ToList(),
            byId[id].ProjectId is { } projectId ? projects.GetValueOrDefault(projectId) : null,
            lecturers.GetValueOrDefault(id),
            mentors.GetValueOrDefault(id))).ToList();
    }

    public async Task<(List<Guid> Ids, int TotalCount)> SearchAsync(TeamSearch s, CancellationToken ct)
    {
        var members = db.Set<TeamMember>();
        var query = db.Set<TeamEntity>().AsNoTracking().Where(t => t.SemesterId == s.SemesterId);
        if (s.CourseCode is not null)
        {
            query = query.Where(t => t.CourseCode == s.CourseCode);
        }
        if (s.HideFullAndDisbanded)
        {
            query = query.Where(t => t.MemberCount < s.MaxSize && t.Status != "DISBANDED");
        }
        if (s.Status is not null)
        {
            query = query.Where(t => t.Status == s.Status);
        }
        if (!string.IsNullOrWhiteSpace(s.Search))
        {
            var pattern = "%" + s.Search.Trim().Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_") + "%";
            query = query.Where(t => EF.Functions.ILike(t.Name, pattern)
                || db.Set<Project>().Any(p => p.Id == t.ProjectId && EF.Functions.ILike(p.Name, pattern)));
        }
        if (s.DomainCode is not null)
        {
            query = query.Where(t => db.Set<TeamDomain>().Any(d => d.TeamId == t.Id && d.DomainCode == s.DomainCode));
        }
        if (s.MissingSpecializationId is { } specializationId)
        {
            query = query.Where(t => !members.Any(m => m.TeamId == t.Id && m.SpecializationId == specializationId));
        }
        if (s.MinOpenSlots is { } minOpen)
        {
            query = query.Where(t => s.MaxSize - t.MemberCount >= minOpen);
        }
        if (s.InvalidOnly)
        {
            query = query.Where(t => t.MemberCount < s.MinSize
                || members.Where(m => m.TeamId == t.Id).Select(m => m.SpecializationId).Distinct().Count() < s.MinSpecializations);
        }

        var total = await query.CountAsync(ct);
        query = s.Sort switch
        {
            "name" => query.OrderBy(t => t.Name).ThenBy(t => t.Id),
            "openSlots" => query.OrderByDescending(t => t.MemberCount).ThenBy(t => t.Id),
            "-openSlots" => query.OrderBy(t => t.MemberCount).ThenBy(t => t.Id),
            _ => query.OrderByDescending(t => t.CreatedAt).ThenBy(t => t.Id), // -createdAt; relevance chưa có điểm riêng
        };
        var ids = await query.Skip((s.Page - 1) * s.PageSize).Take(s.PageSize).Select(t => t.Id).ToListAsync(ct);
        return (ids, total);
    }

    public Task<Guid?> FindTeamIdOfStudentAsync(Guid studentId, CancellationToken ct) =>
        db.Set<TeamMember>().AsNoTracking()
            .Where(m => m.StudentId == studentId && db.Set<TeamEntity>().Any(t => t.Id == m.TeamId && t.Status != "DISBANDED"))
            .OrderByDescending(m => m.JoinedAt)
            .Select(m => (Guid?)m.TeamId)
            .FirstOrDefaultAsync(ct);

    public Task<bool> IsStudentInTeamAsync(Guid semesterId, Guid studentId, CancellationToken ct) =>
        db.Set<TeamMember>().AnyAsync(m => m.SemesterId == semesterId && m.StudentId == studentId, ct);

    public Task<bool> IsNameTakenAsync(Guid semesterId, string courseCode, string name, Guid? exceptTeamId, CancellationToken ct)
    {
        var lowered = name.ToLower();
        return db.Set<TeamEntity>().AnyAsync(t => t.SemesterId == semesterId && t.CourseCode == courseCode
            && t.Status != "DISBANDED" && t.Name.ToLower() == lowered && t.Id != exceptTeamId, ct);
    }

    public Task<List<TeamDomain>> ListDomainsAsync(Guid teamId, CancellationToken ct) =>
        db.Set<TeamDomain>().Where(d => d.TeamId == teamId).ToListAsync(ct);

    public Task<Project?> GetProjectAsync(Guid projectId, CancellationToken ct) =>
        db.Set<Project>().FirstOrDefaultAsync(p => p.Id == projectId, ct);

    public async Task<TeamDetailExtras> GetDetailExtrasAsync(Guid teamId, Guid? viewerStudentId, DateTimeOffset now, CancellationToken ct)
    {
        var post = await db.Set<RecruitmentPost>().AsNoTracking().FirstOrDefaultAsync(p => p.TeamId == teamId && p.Status == "OPEN", ct);
        var postSpecializations = post is null ? [] : await db.Set<RecruitmentPostSpecialization>()
            .Where(s => s.RecruitmentPostId == post.Id).Select(s => s.SpecializationId).ToListAsync(ct);
        var hasPendingChanges = await db.Set<ChangeRequestItem>().AnyAsync(i => i.TeamId == teamId && i.ChangeRequestId == null, ct)
            || await db.Set<ChangeRequest>().AnyAsync(r => r.TeamId == teamId && r.Status == Pending, ct);
        var invited = viewerStudentId is { } viewer && await db.Set<Invitation>()
            .AnyAsync(i => i.TeamId == teamId && i.InviteeStudentId == viewer && i.Status == Pending && i.ExpiresAt > now, ct);
        var requested = viewerStudentId is { } requester && await db.Set<JoinRequest>()
            .AnyAsync(r => r.TeamId == teamId && r.StudentId == requester && r.Status == Pending && r.ExpiresAt > now, ct);
        return new TeamDetailExtras(post, postSpecializations, hasPendingChanges, invited, requested);
    }

    public async Task<TeamHomeStats> GetHomeStatsAsync(Guid teamId, DateTimeOffset now, CancellationToken ct) => new(
        await db.Set<JoinRequest>().CountAsync(r => r.TeamId == teamId && r.Status == Pending && r.ExpiresAt > now, ct),
        await db.Set<TopicInterest>().CountAsync(i => i.TeamId == teamId && i.AdvisorKind == "LECTURER" && i.Status == "OPEN", ct),
        await db.Set<LecturerAssignment>().AnyAsync(a => a.TeamId == teamId && a.Status == "ASSIGNED", ct),
        await db.Set<LecturerPreference>().AnyAsync(p => p.TeamId == teamId, ct),
        await db.Set<ChangeRequestItem>().AnyAsync(i => i.TeamId == teamId && i.ChangeRequestId == null, ct),
        await db.Set<ChangeRequest>().AnyAsync(r => r.TeamId == teamId && r.Status == Pending, ct));

    public Task<int> CountPendingInvitationsAsync(Guid studentId, DateTimeOffset now, CancellationToken ct) =>
        db.Set<Invitation>().CountAsync(i => i.InviteeStudentId == studentId && i.Status == Pending && i.ExpiresAt > now, ct);

    public void Add(object entity) => db.Add(entity);

    public void Remove(object entity) => db.Remove(entity);

    public void AddEvent<TPayload>(string eventType, Guid teamId, TPayload payload) =>
        db.AddOutboxMessage(Topics.Team, eventType, "Team", teamId, payload);

    public Task SaveChangesAsync(CancellationToken ct) => db.SaveChangesAsync(ct);
}
