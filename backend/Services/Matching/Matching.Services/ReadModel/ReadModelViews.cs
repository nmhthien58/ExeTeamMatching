namespace Matching.Services.ReadModel;

/// <summary>5 kỹ năng cố định, tự đánh giá 1–5 (rule 16). Giá trị trùng enum <c>Skill</c> trong matching.proto.</summary>
public enum Skill
{
    Technology = 1,
    UiUxDesign = 2,
    Management = 3,
    Marketing = 4,
    CommunicationPlanning = 5,
}

public enum AdvisorKind
{
    Lecturer = 1,
    Mentor = 2,
}

public static class Courses
{
    public const string Exe101 = "EXE101";
    public const string Exe201 = "EXE201";
}

/// <summary>Trạng thái nhóm mà Matching biết từ sự kiện (Draft / Active gộp chung là <see cref="Open"/>).</summary>
public static class TeamStatuses
{
    public const string Open = "OPEN";
    public const string Finalized = "FINALIZED";
    public const string NeedsReview = "NEEDS_REVIEW";
}

public static class SkillCodes
{
    private static readonly Dictionary<string, Skill> ByCode = new(StringComparer.OrdinalIgnoreCase)
    {
        ["TECHNOLOGY"] = Skill.Technology,
        ["UI_UX_DESIGN"] = Skill.UiUxDesign,
        ["MANAGEMENT"] = Skill.Management,
        ["MARKETING"] = Skill.Marketing,
        ["COMMUNICATION_PLANNING"] = Skill.CommunicationPlanning,
    };

    /// <summary>Mã kỹ năng trong payload sự kiện (<c>UI_UX_DESIGN</c>…); mã lạ trả null.</summary>
    public static Skill? Parse(string code) => ByCode.TryGetValue(code, out var skill) ? skill : null;

    /// <summary>Tên hiển thị trong lý do gợi ý.</summary>
    public static string DisplayName(Skill skill) => skill switch
    {
        Skill.Technology => "Công nghệ",
        Skill.UiUxDesign => "Thiết kế UI/UX",
        Skill.Management => "Quản lý",
        Skill.Marketing => "Marketing",
        Skill.CommunicationPlanning => "Truyền thông – kế hoạch",
        _ => skill.ToString(),
    };
}

/// <summary>Sinh viên trong read model. <see cref="TeamId"/> null = chưa có nhóm.</summary>
public sealed record StudentView(
    Guid Id,
    Guid SemesterId,
    string CourseCode,
    string? FullName,
    int SpecializationId,
    IReadOnlyDictionary<Skill, int> Skills,
    bool IsSeekingTeam,
    Guid? TeamId)
{
    public int SkillLevel(Skill skill) => Skills.TryGetValue(skill, out var level) ? level : 0;
}

/// <summary>Nhóm trong read model; <see cref="ProjectExe101LecturerId"/> là giảng viên EXE101 của cùng đề tài (rule 26).</summary>
public sealed record TeamView(
    Guid Id,
    Guid SemesterId,
    string CourseCode,
    string? Name,
    string Status,
    Guid? ProjectId,
    IReadOnlyList<int> DomainIds,
    IReadOnlySet<Guid> MemberIds,
    Guid? LecturerId,
    Guid? MentorId,
    IReadOnlySet<Guid> InterestedLecturerIds,
    IReadOnlySet<Guid> InterestedMentorIds,
    Guid? ProjectExe101LecturerId)
{
    public Guid? AdvisorId(AdvisorKind kind) => kind == AdvisorKind.Mentor ? MentorId : LecturerId;

    public IReadOnlySet<Guid> InterestedAdvisorIds(AdvisorKind kind) =>
        kind == AdvisorKind.Mentor ? InterestedMentorIds : InterestedLecturerIds;
}

/// <summary>Giảng viên hoặc mentor: domain chuyên môn dùng cho gợi ý đề tài (rule 19).</summary>
public sealed record AdvisorView(AdvisorKind Kind, Guid Id, IReadOnlyList<int> DomainIds, bool IsActive);
