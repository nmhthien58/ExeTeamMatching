// Sinh từ deploy/db/02_academic_db.sql (BE-91). Sửa DB script trước, rồi sửa file này cho khớp.
namespace Academic.Repositories.Catalogs;

/// <summary>Bảng <c>academic.majors</c> – danh mục ngành / tiền tố ngành của MSSV (rule 12, BE-91).</summary>
public class Major
{
    public int Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}
