using System.Security.Claims;
using System.Text.Json;
using BuildingBlocks.Auth;
using BuildingBlocks.Errors;
using BuildingBlocks.Outbox;
using Team.Repositories.AuditLog;
using Team.Repositories.LecturerPreferences;
using Team.Repositories.Members;
using Team.Repositories.Teams;
using Team.Services.Academic;
using Team.Services.Teams;

namespace Team.Services.LecturerPreferences;

public sealed class LecturerPreferencesService(
    ILecturerPreferenceRepository preferences,
    ITeamRepository teams,
    IAcademicClient academic,
    RoundGuard rounds,
    TeamReader reader,
    AuditLogWriter audit,
    TimeProvider clock) : ILecturerPreferencesService
{
    private const int MaxPreferences = 3;

    public async Task<LecturerPreferenceListResponse> GetPreferencesAsync(Guid teamId, ClaimsPrincipal user, CancellationToken ct)
    {
        var graph = await teams.GetGraphAsync(teamId, ct) ?? throw TeamErrors.TeamNotFound(teamId);
        EnsureCanRead(graph, user);

        var hasAssigned = await preferences.HasAssignedInstructorAsync(teamId, ct);
        var round = await academic.GetOpenRoundAsync(graph.Team.CourseCode, RoundTypes.TeamFormation, ct);
        var roundOpen = round is not null && round.SemesterId == graph.Team.SemesterId;
        var locked = hasAssigned || !roundOpen;
        var lockAt = round?.DeadlineAt;

        var existingPrefs = await preferences.ListByTeamIdAsync(teamId, ct);
        var openInterests = await preferences.ListOpenLecturerInterestsByTeamIdAsync(teamId, ct);
        var interestedMap = openInterests.ToDictionary(i => i.AdvisorId, i => i.AdvisorName);

        var prev = await preferences.GetPreviousInstructorAsync(teamId, ct);
        var catalogs = await reader.LoadCatalogsAsync(ct);

        var prefItems = new List<LecturerPreferenceItem>();
        foreach (var p in existingPrefs.OrderBy(p => p.Rank))
        {
            var isInterested = interestedMap.ContainsKey(p.LecturerId);
            var (fullName, domains) = await ResolveLecturerInfoAsync(p.LecturerId, p.LecturerName, catalogs, ct);
            prefItems.Add(new LecturerPreferenceItem(p.LecturerId, fullName, domains, p.Rank, isInterested));
        }

        var interestedLecturers = new List<LecturerRef>();
        foreach (var interest in openInterests)
        {
            var (fullName, domains) = await ResolveLecturerInfoAsync(interest.AdvisorId, interest.AdvisorName, catalogs, ct);
            interestedLecturers.Add(new LecturerRef(interest.AdvisorId, fullName, domains));
        }

        LecturerRef? previousInstructor = null;
        if (prev is not null)
        {
            var (fullName, domains) = await ResolveLecturerInfoAsync(prev.LecturerId, prev.LecturerName, catalogs, ct);
            previousInstructor = new LecturerRef(prev.LecturerId, fullName, domains);
        }

        return new LecturerPreferenceListResponse(
            MaxPreferences,
            locked,
            lockAt,
            prefItems,
            interestedLecturers,
            previousInstructor);
    }

    public async Task<LecturerPreferenceListResponse> UpdatePreferencesAsync(
        Guid teamId,
        UpdateLecturerPreferencesInput input,
        ClaimsPrincipal user,
        CancellationToken ct)
    {
        var graph = await teams.GetGraphAsync(teamId, ct) ?? throw TeamErrors.TeamNotFound(teamId);

        var studentId = user.GetStudentId() ?? throw TeamErrors.StudentsOnly();
        var leader = graph.Members.FirstOrDefault(m => m.StudentId == studentId && m.Role == MemberRoles.Leader);
        if (leader is null)
        {
            throw TeamErrors.NotLeader();
        }

        if (await preferences.HasAssignedInstructorAsync(teamId, ct))
        {
            throw new ConflictException("preferences-locked", "Nguyện vọng đã bị khóa", "Nhóm đã được ghép giảng viên.");
        }

        await rounds.RequireTeamFormationAsync(graph.Team.CourseCode, graph.Team.SemesterId, ct);

        if (input.LecturerIds is null)
        {
            throw TeamErrors.Invalid("lecturerIds", "Danh sách nguyện vọng không được null.");
        }

        if (input.LecturerIds.Count > MaxPreferences)
        {
            throw TeamErrors.Invalid("lecturerIds", $"Tối đa {MaxPreferences} nguyện vọng giảng viên.");
        }

        if (input.LecturerIds.Distinct().Count() != input.LecturerIds.Count)
        {
            throw TeamErrors.Invalid("lecturerIds", "Danh sách nguyện vọng không được trùng giảng viên.");
        }

        var currentPrefs = await preferences.ListByTeamIdAsync(teamId, ct);
        var currentLecturerIds = currentPrefs.Select(p => p.LecturerId).ToHashSet();
        var addedLecturerIds = input.LecturerIds.Where(id => !currentLecturerIds.Contains(id)).ToList();

        var openInterests = await preferences.ListOpenLecturerInterestsByTeamIdAsync(teamId, ct);
        var interestNames = openInterests.ToDictionary(i => i.AdvisorId, i => i.AdvisorName);
        var currentNames = currentPrefs.ToDictionary(p => p.LecturerId, p => p.LecturerName);

        var now = clock.GetUtcNow();
        var newPrefs = new List<LecturerPreference>();
        for (var i = 0; i < input.LecturerIds.Count; i++)
        {
            var lecturerId = input.LecturerIds[i];
            var lecturerName = await ResolveLecturerNameAsync(lecturerId, interestNames, currentNames, ct);
            newPrefs.Add(new LecturerPreference
            {
                TeamId = teamId,
                Rank = (short)(i + 1),
                LecturerId = lecturerId,
                LecturerName = lecturerName,
                UpdatedByStudentId = studentId,
                UpdatedAt = now
            });
        }

        var outbox = new OutboxMessage
        {
            Id = Guid.NewGuid(),
            Topic = AssignmentEventTypes.Topic,
            EventType = AssignmentEventTypes.LecturerPreferencesUpdated,
            AggregateType = "Team",
            AggregateId = teamId,
            Payload = JsonSerializer.Serialize(new LecturerPreferencesUpdated(
                teamId,
                graph.Team.Name,
                input.LecturerIds,
                addedLecturerIds)),
            OccurredAt = now
        };

        audit.Add(
            teamId,
            "Team",
            teamId,
            "LECTURER_PREFERENCES_UPDATED",
            user.ToAuditActor(),
            oldValues: new { LecturerIds = currentPrefs.OrderBy(p => p.Rank).Select(p => p.LecturerId).ToList() },
            newValues: new { LecturerIds = input.LecturerIds });

        await preferences.ReplacePreferencesAsync(teamId, newPrefs, [outbox], ct);

        return await GetPreferencesAsync(teamId, user, ct);
    }

    public async Task<IReadOnlyList<LecturerPreferenceMeItem>> GetPreferencesMeAsync(
        string? courseCode,
        ClaimsPrincipal user,
        CancellationToken ct)
    {
        var lecturerId = user.GetLecturerId();
        if (lecturerId is null)
        {
            throw new ForbiddenException(title: "Chỉ giảng viên được truy cập.");
        }

        var prefs = await preferences.ListByLecturerIdAsync(lecturerId.Value, ct);
        var catalogs = await reader.LoadCatalogsAsync(ct);
        var items = new List<LecturerPreferenceMeItem>();

        foreach (var pref in prefs)
        {
            var graph = await teams.GetGraphAsync(pref.TeamId, ct);
            if (graph is null) continue;

            if (!string.IsNullOrWhiteSpace(courseCode) &&
                !string.Equals(graph.Team.CourseCode, courseCode.Trim(), StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var isInterested = await preferences.IsLecturerInterestedAsync(pref.TeamId, lecturerId.Value, ct);
            var card = TeamMapper.ToCard(graph, catalogs);
            items.Add(new LecturerPreferenceMeItem(card, pref.Rank, isInterested));
        }

        return items.OrderBy(i => i.Rank).ThenBy(i => i.Team.Name).ToList();
    }

    private static void EnsureCanRead(TeamGraph graph, ClaimsPrincipal user)
    {
        if (user.IsInRole(Roles.Admin) || user.IsInRole(Roles.Lecturer) || user.IsInRole(Roles.Mentor))
        {
            return;
        }

        var studentId = user.GetStudentId();
        if (studentId is not null && graph.Members.Any(m => m.StudentId == studentId.Value))
        {
            return;
        }

        throw TeamErrors.NotMember();
    }

    private async Task<string> ResolveLecturerNameAsync(
        Guid lecturerId,
        IReadOnlyDictionary<Guid, string> interestNames,
        IReadOnlyDictionary<Guid, string> currentNames,
        CancellationToken ct)
    {
        if (interestNames.TryGetValue(lecturerId, out var nameFromInterest) && !string.IsNullOrWhiteSpace(nameFromInterest))
        {
            return nameFromInterest;
        }

        if (currentNames.TryGetValue(lecturerId, out var nameFromCurrent) && !string.IsNullOrWhiteSpace(nameFromCurrent))
        {
            return nameFromCurrent;
        }

        var academicLecturer = await academic.GetLecturerAsync(lecturerId, ct);
        if (academicLecturer is not null && !string.IsNullOrWhiteSpace(academicLecturer.FullName))
        {
            return academicLecturer.FullName;
        }

        return "Giảng viên";
    }

    private async Task<(string FullName, IReadOnlyList<CatalogRef> Domains)> ResolveLecturerInfoAsync(
        Guid lecturerId,
        string fallbackName,
        Catalogs catalogs,
        CancellationToken ct)
    {
        var academicLecturer = await academic.GetLecturerAsync(lecturerId, ct);
        if (academicLecturer is not null)
        {
            var domains = academicLecturer.Domains
                .Select(d => new CatalogRef(d.Id, d.Code, catalogs.Domains.GetValueOrDefault(d.Id)?.Name ?? d.Name))
                .ToList();
            return (academicLecturer.FullName, domains);
        }

        return (string.IsNullOrWhiteSpace(fallbackName) ? "Giảng viên" : fallbackName, []);
    }
}
