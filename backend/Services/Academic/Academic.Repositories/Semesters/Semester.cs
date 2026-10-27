// Sinh từ deploy/db/02_academic_db.sql (BE-15). Sửa DB script trước, rồi sửa file này cho khớp.
namespace Academic.Repositories.Semesters;

/// <summary>Bảng <c>academic.semesters</c> (rule 6). Mỗi lúc chỉ một học kỳ <see cref="IsCurrent"/>.</summary>
public class Semester
{
    public Guid Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public DateOnly StartDate { get; set; }
    public DateOnly EndDate { get; set; }
    public bool IsCurrent { get; set; }

    /// <summary>Rule 20: mentor làm việc với nhóm từ tuần này.</summary>
    public short MentoringStartWeek { get; set; } = 6;

    /// <summary>Số buổi mentor dự kiến ("Buổi mentor n/3").</summary>
    public short ExpectedMentorSessions { get; set; } = 3;

    /// <summary>Rule 25: hạn chọn slot họp hằng tuần EXE201 (cuối tuần này).</summary>
    public short SlotBookingDeadlineWeek { get; set; } = 2;

    /// <summary>Rule 25: độ dài một slot họp hằng tuần (phút).</summary>
    public short MeetingSlotMinutes { get; set; } = 15;

    /// <summary>Rule 27: tuần mục tiêu Checkpoint 1–4 (EXE101); null = không nhắc.</summary>
    public short[]? CheckpointTargetWeeks { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}
