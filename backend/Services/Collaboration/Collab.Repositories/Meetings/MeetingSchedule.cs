// Sinh từ deploy/db/06_collab_db.sql (BE-62). Sửa DB script trước, rồi sửa file này cho khớp.
namespace Collab.Repositories.Meetings;

/// <summary>Bảng <c>collab.meeting_schedules</c>.</summary>
public class MeetingSchedule
{
    public Guid Id { get; set; }
    public Guid LecturerId { get; set; }
    public string LecturerName { get; set; } = string.Empty;
    public Guid SemesterId { get; set; }
    public string CourseCode { get; set; } = string.Empty;
    public short SlotMinutes { get; set; }
    public string Location { get; set; } = string.Empty;
    public DateTimeOffset BookingDeadline { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTimeOffset? PublishedAt { get; set; }
    public DateTimeOffset? ClosedAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}
