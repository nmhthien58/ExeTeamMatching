using BuildingBlocks.Messaging;
using BuildingBlocks.Outbox;
using Microsoft.EntityFrameworkCore;
using Team.Repositories.Context;
using Team.Repositories.LecturerAssignments;
using Team.Repositories.LecturerPreferences;
using Team.Repositories.Members;
using Team.Repositories.MentorAssignments;
using Team.Repositories.Teams;
using Team.Repositories.TopicInterests;

namespace Team.Repositories.AdvisorMatching;

/// <summary>Truy vấn của module AdvisorMatching (BE-80, rule 9, 19, 20). Đọc team_db, ghi lần chạy và kết quả ghép.</summary>
public interface IAdvisorMatchingRepository
{
    /// <summary>Nhóm chưa giải tán của một môn trong học kỳ (không theo dõi), theo tên.</summary>
    Task<List<TeamEntity>> ListTeamsAsync(Guid semesterId, string courseCode, CancellationToken ct);

    /// <summary>Nhóm theo id (không theo dõi), bỏ nhóm đã giải tán.</summary>
    Task<List<TeamEntity>> ListTeamsAsync(IReadOnlyCollection<Guid> teamIds, CancellationToken ct);

    /// <summary>Trong <paramref name="teamIds"/>, nhóm đang có cố vấn loại <paramref name="advisorKind"/> (LECTURER / MENTOR).</summary>
    Task<HashSet<Guid>> ListTeamIdsWithAdvisorAsync(string advisorKind, IReadOnlyCollection<Guid> teamIds, CancellationToken ct);

    Task<ILookup<Guid, int>> ListDomainIdsAsync(IReadOnlyCollection<Guid> teamIds, CancellationToken ct);

    /// <summary>Nguyện vọng giảng viên theo nhóm, thứ tự rank 1 → 3.</summary>
    Task<ILookup<Guid, LecturerPreference>> ListLecturerPreferencesAsync(IReadOnlyCollection<Guid> teamIds, CancellationToken ct);

    /// <summary>Quan tâm loại <paramref name="advisorKind"/> theo nhóm, cũ trước; <paramref name="openOnly"/> chỉ lấy OPEN.</summary>
    Task<ILookup<Guid, TopicInterest>> ListInterestsAsync(string advisorKind, IReadOnlyCollection<Guid> teamIds, bool openOnly, CancellationToken ct);

    /// <summary>
    /// Giảng viên EXE101 cùng đề tài của nhóm EXE201 (rule 26): nhóm nguồn <c>source_team_id</c> trước, rồi nhóm EXE101 cùng <c>project_id</c>
    /// – cùng cách với <c>WasLecturerEXE101InstructorAsync</c>. Nhóm EXE101 hoặc không tìm thấy thì không có trong kết quả.
    /// </summary>
    Task<Dictionary<Guid, (Guid LecturerId, string LecturerName)>> ListPreviousInstructorsAsync(IReadOnlyCollection<TeamEntity> teams, CancellationToken ct);

    /// <summary>Số nhóm đang hướng dẫn của từng giảng viên trong môn và học kỳ (nhóm chưa giải tán).</summary>
    Task<Dictionary<Guid, int>> CountLecturerLoadsAsync(Guid semesterId, string courseCode, CancellationToken ct);

    /// <summary>Giảng viên đang hướng dẫn của các nhóm (không theo dõi).</summary>
    Task<Dictionary<Guid, LecturerAssignment>> ListActiveLecturerAssignmentsAsync(IReadOnlyCollection<Guid> teamIds, CancellationToken ct);

    /// <summary>Giảng viên đang hướng dẫn của nhóm (được theo dõi, để thu hồi).</summary>
    Task<LecturerAssignment?> GetActiveLecturerAssignmentAsync(Guid teamId, CancellationToken ct);

    /// <summary>
    /// Số nhóm đang mentor của từng mentor trong học kỳ, <b>tính chung hai môn</b> (số tối đa của mentor không chia theo môn – rule 20),
    /// nhóm chưa giải tán. Không dùng <c>currentTeamCount</c> của Academic.
    /// </summary>
    Task<Dictionary<Guid, int>> CountMentorLoadsAsync(Guid semesterId, CancellationToken ct);

    /// <summary>Mentor đang gán của các nhóm (không theo dõi).</summary>
    Task<Dictionary<Guid, MentorAssignment>> ListActiveMentorAssignmentsAsync(IReadOnlyCollection<Guid> teamIds, CancellationToken ct);

    /// <summary>Mentor đang gán của nhóm (được theo dõi, để kết thúc khi đổi / gỡ).</summary>
    Task<MentorAssignment?> GetActiveMentorAssignmentAsync(Guid teamId, CancellationToken ct);

    Task<ILookup<Guid, Guid>> ListMemberStudentIdsAsync(IReadOnlyCollection<Guid> teamIds, CancellationToken ct);

    /// <summary>Lần chạy theo id (được theo dõi).</summary>
    Task<AdvisorMatchingRun?> GetRunAsync(Guid id, CancellationToken ct);

    /// <summary>Đọc lại lần chạy từ DB (sau khi giữ khóa, để thấy trạng thái lệnh khác vừa ghi).</summary>
    Task ReloadAsync(AdvisorMatchingRun run, CancellationToken ct);

    /// <summary>Lần chạy mới nhất của loại cố vấn, môn, học kỳ; lọc thêm trigger / status nếu có.</summary>
    Task<AdvisorMatchingRun?> GetLatestRunAsync(string advisorKind, Guid semesterId, string courseCode, string? trigger, string? status, CancellationToken ct);

    /// <summary>
    /// Khóa <c>pg_advisory_xact_lock</c> theo (loại cố vấn, học kỳ, môn) tới hết transaction: ghép theo lịch, Ghép sớm và đổi tay
    /// lần lượt chạy, nên số nhóm của cố vấn đọc trong transaction không bị lệnh khác vượt mặt. Chỉ gọi trong <see cref="InTransactionAsync{T}"/>.
    /// </summary>
    Task LockAsync(string advisorKind, Guid semesterId, string courseCode, CancellationToken ct);

    void Add(object entity);

    /// <summary>Sự kiện topic <c>exe.assignment</c> qua Outbox; <paramref name="aggregateId"/> là message key.</summary>
    void AddEvent<TPayload>(string eventType, string aggregateType, Guid aggregateId, TPayload payload);

    Task SaveChangesAsync(CancellationToken ct);

    /// <summary>
    /// Chạy nhiều lần SaveChanges trong một transaction (thu hồi giảng viên cũ trước khi thêm người mới – <c>uq_lecturer_assignments_active</c>;
    /// đóng quan tâm có SaveChanges riêng). Lỗi ràng buộc lúc COMMIT đổi sang ProblemDetails như SaveChanges.
    /// </summary>
    Task<T> InTransactionAsync<T>(Func<Task<T>> work, CancellationToken ct);
}

public sealed class AdvisorMatchingRepository(TeamDbContext db) : IAdvisorMatchingRepository
{
    private const string Disbanded = "DISBANDED";
    private const string Assigned = "ASSIGNED";

    public Task<List<TeamEntity>> ListTeamsAsync(Guid semesterId, string courseCode, CancellationToken ct) =>
        db.Set<TeamEntity>().AsNoTracking()
            .Where(t => t.SemesterId == semesterId && t.CourseCode == courseCode && t.Status != Disbanded)
            .OrderBy(t => t.Name).ThenBy(t => t.Id)
            .ToListAsync(ct);

    public Task<List<TeamEntity>> ListTeamsAsync(IReadOnlyCollection<Guid> teamIds, CancellationToken ct) =>
        db.Set<TeamEntity>().AsNoTracking().Where(t => teamIds.Contains(t.Id) && t.Status != Disbanded).ToListAsync(ct);

    public async Task<HashSet<Guid>> ListTeamIdsWithAdvisorAsync(string advisorKind, IReadOnlyCollection<Guid> teamIds, CancellationToken ct)
    {
        var ids = advisorKind == "MENTOR"
            ? db.Set<MentorAssignment>().Where(a => teamIds.Contains(a.TeamId) && a.Status == Assigned).Select(a => a.TeamId)
            : db.Set<LecturerAssignment>().Where(a => teamIds.Contains(a.TeamId) && a.Status == Assigned).Select(a => a.TeamId);
        return (await ids.ToListAsync(ct)).ToHashSet();
    }

    public async Task<ILookup<Guid, int>> ListDomainIdsAsync(IReadOnlyCollection<Guid> teamIds, CancellationToken ct) =>
        (await db.Set<TeamDomain>().AsNoTracking().Where(d => teamIds.Contains(d.TeamId))
            .OrderByDescending(d => d.IsPrimary).ThenBy(d => d.DomainId).ToListAsync(ct))
        .ToLookup(d => d.TeamId, d => d.DomainId);

    public async Task<ILookup<Guid, LecturerPreference>> ListLecturerPreferencesAsync(IReadOnlyCollection<Guid> teamIds, CancellationToken ct) =>
        (await db.Set<LecturerPreference>().AsNoTracking().Where(p => teamIds.Contains(p.TeamId)).OrderBy(p => p.Rank).ToListAsync(ct))
        .ToLookup(p => p.TeamId);

    public async Task<ILookup<Guid, TopicInterest>> ListInterestsAsync(string advisorKind, IReadOnlyCollection<Guid> teamIds, bool openOnly,
        CancellationToken ct)
    {
        var query = db.Set<TopicInterest>().AsNoTracking().Where(i => teamIds.Contains(i.TeamId) && i.AdvisorKind == advisorKind);
        if (openOnly)
        {
            query = query.Where(i => i.Status == "OPEN");
        }
        return (await query.OrderBy(i => i.CreatedAt).ThenBy(i => i.Id).ToListAsync(ct)).ToLookup(i => i.TeamId);
    }

    public async Task<Dictionary<Guid, (Guid LecturerId, string LecturerName)>> ListPreviousInstructorsAsync(IReadOnlyCollection<TeamEntity> teams,
        CancellationToken ct)
    {
        var exe201 = teams.Where(t => t.CourseCode == "EXE201" && (t.SourceTeamId is not null || t.ProjectId is not null)).ToList();
        var result = new Dictionary<Guid, (Guid, string)>();
        if (exe201.Count == 0)
        {
            return result;
        }

        var sourceIds = exe201.Where(t => t.SourceTeamId is not null).Select(t => t.SourceTeamId!.Value).Distinct().ToList();
        var projectIds = exe201.Where(t => t.ProjectId is not null).Select(t => t.ProjectId!.Value).Distinct().ToList();
        var exe101ByProject = (await db.Set<TeamEntity>().AsNoTracking()
                .Where(t => t.CourseCode == "EXE101" && t.ProjectId != null && projectIds.Contains(t.ProjectId.Value))
                .Select(t => new { t.Id, ProjectId = t.ProjectId!.Value }).ToListAsync(ct))
            .ToLookup(t => t.ProjectId, t => t.Id);
        var candidateTeamIds = sourceIds.Concat(exe101ByProject.SelectMany(g => g)).Distinct().ToList();
        var assignments = (await db.Set<LecturerAssignment>().AsNoTracking()
                .Where(a => candidateTeamIds.Contains(a.TeamId) && a.Status == Assigned)
                .OrderByDescending(a => a.AssignedAt).ToListAsync(ct))
            .DistinctBy(a => a.TeamId)
            .ToDictionary(a => a.TeamId);

        foreach (var team in exe201)
        {
            var found = team.SourceTeamId is { } sourceId && assignments.TryGetValue(sourceId, out var bySource)
                ? bySource
                : team.ProjectId is { } projectId
                    ? exe101ByProject[projectId].Select(id => assignments.GetValueOrDefault(id)).Where(a => a is not null)
                        .OrderByDescending(a => a!.AssignedAt).FirstOrDefault()
                    : null;
            if (found is not null)
            {
                result[team.Id] = (found.LecturerId, found.LecturerName);
            }
        }
        return result;
    }

    public async Task<Dictionary<Guid, int>> CountLecturerLoadsAsync(Guid semesterId, string courseCode, CancellationToken ct) =>
        await (from assignment in db.Set<LecturerAssignment>()
               join team in db.Set<TeamEntity>() on assignment.TeamId equals team.Id
               where assignment.Status == Assigned && team.SemesterId == semesterId && team.CourseCode == courseCode && team.Status != Disbanded
               group assignment by assignment.LecturerId into g
               select new { LecturerId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.LecturerId, x => x.Count, ct);

    public Task<Dictionary<Guid, LecturerAssignment>> ListActiveLecturerAssignmentsAsync(IReadOnlyCollection<Guid> teamIds, CancellationToken ct) =>
        db.Set<LecturerAssignment>().AsNoTracking().Where(a => teamIds.Contains(a.TeamId) && a.Status == Assigned).ToDictionaryAsync(a => a.TeamId, ct);

    public Task<LecturerAssignment?> GetActiveLecturerAssignmentAsync(Guid teamId, CancellationToken ct) =>
        db.Set<LecturerAssignment>().FirstOrDefaultAsync(a => a.TeamId == teamId && a.Status == Assigned, ct);

    public async Task<Dictionary<Guid, int>> CountMentorLoadsAsync(Guid semesterId, CancellationToken ct) =>
        await (from assignment in db.Set<MentorAssignment>()
               join team in db.Set<TeamEntity>() on assignment.TeamId equals team.Id
               where assignment.Status == Assigned && team.SemesterId == semesterId && team.Status != Disbanded
               group assignment by assignment.MentorId into g
               select new { MentorId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.MentorId, x => x.Count, ct);

    public Task<Dictionary<Guid, MentorAssignment>> ListActiveMentorAssignmentsAsync(IReadOnlyCollection<Guid> teamIds, CancellationToken ct) =>
        db.Set<MentorAssignment>().AsNoTracking().Where(a => teamIds.Contains(a.TeamId) && a.Status == Assigned).ToDictionaryAsync(a => a.TeamId, ct);

    public Task<MentorAssignment?> GetActiveMentorAssignmentAsync(Guid teamId, CancellationToken ct) =>
        db.Set<MentorAssignment>().FirstOrDefaultAsync(a => a.TeamId == teamId && a.Status == Assigned, ct);

    public async Task<ILookup<Guid, Guid>> ListMemberStudentIdsAsync(IReadOnlyCollection<Guid> teamIds, CancellationToken ct) =>
        (await db.Set<TeamMember>().AsNoTracking().Where(m => teamIds.Contains(m.TeamId)).OrderBy(m => m.JoinedAt)
            .Select(m => new { m.TeamId, m.StudentId }).ToListAsync(ct))
        .ToLookup(m => m.TeamId, m => m.StudentId);

    public Task<AdvisorMatchingRun?> GetRunAsync(Guid id, CancellationToken ct) =>
        db.Set<AdvisorMatchingRun>().FirstOrDefaultAsync(r => r.Id == id, ct);

    public Task ReloadAsync(AdvisorMatchingRun run, CancellationToken ct) => db.Entry(run).ReloadAsync(ct);

    public Task<AdvisorMatchingRun?> GetLatestRunAsync(string advisorKind, Guid semesterId, string courseCode, string? trigger, string? status,
        CancellationToken ct)
    {
        var query = db.Set<AdvisorMatchingRun>().AsNoTracking()
            .Where(r => r.AdvisorKind == advisorKind && r.SemesterId == semesterId && r.CourseCode == courseCode);
        if (trigger is not null)
        {
            query = query.Where(r => r.Trigger == trigger);
        }
        if (status is not null)
        {
            query = query.Where(r => r.Status == status);
        }
        return query.OrderByDescending(r => r.CreatedAt).ThenByDescending(r => r.Id).FirstOrDefaultAsync(ct);
    }

    public async Task LockAsync(string advisorKind, Guid semesterId, string courseCode, CancellationToken ct)
    {
        var key = $"advisor-matching:{advisorKind}:{semesterId}:{courseCode}";
        await db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock(hashtext({key}))", ct);
    }

    public void Add(object entity) => db.Add(entity);

    public void AddEvent<TPayload>(string eventType, string aggregateType, Guid aggregateId, TPayload payload) =>
        db.AddOutboxMessage(Topics.Assignment, eventType, aggregateType, aggregateId, payload);

    public Task SaveChangesAsync(CancellationToken ct) => db.SaveChangesAsync(ct);

    public async Task<T> InTransactionAsync<T>(Func<Task<T>> work, CancellationToken ct)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        var result = await work();
        try
        {
            await transaction.CommitAsync(ct);
        }
        catch (Exception ex) when (TeamDbErrors.ToAppException(ex) is { } mapped)
        {
            throw mapped;
        }
        return result;
    }
}
