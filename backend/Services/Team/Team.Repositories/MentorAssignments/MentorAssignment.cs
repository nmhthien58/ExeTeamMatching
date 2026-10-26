// Sinh từ deploy/db/03_team_db.sql (BE-24). Sửa DB script trước, rồi sửa file này cho khớp.
namespace Team.Repositories.MentorAssignments;

/// <summary>Bảng <c>team.mentor_assignments</c> – mentor của nhóm (rule 20).</summary>
public class MentorAssignment
{
    public Guid Id { get; set; }
    public Guid TeamId { get; set; }
    public Guid MentorId { get; set; }
    public string MentorName { get; set; } = string.Empty;
    public string Status { get; set; } = "ASSIGNED";
    public string Reason { get; set; } = string.Empty;
    public string Source { get; set; } = "AUTO";
    public Guid? MatchingRunId { get; set; }
    public Guid? AssignedByAccountId { get; set; }
    public DateTimeOffset AssignedAt { get; set; }
    public DateTimeOffset? EndedAt { get; set; }
    public string? EndReason { get; set; }
    public string? Note { get; set; }
    public uint Version { get; set; }
}
