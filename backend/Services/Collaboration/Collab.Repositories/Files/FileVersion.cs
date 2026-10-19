// Sinh từ deploy/db/06_collab_db.sql (BE-62). Sửa DB script trước, rồi sửa file này cho khớp.
namespace Collab.Repositories.Files;

/// <summary>Bảng <c>collab.file_versions</c>.</summary>
public class FileVersion
{
    public Guid Id { get; set; }
    public Guid FileId { get; set; }
    public int Version { get; set; }
    public string ObjectKey { get; set; } = string.Empty;
    public long SizeBytes { get; set; }
    public string ContentType { get; set; } = string.Empty;
    public Guid UploadedById { get; set; }
    public string UploadedByName { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; }
}
