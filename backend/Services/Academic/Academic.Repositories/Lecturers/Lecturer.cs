// Sinh từ deploy/db/02_academic_db.sql (BE-15). Sửa DB script trước, rồi sửa file này cho khớp.
namespace Academic.Repositories.Lecturers;

/// <summary>Bảng <c>academic.lecturers</c> – giảng viên do Admin nhập email.</summary>
public class Lecturer
{
    public Guid Id { get; set; }
    public string FullName { get; set; } = string.Empty;

    /// <summary>Cột <c>citext</c>: so sánh không phân biệt hoa thường.</summary>
    public string Email { get; set; } = string.Empty;

    public string? Phone { get; set; }

    /// <summary>Rule 9: số nhóm tối đa do Admin đặt; null = không giới hạn.</summary>
    public short? MaxTeamsExe101 { get; set; }

    public short? MaxTeamsExe201 { get; set; }

    /// <summary>BE-93: số nhóm mong muốn EXE101 do GV khai báo; null = chưa khai báo.</summary>
    public short? DesiredTeamsExe101 { get; set; }

    /// <summary>BE-93: số nhóm mong muốn EXE201 do GV khai báo; null = chưa khai báo.</summary>
    public short? DesiredTeamsExe201 { get; set; }

    /// <summary>BE-93: thời điểm GV hoàn tất khai báo hồ sơ (chuyên môn + số nhóm).</summary>
    public DateTimeOffset? ProfileCompletedAt { get; set; }

    public bool IsActive { get; set; } = true;
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}
