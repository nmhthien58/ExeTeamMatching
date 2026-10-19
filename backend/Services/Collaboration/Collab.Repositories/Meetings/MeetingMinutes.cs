// Sinh từ deploy/db/06_collab_db.sql (BE-62). Sửa DB script trước, rồi sửa file này cho khớp.
namespace Collab.Repositories.Meetings;

/// <summary>Bảng <c>collab.meeting_minutes</c>.</summary>
public class MeetingMinutes
{
    public Guid MeetingId { get; set; }
    public string Content { get; set; } = string.Empty;
    public Guid RecordedById { get; set; }
    public string RecordedByName { get; set; } = string.Empty;
    public DateTimeOffset RecordedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}
