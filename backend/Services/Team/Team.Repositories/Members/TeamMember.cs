// Sinh từ deploy/db/03_team_db.sql (BE-24). Sửa DB script trước, rồi sửa file này cho khớp.
namespace Team.Repositories.Members;

/// <summary>Bảng <c>team.team_members</c>. Trigger chặn sửa team_id, semester_id, student_id.</summary>
public class TeamMember
{
    public Guid Id { get; set; }
    public Guid TeamId { get; set; }
    public Guid SemesterId { get; set; }
    public Guid StudentId { get; set; }
    public string StudentCode { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public int SpecializationId { get; set; }
    public string Role { get; set; } = "MEMBER";
    public string JoinSource { get; set; } = string.Empty;
    public DateTimeOffset JoinedAt { get; set; }
    public uint Version { get; set; }
}
