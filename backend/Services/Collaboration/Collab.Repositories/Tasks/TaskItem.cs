// Sinh từ deploy/db/06_collab_db.sql (BE-62). Sửa DB script trước, rồi sửa file này cho khớp.
namespace Collab.Repositories.Tasks;

/// <summary>Bảng <c>collab.task_items</c>.</summary>
public class TaskItem
{
    public Guid Id { get; set; }
    public Guid WorkspaceId { get; set; }
    public string Kind { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTimeOffset? DueAt { get; set; }
    public int Position { get; set; }
    public string CreatedByType { get; set; } = string.Empty;
    public Guid CreatedById { get; set; }
    public Guid? SourceMeetingId { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}
