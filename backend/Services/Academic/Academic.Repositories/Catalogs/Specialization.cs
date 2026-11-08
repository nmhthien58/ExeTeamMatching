// Sinh từ deploy/db/02_academic_db.sql (BE-15). Sửa DB script trước, rồi sửa file này cho khớp.
namespace Academic.Repositories.Catalogs;

/// <summary>Bảng <c>academic.specializations</c> – danh mục chuyên ngành (rule 12).</summary>
public class Specialization
{
    public int Id { get; set; }
    public int? MajorId { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}
