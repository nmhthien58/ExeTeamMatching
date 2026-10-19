// Sinh từ deploy/db/06_collab_db.sql (BE-62). Sửa DB script trước, rồi sửa file này cho khớp.
namespace Collab.Repositories.Meetings;

/// <summary>Bảng <c>collab.meeting_reminders</c>.</summary>
public class MeetingReminder
{
    public Guid MeetingId { get; set; }
    public short OffsetMinutes { get; set; }
    public DateTimeOffset SentAt { get; set; }
}
