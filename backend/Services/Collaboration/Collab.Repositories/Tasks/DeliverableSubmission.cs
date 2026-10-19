// Sinh từ deploy/db/06_collab_db.sql (BE-62). Sửa DB script trước, rồi sửa file này cho khớp.
namespace Collab.Repositories.Tasks;

/// <summary>Bảng <c>collab.deliverable_submissions</c>.</summary>
public class DeliverableSubmission
{
    public Guid Id { get; set; }
    public Guid TaskId { get; set; }
    public Guid FileVersionId { get; set; }
    public Guid SubmittedById { get; set; }
    public DateTimeOffset SubmittedAt { get; set; }
    public string ReviewStatus { get; set; } = string.Empty;
    public string? ReviewComment { get; set; }
    public Guid? ReviewedById { get; set; }
    public DateTimeOffset? ReviewedAt { get; set; }
}
