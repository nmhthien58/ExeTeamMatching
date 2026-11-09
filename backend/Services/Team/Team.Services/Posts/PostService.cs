using System.Security.Claims;
using BuildingBlocks.Auth;
using BuildingBlocks.Errors;
using Team.Repositories.AuditLog;
using Team.Repositories.Members;
using Team.Repositories.Posts;
using Team.Repositories.Teams;
using Team.Services.Academic;
using Team.Services.Teams;

namespace Team.Services.Posts;

public interface IPostService
{
    /// <summary>S21 – trưởng nhóm tạo hoặc sửa bài tuyển đang mở (mỗi nhóm tối đa 1 bài mở); chỉ trong đợt lập nhóm (rule 18).</summary>
    Task<RecruitmentPostResponse> UpsertRecruitmentAsync(Guid teamId, RecruitmentPostInput input, ClaimsPrincipal user, CancellationToken ct);

    /// <summary>Trưởng nhóm đóng bài tuyển.</summary>
    Task CloseRecruitmentAsync(Guid teamId, ClaimsPrincipal user, CancellationToken ct);

    /// <summary>S10 – sinh viên chưa có nhóm đang tìm nhóm trong học kỳ hiện hành.</summary>
    Task<PagedResponse<SeekingPostResponse>> ListSeekingAsync(SeekingPostQuery query, ClaimsPrincipal user, CancellationToken ct);

    /// <summary>S11 – bài "Tôi đang tìm nhóm" đang mở của mình; 404 nếu chưa có.</summary>
    Task<SeekingPostResponse> GetMySeekingAsync(ClaimsPrincipal user, CancellationToken ct);

    /// <summary>S11 – tạo hoặc sửa bài; chỉ khi chưa có nhóm, trong đợt lập nhóm (rule 18).</summary>
    Task<SeekingPostResponse> UpsertMySeekingAsync(SeekingPostInput input, ClaimsPrincipal user, CancellationToken ct);

    /// <summary>Gỡ bài của mình.</summary>
    Task CloseMySeekingAsync(ClaimsPrincipal user, CancellationToken ct);
}

/// <summary>Module Posts của Team Service (BE-31).</summary>
public sealed class PostService(
    IPostRepository posts, ITeamRepository teams, IAcademicClient academic, RoundGuard rounds, TeamReader reader, AuditLogWriter audit,
    TimeProvider clock) : IPostService
{
    public async Task<RecruitmentPostResponse> UpsertRecruitmentAsync(Guid teamId, RecruitmentPostInput input, ClaimsPrincipal user, CancellationToken ct)
    {
        var team = await RequireLeaderAsync(teamId, user, ct);
        await rounds.RequireTeamFormationAsync(team.CourseCode, team.SemesterId, ct);
        var specializationIds = (input.SpecializationIds ?? []).Distinct().ToList();
        var catalog = (await academic.GetSpecializationsAsync(ct)).Where(s => s.IsActive).Select(s => s.Id).ToHashSet();
        if (specializationIds.Where(id => !catalog.Contains(id)).ToList() is [var unknown, ..])
        {
            throw TeamErrors.Invalid("specializationIds", $"Chuyên ngành {unknown} không có trong danh mục.");
        }

        var post = await posts.GetOpenRecruitmentAsync(teamId, ct);
        var created = post is null;
        if (post is null)
        {
            post = new RecruitmentPost { Id = Guid.NewGuid(), TeamId = teamId, Status = PostStatuses.Open, CreatedAt = clock.GetUtcNow() };
            posts.Add(post);
        }
        (post.Title, post.Description, post.UpdatedAt) = (input.Title.Trim(), Clean(input.Description), clock.GetUtcNow());
        await posts.ReplaceSpecializationsAsync(post.Id, specializationIds, ct);
        audit.Add(teamId, "RecruitmentPost", post.Id, created ? "RECRUITMENT_POST_OPENED" : "RECRUITMENT_POST_UPDATED", user.ToAuditActor(),
            newValues: new { post.Title, specializationIds });
        await posts.SaveChangesAsync(ct);
        return new RecruitmentPostResponse(post.Id, post.Title, post.Description, specializationIds.Order().ToList(), post.Status, post.UpdatedAt);
    }

    public async Task CloseRecruitmentAsync(Guid teamId, ClaimsPrincipal user, CancellationToken ct)
    {
        await RequireLeaderAsync(teamId, user, ct);
        var post = await posts.GetOpenRecruitmentAsync(teamId, ct) ?? throw new NotFoundException("bài tuyển đang mở của nhóm");
        Close(post);
        audit.Add(teamId, "RecruitmentPost", post.Id, "RECRUITMENT_POST_CLOSED", user.ToAuditActor());
        await posts.SaveChangesAsync(ct);
    }

    /// <summary>
    /// Mục "SV tìm nhóm": sinh viên cùng học kỳ (và cùng môn khi người xem là sinh viên) đang bật "Tôi đang tìm nhóm"
    /// trong hồ sơ và chưa có nhóm. Không cần đăng bài; <c>id</c> = id sinh viên, <c>title</c> rỗng, mô tả = giới thiệu trong hồ sơ.
    /// </summary>
    public async Task<PagedResponse<SeekingPostResponse>> ListSeekingAsync(SeekingPostQuery query, ClaimsPrincipal user, CancellationToken ct)
    {
        Guid semesterId;
        string? courseCode = null;
        Guid? viewerId = user.GetStudentId();
        if (viewerId is { } studentId)
        {
            var (semester, me) = await rounds.ResolveStudentAsync(studentId, ct);
            await rounds.RequireTeamFormationAsync(me.CourseCode, semester.Id, ct);
            (semesterId, courseCode) = (semester.Id, me.CourseCode);
        }
        else
        {
            semesterId = (await academic.GetCurrentSemesterAsync(ct))?.Id ?? throw new NotFoundException("học kỳ hiện hành");
        }

        var seeking = (await academic.ListSeekingStudentsAsync(semesterId, courseCode, ct))
            .Where(s => s.Id != viewerId && (query.SpecializationId is null || s.SpecializationId == query.SpecializationId))
            .ToList();
        var inTeam = await posts.StudentsInTeamAsync(semesterId, seeking.Select(s => s.Id).ToList(), ct);
        seeking = seeking.Where(s => !inTeam.Contains(s.Id)).ToList();

        var catalogs = await reader.LoadCatalogsAsync(ct);
        var now = clock.GetUtcNow();
        var items = seeking.Skip((query.Page - 1) * query.PageSize).Take(query.PageSize)
            .Select(s => new SeekingPostResponse(s.Id, string.Empty, s.Description, [],
                new StudentRef(s.Id, s.StudentCode, s.FullName, catalogs.Specialization(s.SpecializationId ?? 0)),
                s.Skills ?? [], PostStatuses.Open, now))
            .ToList();
        var total = seeking.Count;
        return new PagedResponse<SeekingPostResponse>(items, query.Page, query.PageSize, total, (total + query.PageSize - 1) / query.PageSize);
    }

    public async Task<SeekingPostResponse> GetMySeekingAsync(ClaimsPrincipal user, CancellationToken ct)
    {
        var studentId = user.GetStudentId() ?? throw TeamErrors.StudentsOnly();
        var semester = await academic.GetCurrentSemesterAsync(ct) ?? throw new NotFoundException("học kỳ hiện hành");
        var post = await posts.GetOpenSeekingAsync(semester.Id, studentId, ct) ?? throw MySeekingNotFound();
        return (await ToResponsesAsync(semester.Id, [post], ct)).Single();
    }

    public async Task<SeekingPostResponse> UpsertMySeekingAsync(SeekingPostInput input, ClaimsPrincipal user, CancellationToken ct)
    {
        var studentId = user.GetStudentId() ?? throw TeamErrors.StudentsOnly();
        var (semester, me) = await rounds.ResolveStudentAsync(studentId, ct);
        await rounds.RequireTeamFormationAsync(me.CourseCode, semester.Id, ct);
        if (await teams.IsStudentInTeamAsync(semester.Id, studentId, ct))
        {
            throw TeamErrors.AlreadyInTeam();
        }
        var domainIds = (input.DomainIds ?? []).Distinct().ToList();
        var catalog = (await academic.GetDomainsAsync(ct)).Where(d => d.IsActive).ToDictionary(d => d.Id);
        var domains = domainIds.Select(id => catalog.TryGetValue(id, out var d)
            ? (id, d.Code)
            : throw TeamErrors.Invalid("domainIds", $"Domain {id} không có trong danh mục.")).ToList();

        var post = await posts.GetOpenSeekingAsync(semester.Id, studentId, ct);
        if (post is null)
        {
            post = new SeekingPost { Id = Guid.NewGuid(), SemesterId = semester.Id, StudentId = studentId, Status = PostStatuses.Open, CreatedAt = clock.GetUtcNow() };
            posts.Add(post);
        }
        (post.SpecializationId, post.Title, post.Description, post.UpdatedAt) = (me.SpecializationId ?? 0, input.Title.Trim(), Clean(input.Description), clock.GetUtcNow());
        await posts.ReplaceDomainsAsync(post.Id, domains, ct);
        await posts.SaveChangesAsync(ct);
        return (await ToResponsesAsync(semester.Id, [post], ct)).Single();
    }

    public async Task CloseMySeekingAsync(ClaimsPrincipal user, CancellationToken ct)
    {
        var studentId = user.GetStudentId() ?? throw TeamErrors.StudentsOnly();
        var semester = await academic.GetCurrentSemesterAsync(ct) ?? throw new NotFoundException("học kỳ hiện hành");
        Close(await posts.GetOpenSeekingAsync(semester.Id, studentId, ct) ?? throw MySeekingNotFound());
        await posts.SaveChangesAsync(ct);
    }

    private void Close(RecruitmentPost post) => (post.Status, post.ClosedAt) = (PostStatuses.Closed, clock.GetUtcNow());

    private void Close(SeekingPost post) => (post.Status, post.ClosedAt) = (PostStatuses.Closed, clock.GetUtcNow());

    private async Task<TeamEntity> RequireLeaderAsync(Guid teamId, ClaimsPrincipal user, CancellationToken ct)
    {
        var graph = await teams.GetGraphAsync(teamId, ct);
        if (graph is null || graph.Team.Status == TeamStatuses.Disbanded)
        {
            throw TeamErrors.TeamNotFound(teamId);
        }
        var studentId = user.GetStudentId();
        return graph.Members.Any(m => m.StudentId == studentId && m.Role == MemberRoles.Leader) ? graph.Team : throw TeamErrors.NotLeader();
    }

    private async Task<List<SeekingPostResponse>> ToResponsesAsync(Guid semesterId, IReadOnlyList<SeekingPost> rows, CancellationToken ct)
    {
        if (rows.Count == 0)
        {
            return [];
        }
        var students = await reader.StudentsAsync(semesterId, rows.Select(p => p.StudentId), await reader.LoadCatalogsAsync(ct), ct);
        var domains = await posts.ListDomainsAsync(rows.Select(p => p.Id).ToList(), ct);
        return rows.Select(p => new SeekingPostResponse(p.Id, p.Title, p.Description, domains[p.Id].Select(d => d.DomainId).ToList(),
            students[p.StudentId], [], p.Status, p.UpdatedAt)).ToList();
    }

    private static string? Clean(string? text) => string.IsNullOrWhiteSpace(text) ? null : text.Trim();

    private static NotFoundException MySeekingNotFound() => new("bài tìm nhóm của bạn");
}
