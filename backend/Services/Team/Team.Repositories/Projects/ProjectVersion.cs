// Sinh từ deploy/db/03_team_db.sql (BE-24). Sửa DB script trước, rồi sửa file này cho khớp.
namespace Team.Repositories.Projects;

/// <summary>Bảng <c>team.project_versions</c>. Cột <c>version</c> là số phiên bản đề tài, không phải xmin.</summary>
public class ProjectVersion
{
    public Guid ProjectId { get; set; }
    public short Version { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string ChangeType { get; set; } = string.Empty;
    public string? Reason { get; set; }
    public Guid SemesterId { get; set; }
    public string CourseCode { get; set; } = string.Empty;
    public Guid? ChangeRequestId { get; set; }
    public Guid? ChangedByAccountId { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}
