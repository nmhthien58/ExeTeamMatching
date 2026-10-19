// Sinh từ deploy/db/06_collab_db.sql (BE-62). Sửa DB script trước, rồi sửa file này cho khớp.
namespace Collab.Repositories.Files;

/// <summary>Bảng <c>collab.file_comments</c>.</summary>
public class FileComment
{
    public Guid Id { get; set; }
    public Guid FileId { get; set; }
    public Guid? FileVersionId { get; set; }
    public string AuthorPersonType { get; set; } = string.Empty;
    public Guid AuthorPersonId { get; set; }
    public string AuthorName { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; }
}
