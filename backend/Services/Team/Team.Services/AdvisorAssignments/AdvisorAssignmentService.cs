using System.Security.Claims;
using BuildingBlocks.Auth;
using BuildingBlocks.Errors;
using Team.Repositories.AuditLog;
using Team.Repositories.LecturerAssignments;
using Team.Repositories.MentorAssignments;
using Team.Repositories.Teams;
using Team.Services.AdvisorMatching;
using Team.Services.Teams;

namespace Team.Services.AdvisorAssignments;

public sealed class AdvisorAssignmentService(
    ILecturerAssignmentRepository lecturerRepo,
    IMentorAssignmentRepository mentorRepo,
    ITeamRepository teams,
    TeamReader reader,
    MentorAssignmentWriter mentorWriter) : IAdvisorAssignmentService
{
    public async Task<LecturerAssignmentResponse?> GetTeamLecturerAsync(Guid teamId, ClaimsPrincipal user, CancellationToken ct)
    {
        var graph = await teams.GetGraphAsync(teamId, ct) ?? throw TeamErrors.TeamNotFound(teamId);
        var assignment = await lecturerRepo.GetActiveByTeamIdAsync(teamId, ct);

        await EnsureCanReadTeamAdvisorsAsync(graph, assignment?.LecturerId, null, user, ct);

        if (assignment is null) return null;

        return new LecturerAssignmentResponse(
            assignment.Id,
            assignment.TeamId,
            assignment.LecturerId,
            assignment.LecturerName,
            assignment.Status,
            assignment.Reason,
            assignment.PreferenceRank,
            assignment.Source,
            assignment.AssignedAt,
            assignment.Note);
    }

    public async Task<MentorAssignmentResponse?> GetTeamMentorAsync(Guid teamId, ClaimsPrincipal user, CancellationToken ct)
    {
        var graph = await teams.GetGraphAsync(teamId, ct) ?? throw TeamErrors.TeamNotFound(teamId);
        var assignment = await mentorRepo.GetActiveByTeamIdAsync(teamId, ct);

        await EnsureCanReadTeamAdvisorsAsync(graph, null, assignment?.MentorId, user, ct);

        if (assignment is null) return null;

        // mentorOrganization và isInternal sẽ lấy từ GET /internal/mentors/{id} khi Academic hoàn thiện.
        // Tạm thời để mentorOrganization = null và isInternal = false (không mặc định true để tránh hiển thị sai).
        return new MentorAssignmentResponse(
            assignment.Id,
            assignment.TeamId,
            assignment.MentorId,
            assignment.MentorName,
            MentorOrganization: null,
            IsInternal: false,
            assignment.Status,
            assignment.Reason,
            assignment.Source,
            assignment.AssignedAt,
            assignment.EndedAt);
    }

    public async Task<LecturerTeamsResponse> GetLecturerTeamsMeAsync(
        string? courseCode,
        string? classCode,
        string? search,
        ClaimsPrincipal user,
        CancellationToken ct)
    {
        var lecturerId = user.GetLecturerId();
        if (lecturerId is null)
        {
            throw new ForbiddenException(detail: "Chỉ giảng viên mới có quyền xem danh sách nhóm hướng dẫn.");
        }

        // Tạm thời bỏ qua classCode trước khi có dữ liệu từ Academic Service (PR #51).
        // Khi Academic có sẵn classCode, gọi /internal/students/lookup một lần cho danh sách thành viên các nhóm
        // của giảng viên, rồi chỉ giữ nhóm có ít nhất một thành viên thuộc lớp đó.
        _ = classCode;

        var instructingAssignments = await lecturerRepo.ListActiveByLecturerIdAsync(lecturerId.Value, ct);

        // Giảng viên kiêm mentor mới có claim mentor_id. Nếu không có, danh sách mentoring trả rỗng.
        var mentorId = user.GetMentorId();
        var mentoringAssignments = mentorId is not null
            ? await mentorRepo.ListActiveByMentorIdAsync(mentorId.Value, ct)
            : (IReadOnlyList<MentorAssignment>) [];

        var allTeamIds = instructingAssignments.Select(a => a.TeamId)
            .Concat(mentoringAssignments.Select(a => a.TeamId))
            .Distinct()
            .ToList();

        if (allTeamIds.Count == 0)
        {
            return new LecturerTeamsResponse(null, [], []);
        }

        var graphs = await teams.ListGraphsAsync(allTeamIds, ct);
        var catalogs = await reader.LoadCatalogsAsync(ct);
        var graphsById = graphs.ToDictionary(g => g.Team.Id);

        var instructingList = new List<AdvisedTeamResponse>();
        foreach (var a in instructingAssignments)
        {
            if (!graphsById.TryGetValue(a.TeamId, out var g)) continue;
            if (!MatchesFilters(g, courseCode, search)) continue;

            instructingList.Add(new AdvisedTeamResponse(
                TeamMapper.ToCard(g, catalogs),
                a.Reason,
                a.PreferenceRank));
        }

        var mentoringList = new List<AdvisedTeamResponse>();
        foreach (var a in mentoringAssignments)
        {
            if (!graphsById.TryGetValue(a.TeamId, out var g)) continue;
            if (!MatchesFilters(g, courseCode, search)) continue;

            mentoringList.Add(new AdvisedTeamResponse(
                TeamMapper.ToCard(g, catalogs),
                a.Reason,
                null));
        }

        // maxTeams tạm để null. Khi API lecturer phù hợp có sẵn từ Academic, lấy sức chứa theo courseCode.
        return new LecturerTeamsResponse(null, instructingList, mentoringList);
    }

    public async Task<IReadOnlyList<AdvisedTeamResponse>> GetMentorTeamsMeAsync(ClaimsPrincipal user, CancellationToken ct)
    {
        var mentorId = user.GetMentorId();
        if (mentorId is null)
        {
            // Nếu không có claim mentor_id, danh sách mentoring trả rỗng.
            return [];
        }

        var mentoringAssignments = await mentorRepo.ListActiveByMentorIdAsync(mentorId.Value, ct);
        if (mentoringAssignments.Count == 0)
        {
            return [];
        }

        var teamIds = mentoringAssignments.Select(a => a.TeamId).Distinct().ToList();
        var graphs = await teams.ListGraphsAsync(teamIds, ct);
        var catalogs = await reader.LoadCatalogsAsync(ct);
        var graphsById = graphs.ToDictionary(g => g.Team.Id);

        var list = new List<AdvisedTeamResponse>();
        foreach (var a in mentoringAssignments)
        {
            if (!graphsById.TryGetValue(a.TeamId, out var g)) continue;

            list.Add(new AdvisedTeamResponse(
                TeamMapper.ToCard(g, catalogs),
                a.Reason,
                null));
        }

        return list;
    }

    /// <summary>
    /// Đếm số nhóm đang mentor cho danh sách mentor (BE-74).
    /// Mentor không có nhóm trả 0. Tránh N+1 từ Academic W27.
    /// </summary>
    public async Task<IReadOnlyList<MentorTeamCountResponse>> GetMentorTeamCountsAsync(
        IReadOnlyList<Guid> mentorIds,
        Guid? semesterId,
        CancellationToken ct)
    {
        if (mentorIds.Count == 0)
        {
            return [];
        }

        var counts = await mentorRepo.CountActiveTeamsByMentorIdsAsync(mentorIds, semesterId, ct);
        return mentorIds.Distinct().Select(id => new MentorTeamCountResponse(
            MentorId: id,
            CurrentTeamCount: counts.GetValueOrDefault(id, 0)
        )).ToList();
    }

    /// <summary>Lý do kết thúc dòng gán khi Bộ môn khóa mentor (end_reason, payload <c>MentorUnassigned.reason</c>).</summary>
    public const string MentorDeactivatedReason = "MENTOR_DEACTIVATED";

    /// <summary>
    /// Gỡ mentor khi Bộ môn khóa (BE-74, rule 20).
    /// Consume sự kiện MentorRegistered từ exe.student; khi isActive=false thì kết thúc mọi dòng gán đang hiệu lực của mentor
    /// qua <see cref="MentorAssignmentWriter.End"/>: ghi nhật ký <c>MENTOR_UNASSIGNED</c> (actor hệ thống) và phát
    /// <c>MentorUnassigned</c> qua Outbox trong CÙNG một SaveChanges (một transaction).
    /// Idempotent: nhận lại hoặc mentor đã được gỡ thì không còn dòng ASSIGNED nên không ghi thêm. isActive=true không gỡ.
    /// </summary>
    public async Task<int> HandleMentorRegisteredAsync(MentorRegisteredEvent @event, CancellationToken ct)
    {
        if (@event.IsActive)
        {
            return 0;
        }

        var activeAssignments = await mentorRepo.ListActiveByMentorIdTrackedAsync(@event.MentorId, ct);
        if (activeAssignments.Count == 0)
        {
            return 0;
        }

        foreach (var assignment in activeAssignments)
        {
            mentorWriter.End(assignment, MentorDeactivatedReason, unassigned: true, AuditActor.System);
        }

        // Cùng TeamDbContext (scoped) với repository của writer và AuditLogWriter: một lần lưu gồm dòng gán, nhật ký và Outbox.
        await mentorRepo.SaveChangesAsync(ct);
        return activeAssignments.Count;
    }

    private async Task EnsureCanReadTeamAdvisorsAsync(
        TeamGraph graph,
        Guid? knownLecturerId,
        Guid? knownMentorId,
        ClaimsPrincipal user,
        CancellationToken ct)
    {
        if (user.IsInRole(Roles.Admin)) return;

        var studentId = user.GetStudentId();
        if (studentId is not null && graph.Members.Any(m => m.StudentId == studentId.Value))
        {
            return;
        }

        var currentLecturerId = user.GetLecturerId();
        if (currentLecturerId is not null)
        {
            var assignedLecturerId = knownLecturerId;
            if (assignedLecturerId is null)
            {
                var lecturerAssignment = await lecturerRepo.GetActiveByTeamIdAsync(graph.Team.Id, ct);
                assignedLecturerId = lecturerAssignment?.LecturerId;
            }

            if (assignedLecturerId is not null && assignedLecturerId.Value == currentLecturerId.Value)
            {
                return;
            }
        }

        var currentMentorId = user.GetMentorId();
        if (currentMentorId is not null)
        {
            var assignedMentorId = knownMentorId;
            if (assignedMentorId is null)
            {
                var mentorAssignment = await mentorRepo.GetActiveByTeamIdAsync(graph.Team.Id, ct);
                assignedMentorId = mentorAssignment?.MentorId;
            }

            if (assignedMentorId is not null && assignedMentorId.Value == currentMentorId.Value)
            {
                return;
            }
        }

        throw new ForbiddenException(detail: "Không có quyền xem thông tin cố vấn của nhóm này.");
    }

    private static bool MatchesFilters(TeamGraph g, string? courseCode, string? search)
    {
        if (!string.IsNullOrWhiteSpace(courseCode) && !g.Team.CourseCode.Equals(courseCode, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.Trim();
            var matchesName = g.Team.Name.Contains(s, StringComparison.OrdinalIgnoreCase);
            var matchesProject = g.Project?.Name.Contains(s, StringComparison.OrdinalIgnoreCase) ?? false;
            if (!matchesName && !matchesProject)
            {
                return false;
            }
        }

        return true;
    }
}
