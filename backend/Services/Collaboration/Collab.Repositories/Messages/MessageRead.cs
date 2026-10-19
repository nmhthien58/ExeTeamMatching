// Sinh từ deploy/db/06_collab_db.sql (BE-62). Sửa DB script trước, rồi sửa file này cho khớp.
namespace Collab.Repositories.Messages;

/// <summary>Bảng <c>collab.message_reads</c>.</summary>
public class MessageRead
{
    public Guid ChannelId { get; set; }
    public string PersonType { get; set; } = string.Empty;
    public Guid PersonId { get; set; }
    public Guid? LastReadMessageId { get; set; }
    public DateTimeOffset LastReadAt { get; set; }
}
