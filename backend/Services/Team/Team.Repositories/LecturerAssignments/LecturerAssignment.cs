// Sinh từ deploy/db/03_team_db.sql (BE-24). Sửa DB script trước, rồi sửa file này cho khớp.
namespace Team.Repositories.LecturerAssignments;

/// <summary>Bảng <c>team.lecturer_assignments</c> – giảng viên hướng dẫn của nhóm (rule 9).</summary>
public class LecturerAssignment
{
    public Guid Id { get; set; }
    public Guid TeamId { get; set; }
    public Guid LecturerId { get; set; }
    public string LecturerName { get; set; } = string.Empty;
    public string Status { get; set; } = "ASSIGNED";
    public string Reason { get; set; } = string.Empty;
    public short? PreferenceRank { get; set; }
    public string Source { get; set; } = "AUTO";
    public Guid? MatchingRunId { get; set; }
    public Guid? AssignedByAccountId { get; set; }
    public DateTimeOffset AssignedAt { get; set; }
    public DateTimeOffset? RevokedAt { get; set; }
    public string? Note { get; set; }
    public uint Version { get; set; }
}
