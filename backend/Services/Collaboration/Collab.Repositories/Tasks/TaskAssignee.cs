// Sinh từ deploy/db/06_collab_db.sql (BE-62). Sửa DB script trước, rồi sửa file này cho khớp.
namespace Collab.Repositories.Tasks;

/// <summary>Bảng <c>collab.task_assignees</c>.</summary>
public class TaskAssignee
{
    public Guid TaskId { get; set; }
    public Guid StudentId { get; set; }
}
