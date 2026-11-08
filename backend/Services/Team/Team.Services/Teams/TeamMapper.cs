using Team.Repositories.Members;
using Team.Repositories.Teams;
using Team.Services.Academic;

namespace Team.Services.Teams;

/// <summary>Danh mục của Academic để gắn tên domain / chuyên ngành; rỗng khi Academic không trả lời (chỉ mất tên, không mất dữ liệu).</summary>
public sealed record Catalogs(IReadOnlyDictionary<int, CatalogItem> Domains, IReadOnlyDictionary<int, CatalogItem> Specializations, TeamRuleSet? Rules = null)
{
    /// <summary>Quy tắc lập nhóm đi kèm danh mục để thẻ nhóm hiện đúng sĩ số theo W05 (BE-90).</summary>
    public TeamRuleSet TeamRules => Rules ?? TeamRuleSet.Default;

    public static readonly Catalogs Empty = new(new Dictionary<int, CatalogItem>(), new Dictionary<int, CatalogItem>());

    public CatalogRef Specialization(int id) =>
        Specializations.TryGetValue(id, out var s) ? new CatalogRef(id, s.Code, s.Name) : new CatalogRef(id, null, null);
}

public static class TeamMapper
{
    public static TeamCardResponse ToCard(TeamGraph g, Catalogs catalogs)
    {
        var team = g.Team;
        var memberCount = g.Members.Count;
        var specializationCount = TeamDiversityRule.CountDiversity(g.Members);
        var rules = catalogs.TeamRules;
        return new TeamCardResponse
        {
            Id = team.Id,
            Name = team.Name,
            ProjectId = team.ProjectId,
            ProjectName = g.Project?.Name,
            IsProjectContinued = g.Project?.OriginCourseCode == "EXE101" && team.CourseCode == "EXE201",
            CourseCode = team.CourseCode,
            Status = team.Status,
            Domains = g.Domains.Select(d => new CatalogRef(d.DomainId, d.DomainCode, catalogs.Domains.GetValueOrDefault(d.DomainId)?.Name)).ToList(),
            MemberCount = memberCount,
            OpenSlots = Math.Max(0, rules.MaxSize - memberCount),
            Leader = g.Members.FirstOrDefault(m => m.Role == MemberRoles.Leader) is { } leader ? ToRef(leader, catalogs) : null,
            LecturerName = g.LecturerName,
            MentorName = g.MentorName,
            // missingSpecializations cần biết nhóm cần chuyên ngành nào (bài tuyển, BE-31) – hiện để trống.
            Requirement = new TeamRequirement(memberCount, rules.MinSize, rules.MaxSize, specializationCount, rules.MinSpecializations,
                TeamRules.IsValid(memberCount, specializationCount, rules), []),
        };
    }

    public static TeamDetailResponse ToDetail(TeamGraph g, Catalogs catalogs, TeamDetailExtras extras, string myRelation) => new(ToCard(g, catalogs))
    {
        IdeaDescription = g.Project?.Description,
        Project = g.Project is { } p ? new ProjectSummary(p.Id, p.Name, p.Description, p.CurrentVersion, p.OriginCourseCode, null) : null,
        // pendingChange (nhóm EXE201 kế thừa) do module ChangeRequests của Thành bổ sung.
        Members = g.Members.Select(m => new MemberResponse(m.StudentId, m.StudentCode, m.FullName, catalogs.Specialization(m.SpecializationId ?? 0),
            m.Role, null, null, m.JoinedAt, null)).ToList(),
        IsInherited = g.Team.IsInherited,
        IsAutoAssigned = g.Team.IsAutoAssigned,
        RecruitmentPost = extras.OpenPost is { } post
            ? new RecruitmentPostResponse(post.Id, post.Title, post.Description, extras.PostSpecializationIds, post.Status, post.UpdatedAt)
            : null,
        HasPendingChanges = extras.HasPendingChanges,
        MyRelation = myRelation,
        FinalizedAt = g.Team.FinalizedAt,
    };

    /// <summary>LEADER / MEMBER theo vai trò trong nhóm; người ngoài: INVITED, REQUESTED hoặc NONE.</summary>
    public static string Relation(TeamGraph g, Guid? viewerStudentId, TeamDetailExtras extras) =>
        g.Members.FirstOrDefault(m => m.StudentId == viewerStudentId) is { } member ? member.Role
        : extras.ViewerInvited ? "INVITED"
        : extras.ViewerRequested ? "REQUESTED"
        : "NONE";

    private static StudentRef ToRef(TeamMember m, Catalogs catalogs) =>
        new(m.StudentId, m.StudentCode, m.FullName, catalogs.Specialization(m.SpecializationId ?? 0));
}
