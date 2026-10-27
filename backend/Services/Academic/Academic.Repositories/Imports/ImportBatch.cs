// Sinh từ deploy/db/02_academic_db.sql (BE-15). Sửa DB script trước, rồi sửa file này cho khớp.
namespace Academic.Repositories.Imports;

/// <summary>Bảng <c>academic.import_batches</c> – một lần import danh sách sinh viên (W06–W08).</summary>
public class ImportBatch
{
    public Guid Id { get; set; }
    public Guid SemesterId { get; set; }
    public string FileName { get; set; } = string.Empty;
    public int TotalRows { get; set; }
    public int SuccessRows { get; set; }
    public int FailedRows { get; set; }

    /// <summary>Cột <c>jsonb</c>: <c>[{row, field, message}]</c>.</summary>
    public string? Errors { get; set; }

    /// <summary><c>PROCESSING</c>, <c>COMPLETED</c>, <c>FAILED</c>.</summary>
    public string Status { get; set; } = "PROCESSING";

    /// <summary><c>identity.user_accounts.id</c>.</summary>
    public Guid ImportedBy { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }
}
