// Sinh từ deploy/db/06_collab_db.sql (BE-62). Sửa DB script trước, rồi sửa file này cho khớp.
namespace Collab.Repositories.Files;

/// <summary>Bảng <c>collab.folders</c>.</summary>
public class Folder
{
    public Guid Id { get; set; }
    public Guid WorkspaceId { get; set; }
    public Guid? ParentId { get; set; }
    public string Name { get; set; } = string.Empty;
    public bool IsArchive { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}
