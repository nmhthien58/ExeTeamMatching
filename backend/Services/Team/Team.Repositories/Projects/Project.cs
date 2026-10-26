// Sinh từ deploy/db/03_team_db.sql (BE-24). Sửa DB script trước, rồi sửa file này cho khớp.
namespace Team.Repositories.Projects;

/// <summary>Bảng <c>team.projects</c> – đề tài xuyên kỳ (rule 26).</summary>
public class Project
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public Guid OriginSemesterId { get; set; }
    public string OriginCourseCode { get; set; } = string.Empty;
    public short CurrentVersion { get; set; } = 1;
    public string Status { get; set; } = "ACTIVE";
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public uint Version { get; set; }
}
