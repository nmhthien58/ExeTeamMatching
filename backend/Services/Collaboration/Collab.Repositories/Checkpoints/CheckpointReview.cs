// Sinh từ deploy/db/06_collab_db.sql (BE-62). Sửa DB script trước, rồi sửa file này cho khớp.
namespace Collab.Repositories.Checkpoints;

/// <summary>Bảng <c>collab.checkpoint_reviews</c>.</summary>
public class CheckpointReview
{
    public Guid Id { get; set; }
    public Guid WorkspaceId { get; set; }
    public short CheckpointNo { get; set; }
    public DateOnly PresentedOn { get; set; }
    public string? Comment { get; set; }
    public Guid ReviewedByLecturerId { get; set; }
    public string ReviewedByName { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; }
}
