// Sinh từ deploy/db/06_collab_db.sql (BE-62). Sửa DB script trước, rồi sửa file này cho khớp.
namespace Collab.Repositories.Workspaces;

/// <summary>Bảng <c>collab.workspaces</c>.</summary>
public class Workspace
{
    public Guid Id { get; set; }
    public Guid TeamId { get; set; }
    public Guid? ProjectId { get; set; }
    public Guid SemesterId { get; set; }
    public string CourseCode { get; set; } = string.Empty;
    public string TeamName { get; set; } = string.Empty;
    public Guid? PreviousWorkspaceId { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? ArchivedAt { get; set; }
}
