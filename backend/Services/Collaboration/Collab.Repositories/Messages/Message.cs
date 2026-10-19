// Sinh từ deploy/db/06_collab_db.sql (BE-62). Sửa DB script trước, rồi sửa file này cho khớp.
namespace Collab.Repositories.Messages;

/// <summary>Bảng <c>collab.messages</c>.</summary>
public class Message
{
    public Guid Id { get; set; }
    public Guid ChannelId { get; set; }
    public Guid? ParentMessageId { get; set; }
    public string AuthorPersonType { get; set; } = string.Empty;
    public Guid AuthorPersonId { get; set; }
    public string AuthorName { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public bool IsAdvisorQuestion { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? EditedAt { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }
}
