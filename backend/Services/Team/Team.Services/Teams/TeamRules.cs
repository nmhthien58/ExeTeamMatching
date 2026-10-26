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

/// <summary>Quy tắc sĩ số và trạng thái nhóm (rule 1, 2, 15).</summary>
public static class TeamRules
{
    // shortcut: hằng số theo rule 1, 2; đọc từ cấu hình quy tắc của Academic khi có BE-23 (W05).
    public const int MinSize = 4;
    public const int MaxSize = 6;
    public const int MinSpecializations = 2;

    public static bool IsValid(int memberCount, int specializationCount) =>
        memberCount is >= MinSize and <= MaxSize && specializationCount >= MinSpecializations;

    /// <summary>Việc nên làm tiếp trên trang chủ sinh viên (S01).</summary>
    public static string NextAction(NextActionInput x)
    {
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
            if (x.MemberCount < MinSize)
            {
                return "INVITE_MEMBERS";
            }
            if (x.SpecializationCount < MinSpecializations)
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
    bool HasLecturer, bool HasLecturerPreferences, bool HasUnsubmittedChanges, bool HasPendingChangeRequest);
