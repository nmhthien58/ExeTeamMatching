// Sinh từ deploy/db/03_team_db.sql (BE-24). Sửa DB script trước, rồi sửa file này cho khớp.
namespace Team.Repositories.BaselineImport;

/// <summary>Bảng <c>team.team_baseline_members</c> – mốc EXE101 của nhóm EXE201 kế thừa.</summary>
public class TeamBaselineMember
{
    public Guid TeamId { get; set; }
    public Guid StudentId { get; set; }
    public string StudentCode { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public int SpecializationId { get; set; }
    public string Role { get; set; } = string.Empty;
    public DateTimeOffset ImportedAt { get; set; }
}
