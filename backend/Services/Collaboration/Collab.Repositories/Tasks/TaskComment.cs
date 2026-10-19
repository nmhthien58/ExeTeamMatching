// Sinh từ deploy/db/06_collab_db.sql (BE-62). Sửa DB script trước, rồi sửa file này cho khớp.
namespace Collab.Repositories.Tasks;

/// <summary>Bảng <c>collab.task_comments</c>.</summary>
public class TaskComment
{
    public Guid Id { get; set; }
    public Guid TaskId { get; set; }
    public string AuthorPersonType { get; set; } = string.Empty;
    public Guid AuthorPersonId { get; set; }
    public string AuthorName { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; }
}
