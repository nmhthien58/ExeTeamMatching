// Sinh từ deploy/db/06_collab_db.sql (BE-62). Sửa DB script trước, rồi sửa file này cho khớp.
namespace Collab.Repositories.Meetings;

/// <summary>Bảng <c>collab.meeting_slots</c>.</summary>
public class MeetingSlot
{
    public Guid Id { get; set; }
    public Guid MeetingId { get; set; }
    public DateTimeOffset StartsAt { get; set; }
    public short DurationMinutes { get; set; }
}
