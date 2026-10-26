// Sinh từ deploy/db/03_team_db.sql (BE-24). Sửa DB script trước, rồi sửa file này cho khớp.
namespace Team.Repositories.AdvisorMatching;

/// <summary>Bảng <c>team.advisor_matching_runs</c> – mỗi lần ghép cố vấn (rule 9, 20).</summary>
public class AdvisorMatchingRun
{
    public Guid Id { get; set; }
    public string AdvisorKind { get; set; } = string.Empty;
    public Guid SemesterId { get; set; }
    public string CourseCode { get; set; } = string.Empty;
    public string Trigger { get; set; } = string.Empty;
    public string Status { get; set; } = "PREVIEW";
    public Guid? RequestedByAccountId { get; set; }

    /// <summary>JSON (cột jsonb): danh sách {teamId, advisorId, reason, preferenceRank}.</summary>
    public string Plan { get; set; } = "[]";

    public int MatchedCount { get; set; }
    public int UnmatchedCount { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? AppliedAt { get; set; }
    public uint Version { get; set; }
}
