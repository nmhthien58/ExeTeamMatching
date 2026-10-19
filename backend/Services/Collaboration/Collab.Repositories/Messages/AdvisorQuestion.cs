// Sinh từ deploy/db/06_collab_db.sql (BE-62). Sửa DB script trước, rồi sửa file này cho khớp.
namespace Collab.Repositories.Messages;

/// <summary>Bảng <c>collab.advisor_questions</c>.</summary>
public class AdvisorQuestion
{
    public Guid MessageId { get; set; }
    public Guid WorkspaceId { get; set; }
    public string Status { get; set; } = string.Empty;
    public string? AnsweredByPersonType { get; set; }
    public Guid? AnsweredByPersonId { get; set; }
    public DateTimeOffset? AnsweredAt { get; set; }
}
