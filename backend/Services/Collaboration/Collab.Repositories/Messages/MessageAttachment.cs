// Sinh từ deploy/db/06_collab_db.sql (BE-62). Sửa DB script trước, rồi sửa file này cho khớp.
namespace Collab.Repositories.Messages;

/// <summary>Bảng <c>collab.message_attachments</c>.</summary>
public class MessageAttachment
{
    public Guid MessageId { get; set; }
    public Guid FileId { get; set; }
}
