using BuildingBlocks.Messaging;
using Matching.Services.ReadModel;

namespace Matching.Services.Events;

/// <summary>Nhóm chưa có trong read model khi nhận sự kiện của topic khác – consumer thử lại (TeamCreated có thể tới sau).</summary>
public sealed class ReadModelNotReadyException(Guid teamId)
    : Exception($"Read model chưa có nhóm {teamId}."), IRetryableEventException;

/// <summary>
/// Dựng read model từ sự kiện (BE-35, BE-71). Mỗi hàm idempotent: nhận lại cùng sự kiện không đổi kết quả.
/// </summary>
public sealed class ReadModelProjector(IMatchingReadModel model)
{
    // ---------- exe.student ----------

    public Task HandleAsync(StudentImported e, CancellationToken ct) =>
        model.SaveStudentAsync(e.StudentId, e.SemesterId, e.CourseCode, e.FullName, e.SpecializationId, ct);

    public Task HandleAsync(StudentProfileUpdated e, CancellationToken ct)
    {
        var skills = new Dictionary<Skill, int>();
        foreach (var rating in e.Skills ?? [])
        {
            if (SkillCodes.Parse(rating.Skill) is { } skill)
            {
                skills[skill] = Math.Clamp(rating.Level, 1, 5);
            }
        }
        return model.SaveStudentProfileAsync(e.StudentId, e.FullName, e.SpecializationId, skills, e.IsSeekingTeam, ct);
    }

    public Task HandleAsync(LecturerRegistered e, CancellationToken ct) =>
        model.SaveAdvisorAsync(AdvisorKind.Lecturer, e.LecturerId, e.DomainIds, e.IsActive ?? true, ct);

    public Task HandleAsync(LecturerProfileUpdated e, CancellationToken ct) =>
        model.SaveAdvisorAsync(AdvisorKind.Lecturer, e.LecturerId, e.DomainIds, e.IsActive ?? true, ct);

    public Task HandleAsync(MentorRegistered e, CancellationToken ct) =>
        model.SaveAdvisorAsync(AdvisorKind.Mentor, e.MentorId, e.DomainIds, e.IsActive ?? true, ct);

    // ---------- exe.team ----------

    public async Task HandleAsync(TeamCreated e, CancellationToken ct)
    {
        await model.SaveTeamAsync(e.TeamId, e.SemesterId, e.CourseCode, e.Name, e.ProjectId, ct);
        // Trưởng nhóm là thành viên đầu tiên; không có MemberJoined riêng cho trưởng nhóm.
        await model.AddTeamMemberAsync(e.TeamId, e.LeaderStudentId, ct);
        await model.SetStudentTeamAsync(e.LeaderStudentId, e.TeamId, isMember: true, ct);
    }

    public async Task HandleAsync(MemberJoined e, CancellationToken ct)
    {
        await model.AddTeamMemberAsync(e.TeamId, e.StudentId, ct);
        await model.SetStudentTeamAsync(e.StudentId, e.TeamId, isMember: true, ct);
    }

    public async Task HandleAsync(MemberLeft e, CancellationToken ct)
    {
        await model.RemoveTeamMemberAsync(e.TeamId, e.StudentId, ct);
        await model.SetStudentTeamAsync(e.StudentId, e.TeamId, isMember: false, ct);
    }

    public Task HandleAsync(TeamFinalized e, CancellationToken ct) =>
        model.SetTeamStatusAsync(e.TeamId, TeamStatuses.Finalized, ct);

    public Task HandleAsync(TeamNeedsReview e, CancellationToken ct) =>
        model.SetTeamStatusAsync(e.TeamId, TeamStatuses.NeedsReview, ct);

    public async Task HandleAsync(ProjectCreated e, CancellationToken ct)
    {
        await model.SaveProjectDomainsAsync(e.ProjectId, e.DomainIds ?? [], ct);
        await model.LinkTeamToProjectAsync(e.TeamId, e.ProjectId, ct);
    }

    public Task HandleAsync(TeamLinkedToProject e, CancellationToken ct) =>
        model.LinkTeamToProjectAsync(e.TeamId, e.ProjectId, ct);

    // ---------- exe.assignment ----------

    public Task HandleAsync(TopicInterestOpened e, CancellationToken ct) =>
        model.SetInterestAsync(e.TeamId, ParseKind(e.AdvisorKind), e.AdvisorId, isOpen: true, ct);

    public Task HandleAsync(TopicInterestClosed e, CancellationToken ct) =>
        model.SetInterestAsync(e.TeamId, ParseKind(e.AdvisorKind), e.AdvisorId, isOpen: false, ct);

    public async Task HandleAsync(LecturerAssigned e, CancellationToken ct)
    {
        await model.SetTeamAdvisorAsync(e.TeamId, AdvisorKind.Lecturer, e.LecturerId, ct);
        var team = await model.GetTeamAsync(e.TeamId, ct) ?? throw new ReadModelNotReadyException(e.TeamId);
        // Giảng viên EXE101 của đề tài được ưu tiên khi gợi ý đề tài EXE201 (rule 26).
        if (team is { CourseCode: Courses.Exe101, ProjectId: { } projectId })
        {
            await model.SetProjectExe101LecturerAsync(projectId, e.LecturerId, ct);
        }
    }

    public Task HandleAsync(MentorAssigned e, CancellationToken ct) =>
        model.SetTeamAdvisorAsync(e.TeamId, AdvisorKind.Mentor, e.MentorId, ct);

    public async Task HandleAsync(MentorUnassigned e, CancellationToken ct)
    {
        var team = await model.GetTeamAsync(e.TeamId, ct) ?? throw new ReadModelNotReadyException(e.TeamId);
        if (team.MentorId == e.MentorId)
        {
            await model.SetTeamAdvisorAsync(e.TeamId, AdvisorKind.Mentor, null, ct);
        }
    }

    private static AdvisorKind ParseKind(string kind) =>
        string.Equals(kind, "MENTOR", StringComparison.OrdinalIgnoreCase) ? AdvisorKind.Mentor : AdvisorKind.Lecturer;
}
