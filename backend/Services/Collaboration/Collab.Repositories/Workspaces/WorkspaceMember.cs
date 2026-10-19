// Sinh từ deploy/db/06_collab_db.sql (BE-62). Sửa DB script trước, rồi sửa file này cho khớp.
namespace Collab.Repositories.Workspaces;

/// <summary>Bảng <c>collab.workspace_members</c>.</summary>
public class WorkspaceMember
{
    public Guid WorkspaceId { get; set; }
    public string PersonType { get; set; } = string.Empty;
    public Guid PersonId { get; set; }
    public string DisplayName { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public DateTimeOffset JoinedAt { get; set; }
    public DateTimeOffset? LeftAt { get; set; }
}
