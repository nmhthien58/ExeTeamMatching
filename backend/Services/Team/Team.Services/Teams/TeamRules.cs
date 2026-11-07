namespace Team.Services.Teams;

public static class TeamStatuses
{
    public const string Draft = "DRAFT";
    public const string Active = "ACTIVE";
    public const string NeedsReview = "NEEDS_REVIEW";
    public const string Finalized = "FINALIZED";
    public const string Disbanded = "DISBANDED";

    public static readonly IReadOnlyList<string> All = [Draft, Active, NeedsReview, Finalized, Disbanded];
}

public static class MemberRoles
{
    public const string Leader = "LEADER";
    public const string Member = "MEMBER";
}

public static class Phases
{
    public const string TeamFormation = "TEAM_FORMATION";
    public const string SemesterRunning = "SEMESTER_RUNNING";
}

/// <summary>
/// Quy tắc lập nhóm Bộ môn cấu hình ở W05 (BE-90, BE-89), đọc qua <see cref="Academic.IAcademicClient.GetTeamRulesAsync"/>.
/// <see cref="MaxTeamRatio"/>: số nhóm tối đa của môn trong kỳ = floor(số sinh viên của môn × hệ số).
/// </summary>
public sealed record TeamRuleSet(int MinSize, int MaxSize, int MinSpecializations, decimal MaxTeamRatio)
{
    public static readonly TeamRuleSet Default = new(TeamRules.MinSize, TeamRules.MaxSize, TeamRules.MinSpecializations, TeamRules.DefaultMaxTeamRatio);

    /// <summary>
    /// Số nhóm tối đa của môn, ít nhất 1 để lớp rất nhỏ vẫn lập được nhóm; null khi chưa biết sĩ số môn (Academic không trả sinh viên)
    /// thì không giới hạn.
    /// </summary>
    public int? MaxTeams(int studentCount) => studentCount > 0 ? Math.Max(1, (int)Math.Floor(studentCount * MaxTeamRatio)) : null;
}

/// <summary>Quy tắc sĩ số và trạng thái nhóm (rule 1, 2, 15). Hằng số là mặc định khi Academic chưa trả cấu hình.</summary>
public static class TeamRules
{
    public const int MinSize = 4;
    public const int MaxSize = 6;
    public const int MinSpecializations = 2;
    public const decimal DefaultMaxTeamRatio = 0.2m;

    /// <summary>Giới hạn trên của sĩ số tối đa ở W05 (<c>RuleSettingsInputValidator</c>).</summary>
    public const int MaxConfigurableSize = 50;

    // shortcut: rule 8 "72 giờ (cấu hình được)" – đọc từ cấu hình quy tắc khi có BE-23.
    public static readonly TimeSpan RequestLifetime = TimeSpan.FromHours(72);

    /// <summary>
    /// Rule 15: Nháp (dưới 4 người) ↔ Hoạt động khi số thành viên đổi. Cần xem lại, Đã chốt, Đã giải tán do chốt nhóm / Admin quyết định, giữ nguyên.
    /// </summary>
    public static string StatusAfterMemberChange(string current, int memberCount, TeamRuleSet? rules = null) => current switch
    {
        TeamStatuses.Draft or TeamStatuses.Active => memberCount >= (rules ?? TeamRuleSet.Default).MinSize ? TeamStatuses.Active : TeamStatuses.Draft,
        _ => current,
    };

    public static bool IsValid(int memberCount, int specializationCount, TeamRuleSet? rules = null)
    {
        var r = rules ?? TeamRuleSet.Default;
        return memberCount >= r.MinSize && memberCount <= r.MaxSize && specializationCount >= r.MinSpecializations;
    }

    /// <summary>Việc nên làm tiếp trên trang chủ sinh viên (S01).</summary>
    public static string NextAction(NextActionInput x)
    {
        var rules = x.Rules ?? TeamRuleSet.Default;
        if (!x.HasTeam)
        {
            return x.Phase == Phases.TeamFormation ? "CREATE_OR_JOIN" : "NONE";
        }
        if (x.HasPendingChangeRequest)
        {
            return "WAIT_REVIEW";
        }
        if (x.IsLeader && x.HasUnsubmittedChanges)
        {
            return "SUBMIT_CHANGE_REQUEST";
        }
        if (x.Phase == Phases.TeamFormation)
        {
            if (x.MemberCount < rules.MinSize)
            {
                return "INVITE_MEMBERS";
            }
            if (x.SpecializationCount < rules.MinSpecializations)
            {
                return "ADD_SPECIALIZATION";
            }
            if (x.IsLeader && !x.HasLecturer && !x.HasLecturerPreferences)
            {
                return "CHOOSE_LECTURER";
            }
            return "NONE";
        }
        return x.Status == TeamStatuses.Finalized ? "OPEN_WORKSPACE" : "NONE";
    }
}

public sealed record NextActionInput(
    bool HasTeam, bool IsLeader, string Phase, string? Status, int MemberCount, int SpecializationCount,
    bool HasLecturer, bool HasLecturerPreferences, bool HasUnsubmittedChanges, bool HasPendingChangeRequest, TeamRuleSet? Rules = null);
