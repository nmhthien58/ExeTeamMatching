// Sinh từ deploy/db/06_collab_db.sql (BE-62). Sửa DB script trước, rồi sửa file này cho khớp.
namespace Collab.Repositories.Meetings;

/// <summary>Bảng <c>collab.slot_bookings</c>.</summary>
public class SlotBooking
{
    public Guid Id { get; set; }
    public Guid ScheduleId { get; set; }
    public Guid SlotId { get; set; }
    public Guid WorkspaceId { get; set; }
    public string Source { get; set; } = string.Empty;
    public Guid? BookedById { get; set; }
    public DateTimeOffset BookedAt { get; set; }
    public DateTimeOffset? CancelledAt { get; set; }
}
