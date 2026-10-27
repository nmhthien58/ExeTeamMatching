using System.Security.Claims;
using BuildingBlocks.Auth;
using BuildingBlocks.Errors;
using Team.Repositories.LecturerAssignments;
using Team.Repositories.MentorAssignments;
using Team.Repositories.Teams;
using Team.Services.Teams;

namespace Team.Services.AdvisorAssignments;

public sealed class AdvisorAssignmentService(
    ILecturerAssignmentRepository lecturerRepo,
    IMentorAssignmentRepository mentorRepo,
    ITeamRepository teams,
    TeamReader reader) : IAdvisorAssignmentService
{
    public async Task<LecturerAssignmentResponse?> GetTeamLecturerAsync(Guid teamId, ClaimsPrincipal user, CancellationToken ct)
    {
        var graph = await teams.GetGraphAsync(teamId, ct) ?? throw TeamErrors.TeamNotFound(teamId);
        var assignment = await lecturerRepo.GetActiveByTeamIdAsync(teamId, ct);

        EnsureCanReadTeamAdvisors(graph, assignment?.LecturerId, null, user);

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

        EnsureCanReadTeamAdvisors(graph, null, assignment?.MentorId, user);

        if (assignment is null) return null;

        return new MentorAssignmentResponse(
            assignment.Id,
            assignment.TeamId,
            assignment.MentorId,
            assignment.MentorName,
            MentorOrganization: null,
            IsInternal: true,
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
            throw new ForbiddenException("lecturer-only", "Chỉ giảng viên mới có quyền xem danh sách nhóm hướng dẫn.");
        }

        var instructingAssignments = await lecturerRepo.ListActiveByLecturerIdAsync(lecturerId.Value, ct);
        var mentoringAssignments = await mentorRepo.ListActiveByMentorIdAsync(lecturerId.Value, ct);

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

        return new LecturerTeamsResponse(null, instructingList, mentoringList);
    }

    public async Task<IReadOnlyList<AdvisedTeamResponse>> GetMentorTeamsMeAsync(ClaimsPrincipal user, CancellationToken ct)
    {
        var mentorId = user.GetMentorId() ?? user.GetLecturerId();
        if (mentorId is null)
        {
            throw new ForbiddenException("mentor-only", "Chỉ mentor mới có quyền xem danh sách nhóm mentor.");
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

    private static void EnsureCanReadTeamAdvisors(
        TeamGraph graph,
        Guid? lecturerId,
        Guid? mentorId,
        ClaimsPrincipal user)
    {
        if (user.IsInRole(Roles.Admin)) return;

        var studentId = user.GetStudentId();
        if (studentId is not null && graph.Members.Any(m => m.StudentId == studentId.Value))
        {
            return;
        }

        var currentLecturerId = user.GetLecturerId();
        if (currentLecturerId is not null && lecturerId is not null && currentLecturerId.Value == lecturerId.Value)
        {
            return;
        }

        var currentMentorId = user.GetMentorId();
        if (currentMentorId is not null && mentorId is not null && currentMentorId.Value == mentorId.Value)
        {
            return;
        }

        throw new ForbiddenException("advisor-access-denied", "Không có quyền xem thông tin cố vấn của nhóm này.");
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
