// Sinh từ deploy/db/02_academic_db.sql (BE-15). Sửa DB script trước, rồi sửa file này cho khớp.
namespace Academic.Repositories.Catalogs;

/// <summary>Bảng <c>academic.domains</c> – danh mục lĩnh vực D01–D18 (rule 12).</summary>
public class Domain
{
    public int Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}
